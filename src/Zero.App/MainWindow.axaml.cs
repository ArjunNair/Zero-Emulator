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

            ApplyViewSettings();
            RefreshMenuState();
            RebuildRecentMenu();
            AssignGestures();

            // Keys go to the emulator before any control (menus in particular) can swallow them.
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
            AddHandler(KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _statusTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, __) => UpdateStatusBar());

            Opened += OnOpened;
            Closing += OnClosing;
            Activated += (_, __) => { if (_pausedByFocusLoss) { _pausedByFocusLoss = false; _session.Resume(); } _session.Keyboard.ReleaseAll(); };
            Deactivated += (_, __) =>
            {
                _session.Keyboard.ReleaseAll();
                if (_settings.Emulation.PauseOnFocusLost && !_session.IsPaused) { _pausedByFocusLoss = true; _session.Pause(); }
            };
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnOpened(object sender, EventArgs e)
        {
            _session.Start();
            _statusTimer.Start();
            Display.Focus();
            if (!string.IsNullOrEmpty(_initialFile))
                _session.LoadFile(Path.GetFullPath(_initialFile));
        }

        private void OnClosing(object sender, WindowClosingEventArgs e)
        {
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

        private void OnExit(object sender, RoutedEventArgs e) => Close();

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
            if (MainMenu.IsOpen) return;
            if ((e.KeyModifiers & CommandModifier) != 0 && !IsMac) return; // leave Ctrl+? shortcuts alone on Win/Linux only when unmapped... (Ctrl is symbol shift)
            ForwardKey(e, true);
        }

        private void OnWindowKeyUp(object sender, KeyEventArgs e)
        {
            if (MainMenu.IsOpen) return;
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
            bool cmd = (e.KeyModifiers & CommandModifier) != 0;
            bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            switch (e.Key)
            {
                case Key.F3: _ = OpenFileAsync(); return true;
                case Key.F2: _ = SaveSnapshotAsync(); return true;
                case Key.F5: ToggleTape(); return true;
                case Key.F6: _session.Post(_session.Tape.Rewind); return true;
                case Key.F7: _session.TogglePause(); return true;
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
                    case Key.S: _ = SaveSnapshotAsync(); return true;
                    case Key.R: _session.Reset(shift); return true;
                    case Key.P: _session.TogglePause(); return true;
                    case Key.M: SetMute(!_settings.Audio.Mute); return true;
                    case Key.F: ToggleFullScreen(); return true;
                    case Key.Q: if (IsMac) { Close(); return true; } break;
                }
            }
            return false;
        }

        private void AssignGestures()
        {
            OpenItem.InputGesture = new KeyGesture(Key.O, CommandModifier);
            SaveSnapshotItem.InputGesture = new KeyGesture(Key.S, CommandModifier);
            SaveScreenItem.InputGesture = new KeyGesture(Key.F12);
            ResetItem.InputGesture = new KeyGesture(Key.F9);
            HardResetItem.InputGesture = new KeyGesture(Key.F9, KeyModifiers.Shift);
            PauseItem.InputGesture = new KeyGesture(Key.F7);
            TapePlayItem.InputGesture = new KeyGesture(Key.F5);
            TapeRewindItem.InputGesture = new KeyGesture(Key.F6);
            MuteItem.InputGesture = new KeyGesture(Key.F8);
            FullScreenItem.InputGesture = new KeyGesture(Key.F11);
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

        private void OnOpen(object sender, RoutedEventArgs e) => _ = OpenFileAsync();
        private void OnSaveSnapshot(object sender, RoutedEventArgs e) => _ = SaveSnapshotAsync();
        private void OnSaveScreen(object sender, RoutedEventArgs e) => _ = SaveScreenAsync();

        private void RebuildRecentMenu()
        {
            RecentMenu.Items.Clear();
            foreach (string path in _settings.RecentFiles.Where(File.Exists).Take(EmulatorSettings.MaxRecentFiles))
            {
                var item = new MenuItem { Header = Path.GetFileName(path), Tag = path };
                ToolTip.SetTip(item, path);
                item.Click += (_, __) => _session.LoadFile((string)((MenuItem)_).Tag);
                RecentMenu.Items.Add(item);
            }
            if (RecentMenu.Items.Count == 0)
                RecentMenu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
        }

        // ------------------------------------------------------------------ machine menu

        private void OnSelectMachine(object sender, RoutedEventArgs e)
        {
            if (Enum.TryParse(((MenuItem)sender).Tag as string, out MachineModel model))
                _session.SwitchMachine(model);
            RefreshMenuState();
        }

        private void OnReset(object sender, RoutedEventArgs e) => _session.Reset(false);
        private void OnHardReset(object sender, RoutedEventArgs e) => _session.Reset(true);
        private void OnTogglePause(object sender, RoutedEventArgs e) { _session.TogglePause(); RefreshMenuState(); }

        private void OnSelectSpeed(object sender, RoutedEventArgs e)
        {
            _session.SetSpeed(int.Parse((string)((MenuItem)sender).Tag));
            RefreshMenuState();
        }

        private void OnToggleLateTimings(object sender, RoutedEventArgs e)
        {
            _settings.Emulation.LateTimings = LateTimingsItem.IsChecked;
            _session.SwitchMachine(_session.Model); // timing model is fixed at construction
        }

        private void OnToggleIssue2(object sender, RoutedEventArgs e)
        {
            _settings.Emulation.UseIssue2Keyboard = Issue2Item.IsChecked;
            _session.ApplySettings();
        }

        // ------------------------------------------------------------------ tape menu

        private async void OnInsertTape(object sender, RoutedEventArgs e)
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

        private void OnEjectTape(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Eject);
        private void OnTapePlay(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Play);
        private void OnTapeStop(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Stop);
        private void OnTapeRewind(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Rewind);
        private void OnTapePrev(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.PreviousBlock);
        private void OnTapeNext(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.NextBlock);

        private void ToggleTape()
        {
            _session.Post(() =>
            {
                if (!_session.Tape.IsInserted) return;
                if (_session.Tape.IsPlaying) _session.Tape.Stop(); else _session.Tape.Play();
            });
        }

        private void OnTapeOption(object sender, RoutedEventArgs e)
        {
            _settings.Tape.AutoLoad = TapeAutoLoadItem.IsChecked;
            _settings.Tape.AutoPlay = TapeAutoPlayItem.IsChecked;
            _settings.Tape.EdgeLoad = TapeEdgeLoadItem.IsChecked;
            _settings.Tape.FastLoad = TapeFastLoadItem.IsChecked;
            _settings.Tape.RomTraps = TapeRomTrapsItem.IsChecked;
            _session.ApplySettings();
        }

        private async void OnSetTapSaveTarget(object sender, RoutedEventArgs e)
        {
            string path = await PickSaveFileAsync("TAP file for SAVE", "zero_saved.tap", "tap", "TAP tape");
            if (path != null)
            {
                _session.Post(() => _session.Tape.TapSavePath = path);
                SetStatus("SAVE output goes to " + Path.GetFileName(path));
            }
        }

        // ------------------------------------------------------------------ sound menu

        private void OnToggleMute(object sender, RoutedEventArgs e) => SetMute(MuteItem.IsChecked);

        private void SetMute(bool mute)
        {
            _session.SetMute(mute);
            RefreshMenuState();
        }

        private void OnSelectVolume(object sender, RoutedEventArgs e) => _session.SetVolume(int.Parse((string)((MenuItem)sender).Tag));

        private void OnToggleAy48k(object sender, RoutedEventArgs e)
        {
            _settings.Audio.EnableAYFor48K = Ay48kItem.IsChecked;
            _session.ApplySettings();
        }

        private void OnSelectStereo(object sender, RoutedEventArgs e)
        {
            _settings.Audio.StereoSoundMode = int.Parse((string)((MenuItem)sender).Tag);
            _session.ApplySettings();
            RefreshMenuState();
        }

        // ------------------------------------------------------------------ view menu

        private void OnSelectScale(object sender, RoutedEventArgs e)
        {
            _settings.Render.WindowScale = int.Parse((string)((MenuItem)sender).Tag);
            if (WindowState == WindowState.FullScreen) return;
            ApplyWindowScale();
        }

        private void ApplyWindowScale()
        {
            int scale = Math.Clamp(_settings.Render.WindowScale, 1, 6);
            PixelSize frame = Display.FrameSize;
            int crop = Display.BorderCrop;
            double w = (frame.Width - 2 * crop) * scale;
            double h = (frame.Height - 2 * crop) * scale;
            double chrome = Bounds.Height - Display.Bounds.Height;
            if (chrome <= 0 || double.IsNaN(chrome)) chrome = 56;
            Width = w;
            Height = h + chrome;
        }

        private void OnToggleFullScreen(object sender, RoutedEventArgs e) => ToggleFullScreen();

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

        private void OnToggleSmoothing(object sender, RoutedEventArgs e)
        {
            _settings.Render.PixelSmoothing = SmoothingItem.IsChecked;
            Display.Smooth = _settings.Render.PixelSmoothing;
        }

        private void OnToggleIntegerScaling(object sender, RoutedEventArgs e)
        {
            Display.IntegerScaling = IntegerScalingItem.IsChecked;
        }

        private void OnSelectBorder(object sender, RoutedEventArgs e)
        {
            _settings.Render.BorderCrop = int.Parse((string)((MenuItem)sender).Tag);
            Display.BorderCrop = _settings.Render.BorderCrop;
            RefreshMenuState();
        }

        private void OnSelectPalette(object sender, RoutedEventArgs e)
        {
            _session.SetPalette((string)((MenuItem)sender).Tag);
            RefreshMenuState();
        }

        private void ApplyViewSettings()
        {
            Display.Smooth = _settings.Render.PixelSmoothing;
            Display.BorderCrop = _settings.Render.BorderCrop;
            Display.KeepAspectRatio = _settings.Render.MaintainAspectRatio;
            int scale = Math.Clamp(_settings.Render.WindowScale, 1, 6);
            Width = (352 - 2 * _settings.Render.BorderCrop) * scale;
            Height = (296 - 2 * _settings.Render.BorderCrop) * scale + 56;
            if (_settings.Render.FullScreen) WindowState = WindowState.FullScreen;
        }

        // ------------------------------------------------------------------ input menu

        private void OnToggleKeyJoy(object sender, RoutedEventArgs e)
        {
            _settings.Input.EnableKeyboardJoystick = KeyJoyItem.IsChecked;
            _session.ApplySettings();
        }

        private void OnSelectKeyJoyType(object sender, RoutedEventArgs e)
        {
            _settings.Input.KeyboardJoystickType = int.Parse((string)((MenuItem)sender).Tag);
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void OnSelectPad1Type(object sender, RoutedEventArgs e)
        {
            _settings.Input.Gamepad1Emulates = int.Parse((string)((MenuItem)sender).Tag);
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void OnToggleKempstonPort(object sender, RoutedEventArgs e)
        {
            _settings.Input.KempstonUsesPort1F = KempstonPortItem.IsChecked;
            _session.ApplySettings();
        }

        private void OnTogglePauseOnFocus(object sender, RoutedEventArgs e) => _settings.Emulation.PauseOnFocusLost = PauseOnFocusItem.IsChecked;

        // ------------------------------------------------------------------ help

        private void OnKeyboardHelp(object sender, RoutedEventArgs e) => _ = MessageDialog.ShowAsync(this, "Keyboard",
            "Shift = Caps Shift, Ctrl = Symbol Shift.\n" +
            "Type LOAD \"\": press J, then Ctrl+P twice. Shift+Ctrl = Extended mode.\n" +
            "PC punctuation keys (, . ; \" - = etc.) type the matching Spectrum symbol directly.\n\n" +
            "F3 Open   F2 Save snapshot   F12 Save screen\n" +
            "F5 Tape play/stop   F6 Rewind   F7 Pause   F8 Mute\n" +
            "F9 Reset   Shift+F9 Hard reset   F11 Full screen\n" +
            (IsMac ? "Cmd+O / Cmd+S / Cmd+R / Cmd+P / Cmd+M / Cmd+F do the same." : ""));

        private void OnAbout(object sender, RoutedEventArgs e) => _ = MessageDialog.ShowAsync(this, "About Zero",
            "Zero — a ZX Spectrum emulator\nCopyright © 2009-2026 Arjun Nair\n\n" +
            "Cross-platform build: .NET " + Environment.Version + ", Avalonia UI, SDL3 audio & gamepads.\n" +
            "Emulates the 48K, 128K, 128Ke, +3 (no disk) and Pentagon 128K.");

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

        private void RefreshMenuState()
        {
            MachineModel model = _session.Model;
            Machine48k.IsChecked = model == MachineModel._48k;
            Machine128k.IsChecked = model == MachineModel._128k;
            Machine128ke.IsChecked = model == MachineModel._128ke;
            MachinePlus3.IsChecked = model == MachineModel._plus3;
            MachinePentagon.IsChecked = model == MachineModel._pentagon;
            StatusMachine.Text = MachineFactory.DisplayName(model) + (_session.State == EmulatorState.PlayingRzx ? "  ▶ RZX" : "");

            PauseItem.IsChecked = _session.IsPaused;
            int speed = _settings.Emulation.EmulationSpeed;
            Speed1.IsChecked = speed == 1; Speed2.IsChecked = speed == 2; Speed4.IsChecked = speed == 4; Speed10.IsChecked = speed == 10;
            LateTimingsItem.IsChecked = _settings.Emulation.LateTimings;
            Issue2Item.IsChecked = _settings.Emulation.UseIssue2Keyboard;

            TapeAutoLoadItem.IsChecked = _settings.Tape.AutoLoad;
            TapeAutoPlayItem.IsChecked = _settings.Tape.AutoPlay;
            TapeEdgeLoadItem.IsChecked = _settings.Tape.EdgeLoad;
            TapeFastLoadItem.IsChecked = _settings.Tape.FastLoad;
            TapeRomTrapsItem.IsChecked = _settings.Tape.RomTraps;

            MuteItem.IsChecked = _settings.Audio.Mute;
            Ay48kItem.IsChecked = _settings.Audio.EnableAYFor48K;
            StereoMono.IsChecked = _settings.Audio.StereoSoundMode == 0;
            StereoAcb.IsChecked = _settings.Audio.StereoSoundMode == 1;
            StereoAbc.IsChecked = _settings.Audio.StereoSoundMode == 2;

            FullScreenItem.IsChecked = WindowState == WindowState.FullScreen;
            SmoothingItem.IsChecked = _settings.Render.PixelSmoothing;
            IntegerScalingItem.IsChecked = Display.IntegerScaling;
            BorderFull.IsChecked = _settings.Render.BorderCrop == 0;
            BorderMedium.IsChecked = _settings.Render.BorderCrop == 24;
            BorderNone.IsChecked = _settings.Render.BorderCrop >= 48;
            PaletteNormal.IsChecked = _settings.Render.Palette == "Normal";
            PaletteGray.IsChecked = _settings.Render.Palette == "Grayscale";
            PaletteUlaPlus.IsChecked = _settings.Render.Palette == "ULA Plus";

            KeyJoyItem.IsChecked = _settings.Input.EnableKeyboardJoystick;
            int kj = _settings.Input.KeyboardJoystickType;
            KeyJoyKempston.IsChecked = kj == 1; KeyJoySinclair1.IsChecked = kj == 2; KeyJoySinclair2.IsChecked = kj == 3; KeyJoyCursor.IsChecked = kj == 4;
            int p1 = _settings.Input.Gamepad1Emulates;
            Pad1None.IsChecked = p1 == 0; Pad1Kempston.IsChecked = p1 == 1; Pad1Sinclair1.IsChecked = p1 == 2; Pad1Sinclair2.IsChecked = p1 == 3; Pad1Cursor.IsChecked = p1 == 4;
            KempstonPortItem.IsChecked = _settings.Input.KempstonUsesPort1F;
            PauseOnFocusItem.IsChecked = _settings.Emulation.PauseOnFocusLost;
        }
    }
}
