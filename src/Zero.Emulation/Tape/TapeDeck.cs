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

    /// <summary>Descriptive fields from the PZXT header block (all optional).</summary>
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
    /// PZX is the only format the core plays; TAP is converted on insert, TZX/CSW are rejected
    /// until a managed converter exists.
    /// </summary>
    public sealed class TapeDeck
    {
        private zx_spectrum _zx;
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

        public TapeDeckStatus Status => !IsInserted ? TapeDeckStatus.Empty : IsPlaying ? TapeDeckStatus.Playing : TapeDeckStatus.Stopped;

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
                _zx.blockCounter = 0;
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
            byte[] pzx;
            switch (ext)
            {
                case ".pzx":
                    pzx = bytes;
                    break;
                case ".tap":
                    pzx = TapFile.ToPZX(bytes, 500);
                    if (pzx == null) { Error?.Invoke("This doesn't seem to be a valid TAP file."); return false; }
                    break;
                case ".tzx":
                case ".csw":
                    Error?.Invoke("TZX and CSW tapes are not supported in this build yet. Please convert the tape to PZX or TAP.");
                    return false;
                default:
                    Error?.Invoke("Unrecognised tape format: " + ext);
                    return false;
            }

            Eject();
            if (!PZXFile.LoadPZX(ref pzx))
            {
                Error?.Invoke("This doesn't seem to be a valid tape file.");
                return false;
            }

            FileName = name;
            IsInserted = true;
            _tapFileOpen = false;
            ReadTapeInfo(name);

            if (_zx != null)
            {
                _zx.ResetTape();
                _zx.tapeIsPlaying = false;
                _zx.tape_readToPlay = true;
                _zx.tapeFilename = name;
                _zx.blockCounter = 0;
                _zx.tapeBitWasFlipped = false;
            }
            Changed?.Invoke();
            return true;
        }

        private void ReadTapeInfo(string name)
        {
            _blocks.Clear();
            PZXFile.ReadTapeInfo(name);
            int i = 1;
            foreach (PZX_TapeInfo info in PZXFile.tapeBlockInfo)
                _blocks.Add(new TapeBlockInfo { Index = i++, Block = info.Block, Info = info.Info });

            Title = Path.GetFileNameWithoutExtension(name);
            Metadata = new TapeMetadata();
            foreach (PZXFile.Block b in PZXFile.blocks)
            {
                if (!(b is PZXFile.PZXT_Header h)) continue;
                if (!string.IsNullOrEmpty(h.Title)) Title = h.Title;
                Metadata = new TapeMetadata
                {
                    Title = h.Title, Publisher = h.Publisher, Authors = h.Authors.ToArray(), Year = h.YearOfPublication,
                    Language = h.Language, Type = h.Type, Price = h.Price, Protection = h.ProtectionScheme,
                    Origin = h.Origin, Comments = h.Comments.ToArray()
                };
            }
        }

        public void Eject()
        {
            if (!IsInserted) return;
            PZXFile.blocks.Clear();
            PZXFile.tapeBlockInfo.Clear();
            _blocks.Clear();
            if (_zx != null)
            {
                _zx.StopTape(true);
                _zx.ResetTape();
                _zx.blockCounter = 0;
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
            _zx.tapeIsPlaying = true;
            _zx.tapeTStates = 0;
            _zx.tape_readToPlay = true;
            if (_fastLoad) _zx.ResetKeyboard();
            _zx.blockCounter--;
            if (_zx.blockCounter < 0) _zx.blockCounter = 0;
            _zx.NextPZXBlock();
            Changed?.Invoke();
        }

        public void Stop()
        {
            if (!IsInserted || _zx == null) return;
            _zx.StopTape(true);
            Changed?.Invoke();
        }

        public void Rewind()
        {
            if (!IsInserted || _zx == null) return;
            _zx.StopTape(true);
            _zx.blockCounter = 0;
            Changed?.Invoke();
        }

        public void PreviousBlock()
        {
            if (!IsInserted || _zx == null) return;
            if (_zx.blockCounter > 0) _zx.blockCounter--;
            Changed?.Invoke();
        }

        public void NextBlock()
        {
            if (!IsInserted || _zx == null) return;
            if (_zx.blockCounter < PZXFile.tapeBlockInfo.Count - 1) _zx.blockCounter++;
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
                    if (_zx != null && IsInserted && _zx.blockCounter - 1 >= _blocks.Count)
                        Stop();
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
                BlockSaved?.Invoke(_tapSavePath);
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to write TAP file: " + ex.Message);
            }
        }
    }
}
