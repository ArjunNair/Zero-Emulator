using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Peripherals;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation.Tape
{
    public enum TapeDeckStatus { Empty, Stopped, Playing }

    /// <summary>Descriptive fields from a PZXT header or a TZX archive info block (all optional).</summary>
    public sealed class TapeMetadata
    {
        public string Title { get; set; }
        public string Publisher { get; set; }
        public IReadOnlyList<string> Authors { get; set; } = Array.Empty<string>();
        public string Year { get; set; }
        public string Language { get; set; }
        public string Type { get; set; }
        public string Price { get; set; }
        public string Protection { get; set; }
        public string Origin { get; set; }
        public IReadOnlyList<string> Comments { get; set; } = Array.Empty<string>();

        public bool HasDetails => Publisher != null || Authors.Count > 0 || Year != null || Language != null
                                  || Type != null || Price != null || Protection != null || Origin != null || Comments.Count > 0;
    }

    public sealed class TapeBlockInfo
    {
        public int Index { get; set; }
        public string Block { get; set; }
        public string Info { get; set; }
    }

    /// <summary>
    /// Host-neutral tape deck: the logic from the WinForms TapeDeck form without the form.
    /// All members must be called on the emulation thread (the session marshals for you).
    /// PZX, TZX and CSW images all decode into one <see cref="TapeImage"/>, which the machine's
    /// <see cref="TapePlayer"/> plays; TAP is converted to PZX on insert.
    /// </summary>
    public sealed class TapeDeck
    {
        private zx_spectrum _zx;
        private TapeImage _image = TapeImage.Empty;
        private string _tapSavePath;
        private bool _tapFileOpen;

        public string FileName { get; private set; } = "";
        public string Title { get; private set; } = "";
        public TapeMetadata Metadata { get; private set; } = new TapeMetadata();
        public bool IsInserted { get; private set; }
        public bool IsPlaying => _zx != null && _zx.tapeIsPlaying;
        public int CurrentBlock => _zx?.blockCounter ?? 0;
        public IReadOnlyList<TapeBlockInfo> Blocks => _blocks;
        private readonly List<TapeBlockInfo> _blocks = new List<TapeBlockInfo>();

        // ------------------------------------------------------------------ tape counter

        /// <summary>
        /// Seconds of tape that have run past the head. Moves only while the tape plays, as a real
        /// deck's does; rewinding and block skips set it to the start of the block that will play.
        /// </summary>
        public double PositionSeconds
        {
            get
            {
                int b = _counterBlock;
                if (b < 0 || b >= _blockStart.Count) return 0;
                return _blockStart[b] + Math.Min(_counterElapsed, _blockLength[b]);
            }
        }

        /// <summary>The three-digit reel counter at the current position: see <see cref="CounterAt"/>.</summary>
        public int Counter => CounterAt(PositionSeconds);

        /// <summary>
        /// What a cassette recorder's mechanical counter reads after this many seconds of play. A belt
        /// from the take-up spool drives it, so it counts turns of the reel, and as the reel fills each
        /// turn takes up more tape: it counts about once every 1.5 seconds at the start and slows from
        /// there. Modelled on a C90 (12 µm tape on an 11 mm hub at 4.76 cm/s) with one count a turn,
        /// which ends a five-minute game near 187 and a C60 side near 850; a C90 side rolls over.
        /// </summary>
        public static int CounterAt(double seconds)
        {
            const double hub = 0.011, thickness = 12e-6, speed = 0.0476;
            if (seconds <= 0) return 0;
            double radius = Math.Sqrt(hub * hub + speed * thickness * seconds / Math.PI);
            return (int)((radius - hub) / thickness) % 1000;
        }

        // Start and length of each block in seconds, indexed like the tape's blocks, which is what
        // the machine's blockCounter indexes too.
        private readonly List<double> _blockStart = new List<double>();
        private readonly List<double> _blockLength = new List<double>();
        private int _counterBlock;
        private double _counterElapsed;

        private const double TapeClock = 3500000.0; // tape durations are in 48K T-states

        // A block in a loop is counted once: the counter is a guide, not a measurement.
        private void MeasureBlocks()
        {
            _blockStart.Clear();
            _blockLength.Clear();
            double at = 0;
            foreach (TapeBlock b in _image.Blocks)
            {
                double tstates = b.DurationTStates;
                _blockStart.Add(at);
                _blockLength.Add(tstates / TapeClock);
                at += tstates / TapeClock;
            }
            CueCounter(0);
        }

        /// <summary>Park the counter at the start of a block.</summary>
        private void CueCounter(int block)
        {
            _counterBlock = Math.Clamp(block, 0, Math.Max(0, _blockStart.Count - 1));
            _counterElapsed = 0;
        }

        /// <summary>
        /// Emulated time has passed: run the counter on if the tape is playing. Called by the session
        /// after each batch of frames, on the emulation thread.
        /// </summary>
        internal void AdvanceCounter(double seconds)
        {
            if (!IsPlaying) return;
            int block = _zx.blockCounter;
            if (block != _counterBlock) CueCounter(block); // the machine moved on to the next block
            _counterElapsed += seconds;
        }

        public TapeDeckStatus Status => !IsInserted ? TapeDeckStatus.Empty : IsPlaying ? TapeDeckStatus.Playing : TapeDeckStatus.Stopped;

        /// <summary>When the machine last SAVEd a block to <see cref="TapSavePath"/> (UTC), or MinValue.</summary>
        public DateTime LastSaveUtc { get; private set; } = DateTime.MinValue;

        /// <summary>Where SAVE "" output goes. Appended to if it exists (matches the original deck).</summary>
        public string TapSavePath
        {
            get => _tapSavePath;
            set { _tapSavePath = value; _tapFileOpen = false; }
        }

        // Options mirrored into the machine.
        private bool _edgeLoad = true, _autoPlay = true, _fastLoad, _autoLoad;

        public bool EdgeLoad { get => _edgeLoad; set { _edgeLoad = value; if (_zx != null) _zx.tape_edgeLoad = value; } }
        public bool AutoPlay { get => _autoPlay; set { _autoPlay = value; if (_zx != null) _zx.tape_AutoPlay = value; } }
        public bool FastLoad { get => _fastLoad; set { _fastLoad = value; if (_zx != null) _zx.tape_flashLoad = value; } }
        /// <summary>Type LOAD "" after a hard reset when a tape is inserted.</summary>
        public bool AutoLoad { get => _autoLoad; set => _autoLoad = value; }

        /// <summary>Raised (on the emulation thread) whenever status, position or contents change.</summary>
        public event Action Changed;
        /// <summary>Raised when the machine finishes a SAVE block, with the TAP file it went to.</summary>
        public event Action<string> BlockSaved;
        public event Action<string> Error;

        internal void Attach(zx_spectrum zx)
        {
            Detach();
            _zx = zx;
            _zx.TapeEvent += OnTapeEvent;
            _zx.tape_edgeLoad = _edgeLoad;
            _zx.tape_AutoPlay = _autoPlay;
            _zx.tape_flashLoad = _fastLoad;
            _zx.tape_readToPlay = IsInserted;
            if (IsInserted)
            {
                _zx.tapeFilename = FileName;
                _zx.InsertTape(_image);
            }
        }

        internal void Detach()
        {
            if (_zx != null) _zx.TapeEvent -= OnTapeEvent;
            _zx = null;
        }

        public static bool IsTapeExtension(string ext)
        {
            switch (ext.ToLowerInvariant()) { case ".pzx": case ".tap": case ".tzx": case ".csw": return true; }
            return false;
        }

        /// <summary>Insert a tape image from disk. Returns false (and raises Error) on failure.</summary>
        public bool Insert(string path)
        {
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception ex) { Error?.Invoke("Unable to open tape: " + ex.Message); return false; }
            return Insert(path, bytes);
        }

        /// <summary>Insert a tape image from memory; <paramref name="name"/> supplies the extension and display name.</summary>
        public bool Insert(string name, byte[] bytes)
        {
            string ext = Path.GetExtension(name).ToLowerInvariant();
            TapeImage image;
            switch (ext)
            {
                case ".pzx":
                    image = PZXFile.Load(bytes);
                    break;
                case ".tap":
                    byte[] pzx = TapFile.ToPZX(bytes, 500);
                    if (pzx == null) { Error?.Invoke("This doesn't seem to be a valid TAP file."); return false; }
                    image = PZXFile.Load(pzx);
                    break;
                case ".tzx":
                    image = TZXFile.Load(bytes);
                    break;
                case ".csw":
                    image = CSWFile.Load(bytes);
                    break;
                default:
                    Error?.Invoke("Unrecognised tape format: " + ext);
                    return false;
            }

            if (image == null || image.Blocks.Count == 0)
            {
                Error?.Invoke("This doesn't seem to be a valid tape file.");
                return false;
            }
            Eject();
            _image = image;

            FileName = name;
            IsInserted = true;
            _tapFileOpen = false;
            ReadTapeInfo(name);
            MeasureBlocks();

            if (_zx != null)
            {
                _zx.InsertTape(_image);
                _zx.tapeIsPlaying = false;
                _zx.tape_readToPlay = true;
                _zx.tapeFilename = name;
                _zx.tapeBitWasFlipped = false;
            }
            Changed?.Invoke();
            return true;
        }

        private void ReadTapeInfo(string name)
        {
            _blocks.Clear();
            int i = 1;
            foreach (TapeBlock b in _image.Blocks)
                _blocks.Add(new TapeBlockInfo { Index = i++, Block = b.Kind, Info = b.Info });

            Title = Path.GetFileNameWithoutExtension(name);
            Metadata = new TapeMetadata();
            TapeInfo h = _image.Info;
            if (h != null)
            {
                if (!string.IsNullOrEmpty(h.Title)) Title = h.Title;
                Metadata = new TapeMetadata
                {
                    Title = h.Title, Publisher = h.Publisher, Authors = h.Authors.ToArray(), Year = h.Year,
                    Language = h.Language, Type = h.Type, Price = h.Price, Protection = h.Protection,
                    Origin = h.Origin, Comments = h.Comments.ToArray()
                };
            }
        }

        public void Eject()
        {
            if (!IsInserted) return;
            _image = TapeImage.Empty;
            _blocks.Clear();
            _blockStart.Clear();
            _blockLength.Clear();
            CueCounter(0);
            if (_zx != null)
            {
                _zx.StopTape(true);
                _zx.InsertTape(TapeImage.Empty);
                _zx.tape_readToPlay = false;
                _zx.tapeFilename = "";
            }
            IsInserted = false;
            _tapFileOpen = false;
            FileName = "";
            Title = "";
            Metadata = new TapeMetadata();
            Changed?.Invoke();
        }

        public void Play()
        {
            if (!IsInserted || _zx == null) return;
            _zx.tape_heldStopped = false;
            _zx.tapeIsPlaying = true;
            _zx.tape_readToPlay = true;
            if (_fastLoad) _zx.ResetKeyboard();
            _zx.ReplayTape();
            CueCounter(_zx.blockCounter); // a stopped tape restarts its block from the top
            Changed?.Invoke();
        }

        /// <summary>
        /// Stop the tape, and keep it stopped: auto play and fast loading leave it alone until Play
        /// (or a rewind, a new tape or a reset), as a real deck's loader would find no tape.
        /// </summary>
        public void Stop()
        {
            if (!IsInserted || _zx == null) return;
            _zx.tape_heldStopped = true;
            _zx.StopTape(true);
            Changed?.Invoke();
        }

        public void Rewind()
        {
            if (!IsInserted || _zx == null) return;
            _zx.StopTape(true);
            _zx.tape_heldStopped = false;
            _zx.tape.Cue(0);
            CueCounter(0);
            Changed?.Invoke();
        }

        public void PreviousBlock()
        {
            if (!IsInserted || _zx == null) return;
            if (_zx.blockCounter > 0) _zx.tape.Cue(_zx.blockCounter - 1);
            if (!IsPlaying) CueCounter(_zx.blockCounter);
            Changed?.Invoke();
        }

        public void NextBlock()
        {
            if (!IsInserted || _zx == null) return;
            if (_zx.blockCounter < _image.Blocks.Count - 1) _zx.tape.Cue(_zx.blockCounter + 1);
            if (!IsPlaying) CueCounter(_zx.blockCounter);
            Changed?.Invoke();
        }

        private void OnTapeEvent(object sender, TapeEventArgs e)
        {
            switch (e.EventType)
            {
                case TapeEventType.SAVE_TAP:
                    SaveTapBlock();
                    break;
                case TapeEventType.CLOSE_TAP:
                    _tapFileOpen = false;
                    if (_tapSavePath != null && File.Exists(_tapSavePath))
                        Insert(_tapSavePath);
                    break;
                case TapeEventType.NEXT_BLOCK:
                    if (_zx != null && IsInserted && _zx.blockCounter >= _blocks.Count)
                        _zx.StopTape(true); // ran off the end: not the user's stop, so nothing is held
                    Changed?.Invoke();
                    break;
                case TapeEventType.START_TAPE:
                case TapeEventType.STOP_TAPE:
                    Changed?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// The ROM SAVE trap fired: A = flag byte, IX = start, DE = length. Append a TAP block.
        /// The first save of a session ejects any inserted tape (you cannot save onto the tape you
        /// are loading from), exactly like the original deck.
        /// </summary>
        private void SaveTapBlock()
        {
            if (_zx == null) return;
            if (string.IsNullOrEmpty(_tapSavePath))
            {
                Error?.Invoke("No TAP file is set for saving. Choose one in the tape deck first.");
                return;
            }
            if (!_tapFileOpen && IsInserted)
                Eject();
            _tapFileOpen = true;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_tapSavePath)));
                using (var tapFile = new FileStream(_tapSavePath, FileMode.Append))
                using (var w = new BinaryWriter(tapFile))
                {
                    int blockId = _zx.cpu.regs.AF_ >> 8;
                    int start = _zx.cpu.regs.IX;
                    int length = _zx.cpu.regs.DE;
                    int checksum = blockId;
                    w.Write((short)(length + 2));
                    w.Write((byte)blockId);
                    for (int f = start; f < start + length; f++)
                    {
                        byte data = _zx.PeekByteNoContend((ushort)f);
                        w.Write(data);
                        checksum ^= data;
                    }
                    w.Write((byte)checksum);
                }
                LastSaveUtc = DateTime.UtcNow;
                BlockSaved?.Invoke(_tapSavePath);
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to write TAP file: " + ex.Message);
            }
        }
    }
}
