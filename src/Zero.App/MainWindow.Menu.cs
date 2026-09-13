using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SpeccyCommon;
using Zero.App.Input;
using Zero.Emulation;
using Zero.Emulation.Machines;
using Zero.Emulation.Settings;

namespace Zero.App
{
    /// <summary>The menu tree. Items are fields so RefreshMenuState can keep their check marks honest.</summary>
    public partial class MainWindow
    {
        internal NativeMenuItem OpenItem, RecentMenu, SaveSnapshotItem, SaveScreenItem, OptionsItem;
        internal NativeMenuItem Machine48k, Machine128k, Machine128ke, MachinePlus3, MachinePentagon;
        internal NativeMenuItem ResetItem, HardResetItem, PauseItem, Speed1, Speed2, Speed4, Speed10, LateTimingsItem, Issue2Item;
        internal NativeMenuItem TapeDeckItem, TapePlayItem, TapeStopItem, TapeRewindItem;
        internal NativeMenuItem TapeAutoLoadItem, TapeAutoPlayItem, TapeEdgeLoadItem, TapeFastLoadItem, TapeRomTrapsItem;
        internal NativeMenuItem MuteItem, Ay48kItem, StereoMono, StereoAcb, StereoAbc;
        internal NativeMenuItem FullScreenItem, SmoothingItem, IntegerScalingItem, BorderFull, BorderMedium, BorderNone, PaletteNormal, PaletteGray, PaletteUlaPlus;
        internal NativeMenuItem KeyJoyItem, KeyJoyKempston, KeyJoySinclair1, KeyJoySinclair2, KeyJoyCursor;
        internal NativeMenuItem Pad1None, Pad1Kempston, Pad1Sinclair1, Pad1Sinclair2, Pad1Cursor, KempstonPortItem, MouseItem, PauseOnFocusItem;
        internal NativeMenuItem KeyboardItem;

        private static NativeMenuItem Item(string header, Action action, KeyGesture gesture = null, bool checkable = false)
        {
            var item = new NativeMenuItem(header) { Command = new ActionCommand(action) };
            if (gesture != null) item.Gesture = gesture;
            if (checkable) item.ToggleType = MenuItemToggleType.CheckBox;
            return item;
        }

        private static NativeMenuItem Submenu(string header, params NativeMenuItemBase[] items)
        {
            var menu = new NativeMenu();
            foreach (NativeMenuItemBase i in items) menu.Items.Add(i);
            return new NativeMenuItem(header) { Menu = menu };
        }

        private static NativeMenuItemSeparator Sep() => new NativeMenuItemSeparator();

        private static KeyGesture G(Key key, KeyModifiers mods = KeyModifiers.None) => new KeyGesture(key, mods);

        private NativeMenu BuildMenu()
        {
            KeyModifiers cmd = CommandModifier;

            var file = Submenu("File",
                OpenItem = Item("Open…", () => _ = OpenFileAsync(), G(Key.O, cmd)),
                RecentMenu = Submenu("Open Recent"),
                Sep(),
                SaveSnapshotItem = Item("Save Snapshot…", () => _ = SaveSnapshotAsync(), G(Key.S, cmd)),
                SaveScreenItem = Item("Save Screen (.scr)…", () => _ = SaveScreenAsync(), G(Key.F12)),
                Sep(),
                Item("Load Binary…", () => _ = RunBinaryDialog(false)),
                Item("Save Binary…", () => _ = RunBinaryDialog(true)),
                Sep(),
                OptionsItem = Item("Options…", () => _ = ShowOptionsAsync(), G(Key.OemComma, cmd)),
                Sep(),
                Item("Exit", Close, IsMac ? G(Key.Q, cmd) : null));

            var machine = Submenu("Machine",
                Machine48k = Item("ZX Spectrum 48K", () => SelectMachine(MachineModel._48k), checkable: true),
                Machine128k = Item("ZX Spectrum 128K", () => SelectMachine(MachineModel._128k), checkable: true),
                Machine128ke = Item("ZX Spectrum 128Ke", () => SelectMachine(MachineModel._128ke), checkable: true),
                MachinePlus3 = Item("ZX Spectrum +3", () => SelectMachine(MachineModel._plus3), checkable: true),
                MachinePentagon = Item("Pentagon 128K", () => SelectMachine(MachineModel._pentagon), checkable: true),
                Sep(),
                ResetItem = Item("Reset", () => _session.Reset(false), G(Key.F9)),
                HardResetItem = Item("Hard Reset", () => _session.Reset(true), G(Key.F9, KeyModifiers.Shift)),
                Sep(),
                PauseItem = Item("Pause", TogglePause, G(Key.F7), checkable: true),
                Submenu("Speed",
                    Speed1 = Item("1x (normal)", () => SelectSpeed(1), checkable: true),
                    Speed2 = Item("2x", () => SelectSpeed(2), checkable: true),
                    Speed4 = Item("4x", () => SelectSpeed(4), checkable: true),
                    Speed10 = Item("10x", () => SelectSpeed(10), checkable: true)),
                LateTimingsItem = Item("Late Timings", ToggleLateTimings, checkable: true),
                Issue2Item = Item("Issue 2 Keyboard", ToggleIssue2, checkable: true));

            var tape = Submenu("Tape",
                TapeDeckItem = Item("Tape Deck…", ShowTapeDeck, G(Key.F4)),
                Sep(),
                Item("Insert Tape…", () => _ = InsertTapeAsync()),
                Item("Eject", () => _session.Post(_session.Tape.Eject)),
                Sep(),
                TapePlayItem = Item("Play", () => _session.Post(_session.Tape.Play), G(Key.F5)),
                TapeStopItem = Item("Stop", () => _session.Post(_session.Tape.Stop)),
                TapeRewindItem = Item("Rewind", () => _session.Post(_session.Tape.Rewind), G(Key.F6)),
                Item("Previous Block", () => _session.Post(_session.Tape.PreviousBlock)),
                Item("Next Block", () => _session.Post(_session.Tape.NextBlock)),
                Sep(),
                TapeAutoLoadItem = Item("Auto Load (LOAD \"\" on insert)", () => ToggleTapeOption(t => t.AutoLoad = !t.AutoLoad), checkable: true),
                TapeAutoPlayItem = Item("Auto Play/Stop", () => ToggleTapeOption(t => t.AutoPlay = !t.AutoPlay), checkable: true),
                TapeEdgeLoadItem = Item("Edge Loading", () => ToggleTapeOption(t => t.EdgeLoad = !t.EdgeLoad), checkable: true),
                TapeFastLoadItem = Item("Fast Loading", () => ToggleTapeOption(t => t.FastLoad = !t.FastLoad), checkable: true),
                TapeRomTrapsItem = Item("ROM Tape Traps", () => ToggleTapeOption(t => t.RomTraps = !t.RomTraps), checkable: true),
                Sep(),
                Item("Set SAVE Target (.tap)…", () => _ = SetTapSaveTargetAsync()));

            var sound = Submenu("Sound",
                MuteItem = Item("Mute", () => SetMute(!_settings.Audio.Mute), G(Key.F8), checkable: true),
                Submenu("Volume",
                    Item("25%", () => _session.SetVolume(25)),
                    Item("50%", () => _session.SetVolume(50)),
                    Item("75%", () => _session.SetVolume(75)),
                    Item("100%", () => _session.SetVolume(100))),
                Ay48kItem = Item("AY chip on 48K", ToggleAy48k, checkable: true),
                Submenu("Stereo",
                    StereoMono = Item("Mono", () => SelectStereo(0), checkable: true),
                    StereoAcb = Item("ACB", () => SelectStereo(1), checkable: true),
                    StereoAbc = Item("ABC", () => SelectStereo(2), checkable: true)));

            var view = Submenu("View",
                Submenu("Window Size",
                    Item("1x", () => SelectScale(1)), Item("2x", () => SelectScale(2)),
                    Item("3x", () => SelectScale(3)), Item("4x", () => SelectScale(4))),
                FullScreenItem = Item("Full Screen", ToggleFullScreen, G(Key.F11), checkable: true),
                SmoothingItem = Item("Pixel Smoothing", ToggleSmoothing, checkable: true),
                IntegerScalingItem = Item("Integer Scaling", ToggleIntegerScaling, checkable: true),
                Submenu("Border",
                    BorderFull = Item("Full", () => SelectBorder(0), checkable: true),
                    BorderMedium = Item("Medium", () => SelectBorder(24), checkable: true),
                    BorderNone = Item("None", () => SelectBorder(48), checkable: true)),
                Submenu("Palette",
                    PaletteNormal = Item("Normal", () => SelectPalette("Normal"), checkable: true),
                    PaletteGray = Item("Grayscale", () => SelectPalette("Grayscale"), checkable: true),
                    PaletteUlaPlus = Item("ULA Plus", () => SelectPalette("ULA Plus"), checkable: true)));

            var input = Submenu("Input",
                KeyJoyItem = Item("Cursor Keys as Joystick", ToggleKeyJoy, checkable: true),
                Submenu("Cursor Keys Emulate",
                    KeyJoyKempston = Item("Kempston", () => SelectKeyJoyType(1), checkable: true),
                    KeyJoySinclair1 = Item("Sinclair 1", () => SelectKeyJoyType(2), checkable: true),
                    KeyJoySinclair2 = Item("Sinclair 2", () => SelectKeyJoyType(3), checkable: true),
                    KeyJoyCursor = Item("Cursor", () => SelectKeyJoyType(4), checkable: true)),
                Submenu("Gamepad 1 Emulates",
                    Pad1None = Item("None", () => SelectPad1Type(0), checkable: true),
                    Pad1Kempston = Item("Kempston", () => SelectPad1Type(1), checkable: true),
                    Pad1Sinclair1 = Item("Sinclair 1", () => SelectPad1Type(2), checkable: true),
                    Pad1Sinclair2 = Item("Sinclair 2", () => SelectPad1Type(3), checkable: true),
                    Pad1Cursor = Item("Cursor", () => SelectPad1Type(4), checkable: true)),
                Item("Configure Gamepad Buttons…", () => _ = ConfigureGamepadsAsync()),
                KempstonPortItem = Item("Kempston uses port 0x1F", ToggleKempstonPort, checkable: true),
                Sep(),
                MouseItem = Item("Kempston Mouse (click screen to capture, Esc releases)", ToggleMouse, checkable: true),
                Sep(),
                PauseOnFocusItem = Item("Pause when window loses focus", () => { _settings.Emulation.PauseOnFocusLost = !_settings.Emulation.PauseOnFocusLost; RefreshMenuState(); }, checkable: true));

            var help = Submenu("Help",
                KeyboardItem = Item("Spectrum Keyboard…", ShowKeyboardWindow, G(Key.F1)),
                Item("Keyboard Shortcuts", ShowShortcutsHelp),
                Item("About Zero", ShowAbout));

            var root = new NativeMenu();
            foreach (NativeMenuItem m in new[] { file, machine, tape, sound, view, input, help }) root.Items.Add(m);
            return root;
        }

        /// <summary>macOS application menu (About / Options live there by convention). Ignored elsewhere.</summary>
        private NativeMenu BuildAppMenu()
        {
            var menu = new NativeMenu();
            menu.Items.Add(Item("About Zero", ShowAbout));
            menu.Items.Add(Sep());
            menu.Items.Add(Item("Options…", () => _ = ShowOptionsAsync(), G(Key.OemComma, KeyModifiers.Meta)));
            return menu;
        }

        private void RebuildRecentMenu()
        {
            NativeMenu menu = RecentMenu.Menu;
            menu.Items.Clear();
            foreach (string path in _settings.RecentFiles.Where(File.Exists).Take(EmulatorSettings.MaxRecentFiles))
            {
                string p = path;
                menu.Items.Add(new NativeMenuItem(Path.GetFileName(p)) { Command = new ActionCommand(() => _session.LoadFile(p)), ToolTip = p });
            }
            if (menu.Items.Count == 0)
                menu.Items.Add(new NativeMenuItem("(empty)") { IsEnabled = false });
        }

        // ------------------------------------------------------------------ menu commands

        private void SelectMachine(MachineModel model) { _session.SwitchMachine(model); RefreshMenuState(); }
        private void TogglePause() { _session.TogglePause(); RefreshMenuState(); }
        private void SelectSpeed(int speed) { _session.SetSpeed(speed); RefreshMenuState(); }

        private void ToggleLateTimings()
        {
            _settings.Emulation.LateTimings = !_settings.Emulation.LateTimings;
            _session.SwitchMachine(_session.Model); // timing model is fixed at construction
            RefreshMenuState();
        }

        private void ToggleIssue2()
        {
            _settings.Emulation.UseIssue2Keyboard = !_settings.Emulation.UseIssue2Keyboard;
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void ToggleTapeOption(Action<TapeSettings> flip)
        {
            flip(_settings.Tape);
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void SetMute(bool mute) { _session.SetMute(mute); RefreshMenuState(); }

        private void ToggleAy48k()
        {
            _settings.Audio.EnableAYFor48K = !_settings.Audio.EnableAYFor48K;
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void SelectStereo(int mode)
        {
            _settings.Audio.StereoSoundMode = mode;
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void SelectScale(int scale)
        {
            _settings.Render.WindowScale = scale;
            if (WindowState != WindowState.FullScreen) ApplyWindowScale();
        }

        private void ToggleSmoothing()
        {
            _settings.Render.PixelSmoothing = !_settings.Render.PixelSmoothing;
            Display.Smooth = _settings.Render.PixelSmoothing;
            RefreshMenuState();
        }

        private void ToggleIntegerScaling() { Display.IntegerScaling = !Display.IntegerScaling; RefreshMenuState(); }

        private void SelectBorder(int crop)
        {
            _settings.Render.BorderCrop = crop;
            Display.BorderCrop = crop;
            RefreshMenuState();
        }

        private void SelectPalette(string palette) { _session.SetPalette(palette); RefreshMenuState(); }

        private void ToggleKeyJoy()
        {
            _settings.Input.EnableKeyboardJoystick = !_settings.Input.EnableKeyboardJoystick;
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void SelectKeyJoyType(int type) { _settings.Input.KeyboardJoystickType = type; _session.ApplySettings(); RefreshMenuState(); }
        private void SelectPad1Type(int type) { _settings.Input.Gamepad1Emulates = type; _session.ApplySettings(); RefreshMenuState(); }

        private void ToggleKempstonPort()
        {
            _settings.Input.KempstonUsesPort1F = !_settings.Input.KempstonUsesPort1F;
            _session.ApplySettings();
            RefreshMenuState();
        }

        private void ToggleMouse()
        {
            _settings.Input.EnableKempstonMouse = !_settings.Input.EnableKempstonMouse;
            if (!_settings.Input.EnableKempstonMouse) ReleaseMouse();
            _session.ApplySettings();
            RefreshMenuState();
        }

        // ------------------------------------------------------------------ state → check marks

        internal void RefreshMenuState()
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
            MouseItem.IsChecked = _settings.Input.EnableKempstonMouse;
            PauseOnFocusItem.IsChecked = _settings.Emulation.PauseOnFocusLost;
        }
    }
}
