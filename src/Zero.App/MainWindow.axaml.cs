using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SpeccyCommon;
using Zero.App.Dialogs;
using Zero.App.Input;
using Zero.App.Windows;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Machines;
using Zero.Emulation.Settings;
using CrtSettings = Zero.Emulation.Settings.CrtSettings;
using Zero.Emulation.Tape;
using Zero.Sdl;

namespace Zero.App
{
    public partial class MainWindow : Window
    {
        private readonly EmulatorSettings _settings;
        private readonly EmulatorSession _session;
        private readonly DispatcherTimer _statusTimer;
        private readonly string _initialFile;
        private int _framePending;
        private long _lastFrameCount;
        private DateTime _lastFpsSample = DateTime.UtcNow;
        private bool _pausedByFocusLoss;
        private bool _audioFallback;
        private WindowState _stateBeforeFullScreen = WindowState.Normal;
        private TapeDeckWindow _tapeDeckWindow;
        private bool _mouseCaptured;
        private Point _lastPointer;
        private double _mouseRemainderX, _mouseRemainderY;

        /// <summary>Test hook: where settings come from (defaults to the user config file).</summary>
        internal static Func<EmulatorSettings> SettingsLoader = () => EmulatorSettings.Load();
        /// <summary>Where SaveSettings writes. Tests point this away from the real config file.</summary>
        internal static Action<EmulatorSettings> SettingsSaver = s => s.Save();
        internal EmulatorSession Session => _session;
        internal bool AudioFallback => _audioFallback;

        private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static KeyModifiers CommandModifier => IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

        public MainWindow() : this(null) { }
        
        private bool HasCommandLineFlag(string flag, ref string[] args) => args?.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase)) ?? false;

        // The settings as loaded, and again once command-line flags are applied. SaveSettings puts the
        // first back wherever a flag changed something and nothing has changed it since: flags last one session.
        private JsonObject _beforeFlags, _afterFlags;

        public MainWindow(string[] args)
        {
            InitializeComponent();
            // The menu bar needs 400 px when it is drawn in the window. On macOS it lives in the system
            // bar, so let a 1x window shrink to the screen itself rather than letterbox it.
            if (IsMac) MinWidth = 256;
            _initialFile = args?.FirstOrDefault(a => !a.StartsWith("-", StringComparison.Ordinal));

            _settings = SettingsLoader();
            _session = new EmulatorSession(_settings)
            {
                AudioFactory = CreateAudioOutput,
                Gamepads = new SdlGamepadSource(),
                ChooseArchiveEntry = ChooseArchiveEntryBlocking
            };
            _beforeFlags = _settings.ToJson();
            ApplyCommandLine(args);
            _afterFlags = _settings.ToJson();

            _session.FrameReady += OnFrameReady;
            _session.Error += msg => Dispatcher.UIThread.Post(() => ShowError(msg));
            _session.MachineChanged += _ => Dispatcher.UIThread.Post(RefreshMenuState);
            _session.StateChanged += _ => Dispatcher.UIThread.Post(RefreshMenuState);
            _session.FileLoaded += path => Dispatcher.UIThread.Post(() => { RebuildRecentMenu(); SetStatus("Loaded " + Path.GetFileName(path)); });
            _session.Tape.Changed += () => Dispatcher.UIThread.Post(RefreshTapeStatus);
            _session.DisksChanged += () => Dispatcher.UIThread.Post(RefreshMenuState);
            _session.Tape.BlockSaved += path => Dispatcher.UIThread.Post(() => { SetStatus("Saved block to " + Path.GetFileName(path)); RefreshDeck(); });

            NativeMenu.SetMenu(this, BuildMenu());
            if (IsMac && Application.Current != null) NativeMenu.SetMenu(Application.Current, BuildAppMenu());
            WireDeck();
            // The deck's display draws its lettering from the Spectrum's own character set, in the 48K ROM.
            Controls.VfdText.LoadCharacterSet(Path.Combine(AppPaths.Resolve(_settings.Paths.Roms, "roms"), _settings.Roms.Rom48k));
            ApplyDeckFinish();
            ApplyCrtSettings();
            ApplyViewSettings();
            RefreshMenuState();
            RebuildRecentMenu();

            // Keys go to the emulator before any control (menus in particular) can swallow them.
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

            Display.PointerPressed += OnDisplayPointerPressed;
            Display.PointerReleased += OnDisplayPointerReleased;
            Display.PointerMoved += OnDisplayPointerMoved;

            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, __) => UpdateStatusBar());

            Opened += OnOpened;
            Closing += OnClosing;
            Activated += (_, __) => { if (_pausedByFocusLoss) { _pausedByFocusLoss = false; _session.Resume(); } _session.Keyboard.ReleaseAll(); };
            Deactivated += (_, __) =>
            {
                ReleaseMouse();
                _session.Keyboard.ReleaseAll();
                if (_settings.Emulation.PauseOnFocusLost && !_session.IsPaused) { _pausedByFocusLoss = true; _session.Pause(); }
            };
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Apply command-line flags for this session. Each one simply sets the setting it
        /// controls; SaveSettings works out afterwards which ones to keep out of the saved file.</summary>
        private void ApplyCommandLine(string[] args)
        {
            if (HasCommandLineFlag("-fs", ref args))
                _settings.Render.FullScreen = true;

            if (HasCommandLineFlag("-cs", ref args))
            {
                _settings.Render.Crt.Scanlines = true;
                _settings.Render.Crt.Enabled = true;
            }             
            
            if (HasCommandLineFlag("-cv", ref args))
            {
                _settings.Render.Crt.Vignette = true;
                _settings.Render.Crt.Enabled = true;
            }

            var pixelFilter = args?.FirstOrDefault(a => a.StartsWith("-pf:", StringComparison.OrdinalIgnoreCase));
            if (pixelFilter != null)
            {
                var mode = pixelFilter.Split(':')[1];
                _settings.Render.PixelSmoothing = true;
                _settings.Render.SharpPixelEdges = mode.Equals("sharp", StringComparison.OrdinalIgnoreCase);
            }

            if (HasCommandLineFlag("-lt", ref args))
            {
                _settings.Emulation.LateTimings = true;
            }

            var palette = args?.FirstOrDefault(a => a.StartsWith("-pl:", StringComparison.OrdinalIgnoreCase));
            if (palette != null)
            {
                var mode = palette.Split(':')[1];
                _settings.Render.Palette = mode switch {
                    "ula+" => "Ula Plus",
                    "ulaplus" => "Ula Plus",
                    "grayscale" => "Grayscale",
                    "greyscale" => "Grayscale",
                    _ => "Normal"
                };
            }

            var windowScale = args?.FirstOrDefault(a => a.StartsWith("-ws:", StringComparison.OrdinalIgnoreCase));
            if (windowScale != null)
            {
                try {
                    var scale = windowScale.Split(':')[1];
                    _settings.Render.WindowScale = Int32.Parse(scale);
                }
                catch (Exception) {
                    Console.WriteLine("Invalid parameter in option: " + windowScale);
                }
            }
        }

        /// <summary>
        /// Put back the pre-launch value wherever a flag changed a setting and nothing has changed it
        /// since. A three-way compare per leaf: before the flags, after them, and now. A setting you
        /// change by hand during the session no longer matches the flag value, so it is kept.
        /// </summary>
        internal static void StripFlags(JsonObject before, JsonObject after, JsonObject current)
        {
            foreach (var (key, flagValue) in after)
            {
                JsonNode original = before[key], now = current[key];
                if (flagValue is JsonObject f && original is JsonObject o && now is JsonObject n)
                    StripFlags(o, f, n);                     // recurse into Render, Crt, ...
                else if (!JsonNode.DeepEquals(original, flagValue) && JsonNode.DeepEquals(now, flagValue))
                    current[key] = original?.DeepClone();
            }
        }

        /// <summary>Save the session's settings minus any untouched command-line flags. The live
        /// settings are left alone, so flags stay in force for the rest of the session.</summary>
        internal void SaveSettings()
        {
            JsonObject current = _settings.ToJson();
            StripFlags(_beforeFlags, _afterFlags, current);
            try { SettingsSaver(EmulatorSettings.FromJson(current)); } catch { /* not fatal */ }
        }

        private void OnOpened(object sender, EventArgs e)
        {
            // Chrome height depends on whether the menu bar is in the window (Windows/Linux) or in the
            // system bar (macOS); fit the window once layout has measured it.
            Dispatcher.UIThread.Post(() => { if (WindowState == WindowState.Normal) ApplyWindowScale(); }, DispatcherPriority.Loaded);
            _session.Start();
            _statusTimer.Start();
            Display.Focus();
            if (!string.IsNullOrEmpty(_initialFile))
                _session.LoadFile(Path.GetFullPath(_initialFile));
            else if (_settings.Emulation.RestorePreviousSessionOnStart && File.Exists(SessionSnapshotPath))
                _session.LoadFile(SessionSnapshotPath);
        }

        private async void OnClosing(object sender, WindowClosingEventArgs e)
        {
            if (!_closeConfirmed && _settings.Emulation.ConfirmOnExit && _session.IsRunning)
            {
                e.Cancel = true;
                bool wasPaused = _session.IsPaused;
                _session.Pause();
                bool yes = await MessageDialog.ConfirmAsync(this, "Exit Zero X", "Quit the emulator?", "Quit", "Cancel");
                if (!yes) { if (!wasPaused) _session.Resume(); return; }
                _closeConfirmed = true;
                Close();
                return;
            }
            if (_settings.Emulation.RestorePreviousSessionOnStart && _session.IsRunning)
            {
                try
                {
                    AppPaths.EnsureDirectory(AppPaths.ConfigDirectory);
                    await _session.InvokeAsync(() => _session.Machine?.SaveSZX(SessionSnapshotPath));
                }
                catch { /* best effort */ }
            }
            Zero.Emulation.Trace.Log("MainWindow.OnClosing");
            _statusTimer.Stop();
            _session.FrameReady -= OnFrameReady;
            _session.Dispose();
            Zero.Emulation.Trace.Log("MainWindow.OnClosing: session disposed");
            // SDL is left initialised on purpose: SDL_Quit is not needed at process exit and can block
            // when the audio device is torn down from a thread other than the one that opened it.
            SaveSettings();
            Zero.Emulation.Trace.Log("MainWindow.OnClosing: done");
        }

        private bool _closeConfirmed;

        private static string SessionSnapshotPath => Path.Combine(AppPaths.ConfigDirectory, "last_session.szx");

        private Speccy.IAudioOutput CreateAudioOutput()
        {
            try
            {
                var sdl = new SdlAudioOutput();
                Zero.Emulation.Trace.Log("SDL audio opened");
                return sdl;
            }
            catch (Exception ex)
            {
                _audioFallback = true;
                Dispatcher.UIThread.Post(() => SetStatus("No audio device (" + ex.Message + "); running silent."));
                return new TimerPacedAudioOutput();
            }
        }

        // ------------------------------------------------------------------ frames

        private void OnFrameReady()
        {
            if (Interlocked.Exchange(ref _framePending, 1) != 0) return;
            Dispatcher.UIThread.Post(PresentLatestFrame, DispatcherPriority.Render);
        }

        private void PresentLatestFrame()
        {
            Interlocked.Exchange(ref _framePending, 0);
            VideoFrame frame = _session.Frames.TryAcquireLatest();
            if (frame != null) Display.Present(frame);
        }

        // ------------------------------------------------------------------ keyboard

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (HandleShortcut(e)) { e.Handled = true; return; }
            // Nothing is swallowed here. On Windows and Linux Control is Symbol Shift and nothing
            // else, so Ctrl+P has to reach the Spectrum as a quote rather than being eaten.
            ForwardKey(e, true);
        }

        private void OnWindowKeyUp(object sender, KeyEventArgs e)
        {
            ForwardKey(e, false);
        }

        private void ForwardKey(KeyEventArgs e, bool down)
        {
            bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if (!KeyMap.TryMap(e.PhysicalKey, shift, out keyCode key, out bool symbolShift, out bool dropCapsShift))
                return;

            if (key == keyCode.SHIFT || key == keyCode.CTRL || key == keyCode.ALT)
            {
                _session.Keyboard.SetKey(key, down);
                e.Handled = true;
                return;
            }

            if (down)
            {
                if (symbolShift) _session.Keyboard.SetKey(keyCode.CTRL, true);
                if (dropCapsShift) _session.Keyboard.SetKey(keyCode.SHIFT, false);
                _session.Keyboard.SetKey(key, true);
            }
            else
            {
                _session.Keyboard.SetKey(key, false);
                if (symbolShift && (e.KeyModifiers & KeyModifiers.Control) == 0)
                    _session.Keyboard.SetKey(keyCode.CTRL, false);
                if (dropCapsShift && shift)
                    _session.Keyboard.SetKey(keyCode.SHIFT, true);
            }
            e.Handled = true;
        }

        private bool HandleShortcut(KeyEventArgs e) => HandleShortcut(e, IsMac);

        /// <summary>
        /// True when the press was a shortcut and the Spectrum should not see it. The command-key
        /// half is macOS only: elsewhere the command modifier is Control, which the Spectrum needs
        /// as Symbol Shift, and Ctrl+P, Ctrl+O, Ctrl+M and friends all type useful characters. The
        /// function keys cover the same ground on every platform.
        /// </summary>
        internal bool HandleShortcut(KeyEventArgs e, bool isMac)
        {
            if (e.Key == Key.Escape && _mouseCaptured) { ReleaseMouse(); return true; }
            bool cmd = isMac && (e.KeyModifiers & KeyModifiers.Meta) != 0;
            bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            switch (e.Key)
            {
                case Key.F3: _ = OpenFileAsync(); return true;
                case Key.F2: _ = SaveSnapshotAsync(); return true;
                case Key.F1: ShowKeyboardWindow(); return true;
                case Key.F4: ShowTapeDeck(); return true;
                case Key.F5: ToggleTape(); return true;
                case Key.F6: _session.Post(_session.Tape.Rewind); return true;
                case Key.F7: TogglePause(); return true;
                case Key.F8: SetMute(!_settings.Audio.Mute); return true;
                case Key.F9: _session.Reset(shift); return true;
                case Key.F10: if (shift) RollbackRzx(); else InsertRzxBookmark(); return true;
                case Key.F11: ToggleFullScreen(); return true;
                case Key.F12: _ = SaveScreenAsync(); return true;
                case Key.Pause: TogglePause(); return true;
            }
            if (cmd)
            {
                switch (e.Key)
                {
                    case Key.O: _ = OpenFileAsync(); return true;
                    case Key.OemComma: _ = ShowOptionsAsync(); return true;
                    case Key.S: _ = SaveSnapshotAsync(); return true;
                    case Key.R: _session.Reset(shift); return true;
                    case Key.P: TogglePause(); return true;
                    case Key.M: SetMute(!_settings.Audio.Mute); return true;
                    case Key.F: ToggleFullScreen(); return true;
                    case Key.Q: Close(); return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ Kempston mouse

        internal bool MouseCaptured => _mouseCaptured;

        private void OnDisplayPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (!_settings.Input.EnableKempstonMouse) { Display.Focus(); return; }
            PointerPoint p = e.GetCurrentPoint(Display);
            if (!_mouseCaptured)
            {
                CaptureMouse(p.Position);
                e.Handled = true;
                return;
            }
            if (p.Properties.IsLeftButtonPressed) _session.Mouse.SetButton(Emulation.Input.MouseState.LeftButton, true);
            if (p.Properties.IsRightButtonPressed) _session.Mouse.SetButton(Emulation.Input.MouseState.RightButton, true);
            e.Handled = true;
        }

        private void OnDisplayPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_mouseCaptured) return;
            if (e.InitialPressMouseButton == MouseButton.Left) _session.Mouse.SetButton(Emulation.Input.MouseState.LeftButton, false);
            if (e.InitialPressMouseButton == MouseButton.Right) _session.Mouse.SetButton(Emulation.Input.MouseState.RightButton, false);
            e.Handled = true;
        }

        private void OnDisplayPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_mouseCaptured) return;
            Point pos = e.GetPosition(Display);
            double scale = Math.Max(0.01, Display.Scale) * (3.0 / Math.Max(1, _settings.Input.MouseSensitivity));
            _mouseRemainderX += (pos.X - _lastPointer.X) / scale;
            _mouseRemainderY += (pos.Y - _lastPointer.Y) / scale;
            _lastPointer = pos;
            int dx = (int)_mouseRemainderX, dy = (int)_mouseRemainderY;
            _mouseRemainderX -= dx; _mouseRemainderY -= dy;
            if (dx != 0 || dy != 0) _session.Mouse.Move(dx, dy);
        }

        private void CaptureMouse(Point at)
        {
            _mouseCaptured = true;
            _lastPointer = at;
            _mouseRemainderX = _mouseRemainderY = 0;
            Display.Cursor = new Cursor(StandardCursorType.None);
            SetStatus("Mouse captured. Press Esc to release.");
        }

        private void ReleaseMouse()
        {
            if (!_mouseCaptured) return;
            _mouseCaptured = false;
            Display.Cursor = Cursor.Default;
            _session.Mouse.SetButton(Emulation.Input.MouseState.LeftButton | Emulation.Input.MouseState.RightButton, false);
            SetStatus("Mouse released.");
        }

        // ------------------------------------------------------------------ drag & drop

        private void OnDrop(object sender, DragEventArgs e)
        {
            string path = null;
            try
            {
                var file = e.DataTransfer?.TryGetFile();
                path = file?.TryGetLocalPath();
            }
            catch { }
            if (path != null) _session.LoadFile(path);
        }

        // ------------------------------------------------------------------ files

        private static FilePickerFileType[] OpenFilters => new[]
        {
            new FilePickerFileType("All supported files") { Patterns = EmulatorSession.AllOpenableExtensions.Select(x => "*" + x).ToArray() },
            new FilePickerFileType("Snapshots") { Patterns = new[] { "*.szx", "*.sna", "*.z80" } },
            new FilePickerFileType("Tapes") { Patterns = new[] { "*.pzx", "*.tap", "*.tzx", "*.csw" } },
            new FilePickerFileType("Disks (+3, TR-DOS)") { Patterns = new[] { "*.dsk", "*.trd", "*.scl" } },
            new FilePickerFileType("Action Replay recordings") { Patterns = new[] { "*.rzx" } },
            new FilePickerFileType("Screens") { Patterns = new[] { "*.scr" } },
            new FilePickerFileType("ZIP archives") { Patterns = new[] { "*.zip" } },
            FilePickerFileTypes.All
        };

        private async Task<string> PickOpenFileAsync(string title, FilePickerFileType[] filters)
        {
            var options = new FilePickerOpenOptions { Title = title, AllowMultiple = false, FileTypeFilter = filters };
            string dir = AppPaths.Resolve(_settings.Paths.Programs, "programs");
            if (Directory.Exists(dir))
                options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(dir);
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(options);
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        private async Task<string> PickSaveFileAsync(string title, string suggested, string ext, string typeName)
        {
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggested,
                DefaultExtension = ext,
                FileTypeChoices = new[] { new FilePickerFileType(typeName) { Patterns = new[] { "*." + ext } } }
            });
            return file?.TryGetLocalPath();
        }

        private async Task OpenFileAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickOpenFileAsync("Open", OpenFilters);
            if (!wasPaused) _session.Resume();
            if (path != null) _session.LoadFile(path);
            Display.Focus();
        }

        private async Task SaveSnapshotAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            IStorageFile file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Snapshot",
                SuggestedFileName = "snapshot.szx",
                DefaultExtension = "szx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("SZX snapshot") { Patterns = new[] { "*.szx" } },
                    new FilePickerFileType("SNA snapshot") { Patterns = new[] { "*.sna" } }
                }
            });
            string path = file?.TryGetLocalPath();
            if (path != null) { _session.SaveSnapshot(path); SetStatus("Saved " + Path.GetFileName(path)); }
            if (!wasPaused) _session.Resume();
            Display.Focus();
        }

        // ------------------------------------------------------------------ RZX recording

        private async Task StartRzxRecordingAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickSaveFileAsync("Record RZX", "recording.rzx", "rzx", "Action Replay recording");
            if (!wasPaused) _session.Resume();
            if (path != null && await _session.StartRzxRecordingAsync(path))
                SetStatus("Recording to " + Path.GetFileName(path) + " — F10 bookmarks, Shift+F10 rolls back");
            RefreshMenuState();
            Display.Focus();
        }

        private async Task StopRzxRecordingAsync()
        {
            if (!_session.IsRecordingRzx) return;
            bool yes = await MessageDialog.ConfirmAsync(this, "Stop Recording", "Stop recording the RZX?", "Stop", "Cancel");
            if (!yes) return;
            _session.StopRzxRecording();
        }

        private async Task ContinueRzxRecordingAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickOpenFileAsync("Continue RZX Recording", new[]
            {
                new FilePickerFileType("Action Replay recordings") { Patterns = new[] { "*.rzx" } },
                FilePickerFileTypes.All
            });
            if (!wasPaused) _session.Resume();
            if (path != null && await _session.ContinueRzxRecordingAsync(path))
                SetStatus("Continuing recording to " + Path.GetFileName(path) + " — F10 bookmarks, Shift+F10 rolls back");
            RefreshMenuState();
            Display.Focus();
        }

        private async Task FinishRzxRecordingAsync()
        {
            if (!_session.IsRecordingRzx) return;
            string path = await _session.FinishRzxRecordingAsync();
            if (path != null) SetStatus("Saved " + Path.GetFileName(path));
            RefreshMenuState();
            Display.Focus();
        }

        private void DiscardRzxRecording()
        {
            if (!_session.IsRecordingRzx) return;
            _session.DiscardRzxRecording();
            SetStatus("Recording discarded");
            RefreshMenuState();
        }

        private void InsertRzxBookmark()
        {
            if (!_session.IsRecordingRzx) return;
            _session.InsertRzxBookmark();
            SetStatus("Bookmark inserted");
        }

        private void RollbackRzx()
        {
            if (!_session.IsRecordingRzx) return;
            _session.RollbackRzx();
            SetStatus("Rolled back to the last bookmark");
        }

        private async Task SaveScreenAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickSaveFileAsync("Save Screen", "screen.scr", "scr", "Spectrum screen");
            if (path != null) { _session.SaveSnapshot(path); SetStatus("Saved " + Path.GetFileName(path)); }
            if (!wasPaused) _session.Resume();
            Display.Focus();
        }

        private string ChooseArchiveEntryBlocking(IReadOnlyList<string> entries)
        {
            // Runs on the emulation thread while a file is being opened; wait for the UI to answer.
            return Dispatcher.UIThread.InvokeAsync(() => ListChoiceDialog.ShowAsync(this, "Open Archive", "This archive contains several files. Which one do you want to open?", entries)).GetAwaiter().GetResult();
        }

        private async Task RunBinaryDialog(bool save)
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            var dialog = new LoadBinaryWindow(_session, save);
            await dialog.ShowDialog(this);
            if (dialog.BytesTransferred >= 0)
                SetStatus($"{(save ? "Saved" : "Loaded")} {dialog.BytesTransferred} bytes.");
            if (!wasPaused) _session.Resume();
            Display.Focus();
        }

        private KeyboardWindow _keyboardWindow;

        // ------------------------------------------------------------------ machine menu

        // ------------------------------------------------------------------ tape menu

        private async Task InsertTapeAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickOpenFileAsync("Insert Tape", new[]
            {
                new FilePickerFileType("Tapes") { Patterns = new[] { "*.pzx", "*.tap", "*.tzx", "*.csw", "*.zip" } },
                FilePickerFileTypes.All
            });
            if (!wasPaused) _session.Resume();
            if (path != null) _session.LoadFile(path);
            Display.Focus();
        }

        // ------------------------------------------------------------------ disk menu

        private static readonly FilePickerFileType[] DiskFilters =
        {
            new FilePickerFileType("Disks") { Patterns = new[] { "*.dsk", "*.trd", "*.scl", "*.zip" } },
            new FilePickerFileType("+3 disks") { Patterns = new[] { "*.dsk", "*.zip" } },
            new FilePickerFileType("TR-DOS disks (Pentagon)") { Patterns = new[] { "*.trd", "*.scl", "*.zip" } },
            FilePickerFileTypes.All
        };

        private async Task InsertDiskAsync(int unit)
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = await PickOpenFileAsync($"Insert Disk in Drive {(char)('A' + unit)}", DiskFilters);
            if (!wasPaused) _session.Resume();
            if (path != null && await _session.InsertDiskAsync(unit, path))
                SetStatus($"Drive {(char)('A' + unit)}: {Path.GetFileName(path)}");
            Display.Focus();
        }

        /// <summary>
        /// Make a formatted disk and put it in drive A: a TR-DOS disk (TRD) on the Pentagon, a +3
        /// disk (DSK) otherwise. +3 BASIC's FORMAT cannot do this here: the +3's controller, like the
        /// DLL it was ported from, does not format.
        /// </summary>
        private async Task NewBlankDiskAsync()
        {
            bool trdos = _session.Model == MachineModel._pentagon;
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            string path = trdos
                ? await PickSaveFileAsync("New Blank Disk", "blank.trd", "trd", "TR-DOS disk")
                : await PickSaveFileAsync("New Blank Disk", "blank.dsk", "dsk", "+3 disk");
            if (!wasPaused) _session.Resume();
            if (path == null) return;
            try
            {
                if (trdos) Peripherals.TrdImage.WriteBlank(path);
                else Peripherals.DskImage.WriteBlankPlus3(path);
            }
            catch (Exception ex) { ShowError("Could not create the disk: " + ex.Message); return; }
            if (await _session.InsertDiskAsync(0, path))
                SetStatus("Drive A: new disk " + Path.GetFileName(path));
            Display.Focus();
        }

        private void ShowTapeDeck()
        {
            if (_tapeDeckWindow == null)
            {
                _tapeDeckWindow = new TapeDeckWindow(_session, InsertTapeAsync);
                _tapeDeckWindow.Closed += (_, __) => _tapeDeckWindow = null;
                _tapeDeckWindow.Show(this);
            }
            else _tapeDeckWindow.Activate();
        }

        private void ShowKeyboardWindow()
        {
            if (_keyboardWindow == null)
            {
                _keyboardWindow = new KeyboardWindow(_session);
                _keyboardWindow.Closed += (_, __) => _keyboardWindow = null;
                _keyboardWindow.Show(this);
            }
            else _keyboardWindow.Activate();
        }

        private async Task ShowOptionsAsync()
        {
            bool wasPaused = _session.IsPaused;
            _session.Pause();
            var dialog = new OptionsWindow(_settings, _session);
            await dialog.ShowDialog(this);
            if (dialog.Accepted)
            {
                _session.RomDirectory = AppPaths.Resolve(_settings.Paths.Roms, "roms");
                string tapSave = DefaultTapSavePath;
                _session.Post(() => _session.Tape.TapSavePath = tapSave);
                _recordArmed = false;
                if (dialog.RomsChanged) _session.SwitchMachine(_session.Model);
                else _session.ApplySettings();
                RefreshMenuState();
                SaveSettings();
                ApplyDeckFinish();
                ApplyCrtSettings();
                if (dialog.ThemeChanged)
                    await MessageDialog.ShowAsync(this, "Theme",
                        $"Zero will use the {_settings.Render.UiTheme} theme the next time it starts.");
            }
            if (!wasPaused) _session.Resume();
            Display.Focus();
        }

        private async Task SetTapSaveTargetAsync()
        {
            string path = await PickSaveFileAsync("TAP file for SAVE", "zero_saved.tap", "tap", "TAP tape");
            if (path != null)
            {
                _session.Post(() => _session.Tape.TapSavePath = path);
                _recordArmed = true;
                SetStatus("SAVE output goes to " + Path.GetFileName(path));
                RefreshDeck();
            }
        }

        private async Task ConfigureGamepadsAsync()
        {
            var dialog = new GamepadWindow(_settings, _session, 0);
            await dialog.ShowDialog(this);
            if (dialog.Accepted) SaveSettings();
            Display.Focus();
        }

        private void ShowShortcutsHelp() => _ = MessageDialog.ShowAsync(this, "Keyboard Shortcuts",
            "Shift = Caps Shift, Ctrl = Symbol Shift.\n" +
            "Type LOAD \"\": press J, then Ctrl+P twice. Shift+Ctrl = Extended mode.\n" +
            "PC punctuation keys (, . ; \" - = etc.) type the matching Spectrum symbol directly.\n\n" +
            "F1 Spectrum keyboard   F3 Open   F2 Save snapshot   F12 Save screen\n" +
            "F4 Tape deck   F5 Tape play/stop   F6 Rewind   F7 Pause   F8 Mute\n" +
            "F9 Reset   Shift+F9 Hard reset   F11 Full screen\n" +
            "F10 RZX bookmark   Shift+F10 roll back (while recording)\n" +
            (IsMac ? "Cmd+O / Cmd+S / Cmd+R / Cmd+P / Cmd+M / Cmd+F do the same." :
             "Control is Symbol Shift here, so the function keys above are the only shortcuts."));

        private void ShowAbout() => _ = MessageDialog.ShowAsync(this, "About Zero X",
            "Zero X — a ZX Spectrum emulator\nCopyright © 2009-2026 Arjun Nair\n\n" +
            "Cross-platform build: .NET " + Environment.Version + ", Avalonia UI, SDL3 audio & gamepads.\n" +
            "Emulates the 48K, 128K, 128Ke, +3 (with its disk drives) and Pentagon 128K.");

        private void ToggleTape()
        {
            _session.Post(() =>
            {
                if (!_session.Tape.IsInserted) return;
                if (_session.Tape.IsPlaying) _session.Tape.Stop(); else _session.Tape.Play();
            });
        }

        // ------------------------------------------------------------------ sound menu

        // ------------------------------------------------------------------ view menu

        private void ApplyWindowScale()
        {
            int scale = Math.Clamp(_settings.Render.WindowScale, 1, 6);
            Rect source = Display.SourceRect;
            double w = source.Width * scale;
            double h = source.Height * scale;
            // The deck is left out of the measured chrome and added back for the new width, since
            // its height follows the window's width (see DeckHeightFor).
            double deck = DeckShelf.IsVisible ? DeckShelf.Bounds.Height : 0;
            double chrome = ClientSize.Height - Display.Bounds.Height - deck;
            if (chrome < 0 || double.IsNaN(chrome) || Display.Bounds.Height <= 0) chrome = 56;
            double sideChrome = Math.Max(0, ClientSize.Width - Display.Bounds.Width);
            Width = w + sideChrome;
            LimitDeckScaleToScreen(h + chrome);
            Height = h + chrome + DeckHeightFor(Math.Max(Width, MinWidth)); // MinWidth may widen it, and the deck with it
        }

        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = _stateBeforeFullScreen;
            }
            else
            {
                _stateBeforeFullScreen = WindowState;
                WindowState = WindowState.FullScreen;
            }
            RefreshMenuState();
        }

        /// <summary>
        /// Push the CRT settings into the display's shader. Safe to call at any time. Scanlines stand
        /// on their own; the CRT switch governs the rest (curvature, glow, the housing and so on).
        /// </summary>
        /// <summary>
        /// How round the glass's corners can be without cutting into the picture: a rounded corner
        /// eats about 0.29 of its radius in from the corner, so it may be up to about three times the
        /// border that shows. With no border at all it still rounds, trimming a pixel or two off the
        /// picture's very corner, which reads as a tube rather than a cut.
        /// </summary>
        internal static float GlassCornerFor(int borderCrop)
        {
            int shown = 48 - Math.Clamp(borderCrop, 0, 48);                    // border pixels at the top and sides
            double halfHeight = (192 + shown + shown * 56.0 / 48) / 2;          // the visible height, halved
            return (float)Math.Clamp(3 * shown / halfHeight, 0.08, 0.16);
        }

        internal void ApplyCrtSettings()
        {
            CrtSettings crt = _settings.Render.Crt;
            bool on = crt.Enabled;
            float Strength(double value) => on ? (float)Math.Clamp(value, 0, 1) : 0f;
            Display.CrtOptions = new Controls.CrtShaderOptions
            {
                Enabled = on || crt.Scanlines,
                Curvature = Strength(crt.Curvature),
                Glow = Strength(crt.Glow),
                EdgeLight = Strength(crt.EdgeLight),
                Bezel = on ? (float)Math.Clamp(crt.Bezel, 0, 0.15) : 0f,
                Diffuse = Strength(crt.Diffuse),
                Scanlines = crt.Scanlines ? 0.35f : 0f,
                Vignette = on && crt.Vignette ? 0.6f : 0f,
                Corner = GlassCornerFor(_settings.Render.BorderCrop),
                Trinitron = string.Equals(crt.Tube, "Trinitron", StringComparison.OrdinalIgnoreCase)
            };
        }

        private void ApplyViewSettings()
        {
            Display.Smooth = _settings.Render.PixelSmoothing;
            Display.SharpPixels = _settings.Render.SharpPixelEdges;
            Display.BorderCrop = _settings.Render.BorderCrop;
            Display.KeepAspectRatio = _settings.Render.MaintainAspectRatio;
            ApplyDeckVisibility(refit: false);
            int scale = Math.Clamp(_settings.Render.WindowScale, 1, 6);
            Rect source = Display.SourceRect;
            Width = source.Width * scale;
            LimitDeckScaleToScreen(source.Height * scale + 56);
            Height = source.Height * scale + 56 + DeckHeightFor(Math.Max(Width, MinWidth)); // refined from the real layout once the window has opened
            if (_settings.Render.FullScreen) WindowState = WindowState.FullScreen;
        }


        // ------------------------------------------------------------------ status

        private void SetStatus(string text)
        {
            StatusMessage.Text = text;
            Deck.ShowMessage(text);
            RefreshDeck();
        }

        private void ShowError(string message)
        {
            // The dialog reports errors. The status bar keeps a copy, but the deck's display is for
            // short notices and is left alone: an error there just ran off the end of the line.
            StatusMessage.Text = message;
            _ = MessageDialog.ShowAsync(this, "Zero X", message);
        }

        internal void UpdateStatusBar()
        {
            DateTime now = DateTime.UtcNow;
            double seconds = (now - _lastFpsSample).TotalSeconds;

            // Frames the machine ran, not frames the display showed. Two things pull those apart. A
            // frame whose predecessor is still waiting to be drawn is dropped rather than queued --
            // the right thing for a UI, since the triple buffer already holds the newest. And above
            // 1x the machine runs several frames for every one it hands over, painting only the last,
            // so at 4x a count of what was handed over reads a quarter of the truth.
            //
            // Over a second rather than half of one. At fifty frames a second, half a second is
            // twenty-five of them, and a single frame either way is already two frames a second of
            // wobble before anything has actually gone wrong.
            if (seconds >= 1.0)
            {
                long frames = _session.EmulatedFrameCount;
                double fps = (frames - _lastFrameCount) / seconds;
                _lastFrameCount = frames;
                _lastFpsSample = now;
                StatusFps.Text = _session.IsPaused ? "paused" : $"{fps:F0} fps";
                Deck.ShowFps(_session.IsPaused ? "PAUSE" : $"{fps:F0}");   // the deck prints FPS beside the rate
            }
            RefreshTapeStatus();
        }

        private void RefreshTapeStatus()
        {
            TapeDeck tape = _session.Tape;
            if (!tape.IsInserted) StatusTape.Text = "No tape";
            else StatusTape.Text = $"{tape.Title}: {(tape.IsPlaying ? "playing" : "stopped")} block {Math.Min(tape.CurrentBlock + 1, Math.Max(1, tape.Blocks.Count))}/{tape.Blocks.Count}";
            RefreshDeck();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != WindowStateProperty || _settings == null) return;

            WindowState old = change.GetOldValue<WindowState>();
            WindowState now = change.GetNewValue<WindowState>();

            // Where F11 should return to. Never Minimized: restoring a minimised full-screen window
            // reports Minimized -> FullScreen, and recording that would make the next F11 minimise it.
            if (now == WindowState.FullScreen && old is WindowState.Normal or WindowState.Maximized)
                _stateBeforeFullScreen = old;

            // Minimised is a pause, not a choice: quitting from the taskbar shouldn't forget full screen.
            // A -fs launch needs no special case here: SaveSettings strips it unless you changed it.
            if (now != WindowState.Minimized)
                _settings.Render.FullScreen = now == WindowState.FullScreen;

            RefreshMenuState();
        }
    }    
}
