using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        private long _lastPresented;
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
        internal EmulatorSession Session => _session;
        internal bool AudioFallback => _audioFallback;

        private static bool IsMac => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static KeyModifiers CommandModifier => IsMac ? KeyModifiers.Meta : KeyModifiers.Control;

        public MainWindow() : this(null) { }

        public MainWindow(string initialFile)
        {
            InitializeComponent();
            _initialFile = initialFile;

            _settings = SettingsLoader();
            _session = new EmulatorSession(_settings)
            {
                AudioFactory = CreateAudioOutput,
                Gamepads = new SdlGamepadSource(),
                ChooseArchiveEntry = ChooseArchiveEntryBlocking
            };
            _session.FrameReady += OnFrameReady;
            _session.Error += msg => Dispatcher.UIThread.Post(() => ShowError(msg));
            _session.MachineChanged += _ => Dispatcher.UIThread.Post(RefreshMenuState);
            _session.StateChanged += _ => Dispatcher.UIThread.Post(RefreshMenuState);
            _session.FileLoaded += path => Dispatcher.UIThread.Post(() => { RebuildRecentMenu(); SetStatus("Loaded " + Path.GetFileName(path)); });
            _session.Tape.Changed += () => Dispatcher.UIThread.Post(RefreshTapeStatus);
            _session.Tape.BlockSaved += path => Dispatcher.UIThread.Post(() => SetStatus("Saved block to " + Path.GetFileName(path)));

            NativeMenu.SetMenu(this, BuildMenu());
            if (IsMac && Application.Current != null) NativeMenu.SetMenu(Application.Current, BuildAppMenu());
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
                bool yes = await MessageDialog.ConfirmAsync(this, "Exit Zero", "Quit the emulator?", "Quit", "Cancel");
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
            try { _settings.Save(); } catch { /* not fatal */ }
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
            if ((e.KeyModifiers & CommandModifier) != 0 && !IsMac) return; // leave Ctrl+? shortcuts alone on Win/Linux only when unmapped... (Ctrl is symbol shift)
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

        private bool HandleShortcut(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _mouseCaptured) { ReleaseMouse(); return true; }
            bool cmd = (e.KeyModifiers & CommandModifier) != 0;
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
                case Key.F11: ToggleFullScreen(); return true;
                case Key.F12: _ = SaveScreenAsync(); return true;
                case Key.Pause: _session.TogglePause(); return true;
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
                    case Key.Q: if (IsMac) { Close(); return true; } break;
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
                _session.Post(() => _session.Tape.TapSavePath = Path.Combine(AppPaths.Resolve(_settings.Paths.Saves, "saves"), "zero_saved.tap"));
                if (dialog.RomsChanged) _session.SwitchMachine(_session.Model);
                else _session.ApplySettings();
                RefreshMenuState();
                try { _settings.Save(); } catch { }
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
                SetStatus("SAVE output goes to " + Path.GetFileName(path));
            }
        }

        private async Task ConfigureGamepadsAsync()
        {
            var dialog = new GamepadWindow(_settings, _session, 0);
            await dialog.ShowDialog(this);
            if (dialog.Accepted) { try { _settings.Save(); } catch { } }
            Display.Focus();
        }

        private void ShowShortcutsHelp() => _ = MessageDialog.ShowAsync(this, "Keyboard Shortcuts",
            "Shift = Caps Shift, Ctrl = Symbol Shift.\n" +
            "Type LOAD \"\": press J, then Ctrl+P twice. Shift+Ctrl = Extended mode.\n" +
            "PC punctuation keys (, . ; \" - = etc.) type the matching Spectrum symbol directly.\n\n" +
            "F1 Spectrum keyboard   F3 Open   F2 Save snapshot   F12 Save screen\n" +
            "F4 Tape deck   F5 Tape play/stop   F6 Rewind   F7 Pause   F8 Mute\n" +
            "F9 Reset   Shift+F9 Hard reset   F11 Full screen\n" +
            (IsMac ? "Cmd+O / Cmd+S / Cmd+R / Cmd+P / Cmd+M / Cmd+F do the same." : ""));

        private void ShowAbout() => _ = MessageDialog.ShowAsync(this, "About Zero",
            "Zero — a ZX Spectrum emulator\nCopyright © 2009-2026 Arjun Nair\n\n" +
            "Cross-platform build: .NET " + Environment.Version + ", Avalonia UI, SDL3 audio & gamepads.\n" +
            "Emulates the 48K, 128K, 128Ke, +3 (no disk) and Pentagon 128K.");

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
            double chrome = ClientSize.Height - Display.Bounds.Height;
            if (chrome < 0 || double.IsNaN(chrome) || Display.Bounds.Height <= 0) chrome = 56;
            double sideChrome = Math.Max(0, ClientSize.Width - Display.Bounds.Width);
            Width = w + sideChrome;
            Height = h + chrome;
        }

        private void ToggleFullScreen()
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = _stateBeforeFullScreen;
                _settings.Render.FullScreen = false;
            }
            else
            {
                _stateBeforeFullScreen = WindowState;
                WindowState = WindowState.FullScreen;
                _settings.Render.FullScreen = true;
            }
            RefreshMenuState();
        }

        private void ApplyViewSettings()
        {
            Display.Smooth = _settings.Render.PixelSmoothing;
            Display.BorderCrop = _settings.Render.BorderCrop;
            Display.KeepAspectRatio = _settings.Render.MaintainAspectRatio;
            int scale = Math.Clamp(_settings.Render.WindowScale, 1, 6);
            Rect source = Display.SourceRect;
            Width = source.Width * scale;
            Height = source.Height * scale + 56; // refined from the real layout once the window has opened
            if (_settings.Render.FullScreen) WindowState = WindowState.FullScreen;
        }


        // ------------------------------------------------------------------ status

        private void SetStatus(string text) => StatusMessage.Text = text;

        private void ShowError(string message)
        {
            SetStatus(message);
            _ = MessageDialog.ShowAsync(this, "Zero", message);
        }

        private void UpdateStatusBar()
        {
            DateTime now = DateTime.UtcNow;
            double seconds = (now - _lastFpsSample).TotalSeconds;
            if (seconds >= 0.5)
            {
                long presented = Display.FramesPresented;
                double fps = (presented - _lastPresented) / seconds;
                _lastPresented = presented;
                _lastFpsSample = now;
                StatusFps.Text = _session.IsPaused ? "paused" : $"{fps:F0} fps";
            }
            RefreshTapeStatus();
        }

        private void RefreshTapeStatus()
        {
            TapeDeck tape = _session.Tape;
            if (!tape.IsInserted) StatusTape.Text = "No tape";
            else StatusTape.Text = $"{tape.Title}: {(tape.IsPlaying ? "playing" : "stopped")} block {Math.Min(tape.CurrentBlock + 1, Math.Max(1, tape.Blocks.Count))}/{tape.Blocks.Count}";
        }

    }
}
