using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Peripherals;
using Speccy;
using SpeccyCommon;

namespace Zero.Emulation
{
    /// <summary>File loading, snapshots and RZX (the LoadZXFile family from Form1, host-neutral).</summary>
    public sealed partial class EmulatorSession
    {
        public static readonly string[] SnapshotExtensions = { ".szx", ".sna", ".z80" };
        public static readonly string[] TapeExtensions = { ".pzx", ".tap", ".tzx", ".csw" };
        public static readonly string[] OtherExtensions = { ".rzx", ".scr", ".zip" };
        public static readonly string[] DiskExtensions = { ".dsk", ".trd", ".scl" };

        public static IEnumerable<string> AllOpenableExtensions =>
            SnapshotExtensions.Concat(TapeExtensions).Concat(OtherExtensions);

        /// <summary>
        /// Called (on the emulation thread) when a ZIP holds several loadable files. Return the entry to
        /// open or null to cancel. Default: the first entry.
        /// </summary>
        public Func<IReadOnlyList<string>, string> ChooseArchiveEntry { get; set; }

        /// <summary>Open any supported file. Errors surface through <see cref="Error"/>.</summary>
        public void LoadFile(string path) => Post(() => LoadFileCore(path));

        /// <summary>Open a file and await the outcome (true = loaded).</summary>
        public System.Threading.Tasks.Task<bool> LoadFileAsync(string path) => InvokeAsync(() => LoadFileCore(path));

        private bool LoadFileCore(string path)
        {
            if (!File.Exists(path))
            {
                Error?.Invoke("Unable to open file: " + path);
                return false;
            }
            bool ok = LoadBytes(path, () => File.ReadAllBytes(path), path);
            if (ok)
            {
                Settings.AddRecentFile(path);
                FileLoaded?.Invoke(path);
            }
            return ok;
        }

        /// <summary>Dispatch on extension. <paramref name="name"/> decides the type; bytes are read lazily.</summary>
        private bool LoadBytes(string name, Func<byte[]> readBytes, string diskPath)
        {
            if (_zx == null) { Error?.Invoke("No machine is running."); return false; }
            string ext = Path.GetExtension(name).ToLowerInvariant();
            try
            {
                switch (ext)
                {
                    case ".sna": return UseSnapshot(SNAFile.LoadSNA(new MemoryStream(readBytes())));
                    case ".z80": return UseSnapshot(Z80File.LoadZ80(new MemoryStream(readBytes())));
                    case ".szx":
                    {
                        var szx = new SZXFile();
                        byte[] bytes = readBytes();
                        if (!szx.LoadSZX(ref bytes)) { Error?.Invoke("Invalid SZX snapshot."); return false; }
                        return UseSzx(szx);
                    }
                    case ".scr": return LoadScreen(readBytes());
                    case ".pzx":
                    case ".tap":
                    case ".tzx":
                    case ".csw":
                    {
                        if (!Tape.Insert(name, readBytes())) return false;
                        if (Tape.AutoLoad) BeginAutoLoad();
                        return true;
                    }
                    case ".rzx":
                    {
                        if (diskPath == null)
                        {
                            diskPath = WriteTemp(name, readBytes());
                        }
                        return StartRzxPlaybackCore(diskPath);
                    }
                    case ".zip": return LoadArchive(name, readBytes());
                    case ".dsk":
                    case ".trd":
                    case ".scl":
                        Error?.Invoke("Disk images are not supported in this build (no floppy controller yet).");
                        return false;
                    default:
                        Error?.Invoke("Sorry, Zero doesn't recognise this file format: " + ext);
                        return false;
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke("Failed to load " + Path.GetFileName(name) + ": " + ex.Message);
                return false;
            }
        }

        private static string WriteTemp(string name, byte[] bytes)
        {
            string dir = AppPaths.EnsureDirectory(AppPaths.TempDirectory);
            string path = Path.Combine(dir, "temp_" + Guid.NewGuid().ToString("N") + Path.GetExtension(name));
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private bool LoadArchive(string zipName, byte[] zipBytes)
        {
            using (var archive = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
            {
                var candidates = archive.Entries
                    .Where(e => AllOpenableExtensions.Contains(Path.GetExtension(e.Name).ToLowerInvariant()) && Path.GetExtension(e.Name).ToLowerInvariant() != ".zip")
                    .Select(e => e.FullName)
                    .ToList();

                if (candidates.Count == 0)
                {
                    Error?.Invoke("The archive contains no files Zero can open.");
                    return false;
                }

                string chosen = candidates.Count == 1 ? candidates[0] : (ChooseArchiveEntry?.Invoke(candidates) ?? candidates[0]);
                if (chosen == null) return false;

                ZipArchiveEntry entry = archive.GetEntry(chosen);
                using (var ms = new MemoryStream())
                {
                    using (Stream s = entry.Open()) s.CopyTo(ms);
                    byte[] data = ms.ToArray();
                    return LoadBytes(entry.Name, () => data, null);
                }
            }
        }

        // ------------------------------------------------------------------ snapshots

        private bool LoadScreen(byte[] data)
        {
            if (data.Length < 6912) { Error?.Invoke("This file seems to have an unsupported screen format."); return false; }
            for (int f = 0; f < 6912; f++)
                _zx.PokeByteNoContend(16384 + f, data[f]);
            return true;
        }

        private bool UseSnapshot(SNA_SNAPSHOT sna)
        {
            if (sna == null) { Error?.Invoke("Invalid SNA snapshot."); return false; }
            // 128K .sna files are Pentagon-flavoured in Zero, as in the original.
            EnsureMachine(sna is SNA_128K ? MachineModel._pentagon : MachineModel._48k);
            _zx.UseSNA(sna);
            return true;
        }

        private bool UseSnapshot(Z80_SNAPSHOT z80)
        {
            if (z80 == null) { Error?.Invoke("Invalid Z80 snapshot."); return false; }
            MachineModel target;
            switch (z80.TYPE)
            {
                case 0: target = MachineModel._48k; break;
                case 1: target = Settings.Emulation.Use128keForSnapshots ? MachineModel._128ke : MachineModel._128k; break;
                case 2: target = MachineModel._plus3; break;
                case 3: target = MachineModel._pentagon; break;
                default: Error?.Invoke("Unsupported machine in Z80 snapshot."); return false;
            }
            EnsureMachine(target);
            _zx.UseZ80(z80);
            return true;
        }

        private bool UseSzx(SZXFile szx)
        {
            bool force128ke = Settings.Emulation.Use128keForSnapshots;
            switch ((SZXFile.ZXTYPE)szx.header.MachineId)
            {
                case SZXFile.ZXTYPE.ZXSTMID_NTSC48K:
                case SZXFile.ZXTYPE.ZXSTMID_48K:
                    if (force128ke)
                    {
                        EnsureMachine(MachineModel._128ke);
                        _zx.Out(0x7ffd, 0x10);
                        _zx.Out(0x1ffd, 0x04);
                        _zx.pagingDisabled = true;
                    }
                    else EnsureMachine(MachineModel._48k);
                    break;
                case SZXFile.ZXTYPE.ZXSTMID_128KE:
                    EnsureMachine(MachineModel._128ke);
                    break;
                case SZXFile.ZXTYPE.ZXSTMID_128K:
                    EnsureMachine(force128ke ? MachineModel._128ke : MachineModel._128k);
                    break;
                case SZXFile.ZXTYPE.ZXSTMID_PENTAGON128:
                    EnsureMachine(MachineModel._pentagon);
                    break;
                case SZXFile.ZXTYPE.ZXSTMID_PLUS3:
                    EnsureMachine(MachineModel._plus3);
                    break;
                default:
                    Error?.Invoke("This SZX snapshot is for a machine Zero doesn't emulate yet.");
                    return false;
            }

            if (szx.InsertTape)
            {
                if (szx.tape.flags != 0 && szx.embeddedTapeData != null)
                {
                    string ext = "." + new string(szx.tape.fileExtension, 0, 3).TrimEnd('\0');
                    Tape.Insert("embedded" + ext, szx.embeddedTapeData);
                }
                else if (!string.IsNullOrEmpty(szx.externalTapeFile) && File.Exists(szx.externalTapeFile))
                {
                    Tape.Insert(szx.externalTapeFile);
                }
                else if (!string.IsNullOrEmpty(szx.externalTapeFile))
                {
                    Error?.Invoke("The snapshot expects this tape in the deck: " + szx.externalTapeFile);
                }
            }

            Settings.Emulation.LateTimings = (szx.header.Flags & 0x1) != 0;
            _zx.UseSZX(szx);
            return true;
        }

        private void EnsureMachine(MachineModel model)
        {
            if (_zx == null || Model != model)
                CreateMachine(model);
        }

        public void SaveSnapshot(string path) => Post(() =>
        {
            if (_zx == null) return;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".sna") _zx.SaveSNA(path);
                else if (ext == ".scr") SaveScreen(path);
                else _zx.SaveSZX(path);
            }
            catch (Exception ex) { Error?.Invoke("Unable to save snapshot: " + ex.Message); }
        });

        private void SaveScreen(string path)
        {
            var data = new byte[6912];
            for (int f = 0; f < 6912; f++)
                data[f] = _zx.PeekByteNoContend((ushort)(16384 + f));
            File.WriteAllBytes(path, data);
        }

        // ------------------------------------------------------------------ RZX

        private bool StartRzxPlaybackCore(string path)
        {
            StopRzxCore();
            _modelBeforeRzx = Model;
            var rzx = new RZXFile();
            rzx.OnError += msg => Error?.Invoke(msg);
            rzx.RZXFileEventHandler += OnRzxEvent;
            if (!rzx.Playback(path))
            {
                Error?.Invoke("Unable to play this RZX recording.");
                return false;
            }
            _zx.StartPlaybackRZX(rzx);
            SetState(_paused ? EmulatorState.Paused : EmulatorState.PlayingRzx);
            return true;
        }

        public void StopRzx() => Post(StopRzxCore);

        private void StopRzxCore()
        {
            if (_zx == null) return;
            if (_zx.isPlayingRZX) _zx.StopPlaybackRZX();
            else if (_zx.isRecordingRZX) FinishRzxRecordingCore();
            if (_state == EmulatorState.PlayingRzx || _state == EmulatorState.RecordingRzx)
                SetState(RunningState());
        }

        // ------------------------------------------------------------------ RZX recording

        /// <summary>The file the current recording is being written to, or null when not recording.</summary>
        private string _rzxRecordingPath;

        public bool IsRecordingRzx => _zx != null && _zx.isRecordingRZX;

        /// <summary>
        /// Start recording to <paramref name="path"/>. The machine keeps running from where it is: the
        /// recording opens with a snapshot of the current state, so the result plays back standalone.
        /// </summary>
        public Task<bool> StartRzxRecordingAsync(string path) => InvokeAsync(() => StartRzxRecordingCore(path));

        private bool StartRzxRecordingCore(string path)
        {
            if (_zx == null) { Error?.Invoke("No machine is running."); return false; }
            StopRzxCore();
            try
            {
                using (File.Create(path)) { }
            }
            catch (Exception ex)
            {
                Error?.Invoke("Cannot record to " + Path.GetFileName(path) + ": " + ex.Message);
                return false;
            }
            try
            {
                _zx.StartRecordingRZX(path, OnRzxEvent);
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to start the recording: " + ex.Message);
                try { _zx.rzx?.Close(); } catch { }
                _zx.rzx = null;
                _zx.isRecordingRZX = false;
                return false;
            }
            _rzxRecordingPath = path;
            SetState(_paused ? EmulatorState.Paused : EmulatorState.RecordingRzx);
            return true;
        }

        /// <summary>Close the recording and write it out. Returns the file written, or null on failure.</summary>
        public Task<string> FinishRzxRecordingAsync() => InvokeAsync(FinishRzxRecordingCore);

        private string FinishRzxRecordingCore()
        {
            if (_zx == null || !_zx.isRecordingRZX) return null;
            string path = _rzxRecordingPath;
            try
            {
                _zx.SaveRZX(true); 
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to save the recording: " + ex.Message);
                path = null;
            }
            _zx.rzx = null;
            _rzxRecordingPath = null;
            SetState(RunningState());
            return path;
        }

        /// <summary>Abandon the recording and delete the part-written file.</summary>
        public void DiscardRzxRecording() => Post(() =>
        {
            DiscardRzxRecordingCore();
            SetState(RunningState());
        });

        /// <summary>
        /// End the recording but allow it be resumed later.
        /// </summary>
        public void StopRzxRecording() => Post(StopRzxRecordingCore);

        private void StopRzxRecordingCore()
        {
            if (_zx == null || !_zx.isRecordingRZX) return;
            try
            {
                _zx.SaveRZX(false);
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to stop the recording: " + ex.Message);
            }
            _zx.rzx = null;
            _rzxRecordingPath = null;
            SetState(RunningState());
        }

        /// <summary>Resume a recording that was stopped with <see cref="StopRzxRecording"/>.</summary>
        public Task<bool> ContinueRzxRecordingAsync(string path) => InvokeAsync(() => ContinueRzxRecordingCore(path));

        private bool ContinueRzxRecordingCore(string path)
        {
            if (_zx == null || _zx.isRecordingRZX) return false;

            zx_spectrum before = _zx;
            try
            {
                if (!before.ContinueRZXSession(path, OnRzxEvent))
                {
                    Error?.Invoke("Unable to continue " + Path.GetFileName(path) +
                                  ": it must be an unfinalised recording made by Zero X.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke("Unable to continue the recording: " + ex.Message);
                try { before.rzx?.Close(); } catch { }
                before.rzx = null;
                before.isRecordingRZX = false;
                return false;
            }

            if (!ReferenceEquals(_zx, before))
            {
                _zx.rzx = before.rzx;
                _zx.isRecordingRZX = true;
                before.isRecordingRZX = false;
            }

            _rzxRecordingPath = path;
            SetState(_paused ? EmulatorState.Paused : EmulatorState.RecordingRzx);
            return true;
        }

        private void DiscardRzxRecordingCore()
        {
            if (_zx == null || !_zx.isRecordingRZX) return;
            string path = _rzxRecordingPath;
            RZXFile rzx = _zx.rzx;
            _zx.DiscardRZX();
            try { rzx?.Close(); } catch { }
            _zx.rzx = null;
            _rzxRecordingPath = null;
            if (path != null) { try { File.Delete(path); } catch { } }
        }

        /// <summary>Drop a rollback point at the current frame. Ignored unless recording.</summary>
        public void InsertRzxBookmark() => Post(() =>
        {
            if (_zx != null && _zx.isRecordingRZX) _zx.InsertBookmark();
        });

        /// <summary>
        /// Rewind machine and recording to the last bookmark. The core raises a snapshot event that
        /// <see cref="OnRzxEvent"/> loads, which can rebuild the machine, so the recording is rebound
        /// to whatever machine comes back.
        /// </summary>
        public void RollbackRzx() => Post(RollbackRzxCore);

        private void RollbackRzxCore()
        {
            if (_zx == null || !_zx.isRecordingRZX) return;
            zx_spectrum before = _zx;
            RZXFile rzx = _zx.rzx;
            _zx.RollbackRZX();
            if (!ReferenceEquals(_zx, before) || _zx.rzx == null)
            {
                _zx.rzx = rzx;
                _zx.isRecordingRZX = true;
            }
        }

        private void OnRzxEvent(RZXFileEventArgs args)
        {
            if (args.hasEnded)
            {
                SetState(_paused ? EmulatorState.Paused : EmulatorState.Running);
                return;
            }
            switch (args.blockID)
            {
                case RZX_BlockType.SNAPSHOT:
                {
                    string ext = (args.snapData.extension ?? "").TrimEnd('\0').ToLowerInvariant();
                    byte[] data = args.snapData.data;
                    if (ext == "sna") UseSnapshot(SNAFile.LoadSNA(new MemoryStream(data)));
                    else if (ext == "z80") UseSnapshot(Z80File.LoadZ80(new MemoryStream(data)));
                    else if (ext == "szx")
                    {
                        var szx = new SZXFile();
                        if (szx.LoadSZX(new MemoryStream(data))) UseSzx(szx);
                    }
                    break;
                }
                case RZX_BlockType.RECORD:
                    _zx.cpu.t_states = (int)args.tstates;
                    break;
            }
            if (args.rzxInstance != null)
                _zx.rzx = args.rzxInstance;
        }
    }
}
