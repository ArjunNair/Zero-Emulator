using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Zero.App.Styles;
using Zero.Emulation;
using Zero.Emulation.Settings;

namespace Zero.App.Windows
{
    /// <summary>
    /// Settings that have no natural home in the menus: folders, ROM images, emulation flags, gamepads.
    /// Edits a copy of the settings and applies them on OK; the caller decides what needs a restart.
    /// </summary>
    public partial class OptionsWindow : Window
    {
        private static readonly string[] JoystickNames = { "None", "Kempston", "Sinclair 1", "Sinclair 2", "Cursor" };

        private readonly EmulatorSettings _settings;
        private readonly EmulatorSession _session;

        /// <summary>True after OK when the ROM folder or a ROM file changed (machine must be recreated).</summary>
        public bool RomsChanged { get; private set; }

        /// <summary>True after OK when a different UI theme was chosen (takes effect on restart).</summary>
        public bool ThemeChanged { get; private set; }
        public bool Accepted { get; private set; }

        public OptionsWindow() : this(new EmulatorSettings(), null) { }

        public OptionsWindow(EmulatorSettings settings, EmulatorSession session)
        {
            InitializeComponent();
            _settings = settings;
            _session = session;

            RomsPath.Text = settings.Paths.Roms;
            ProgramsPath.Text = settings.Paths.Programs;
            SavesPath.Text = settings.Paths.Saves;
            ScreenshotsPath.Text = settings.Paths.Screenshots;

            Rom48k.Text = settings.Roms.Rom48k;
            Rom128k.Text = settings.Roms.Rom128k;
            Rom128ke.Text = settings.Roms.Rom128ke;
            RomPlus3.Text = settings.Roms.RomPlus3;
            RomPentagon.Text = settings.Roms.RomPentagon;

            CpuMultiplier.ItemsSource = Enumerable.Range(1, 14).Select(i => i.ToString()).ToArray();
            CpuMultiplier.SelectedIndex = Math.Clamp(settings.Emulation.CpuMultiplier, 1, 14) - 1;
            Use128ke.IsChecked = settings.Emulation.Use128keForSnapshots;
            PauseOnFocusLost.IsChecked = settings.Emulation.PauseOnFocusLost;
            ConfirmOnExit.IsChecked = settings.Emulation.ConfirmOnExit;
            RestoreSession.IsChecked = settings.Emulation.RestorePreviousSessionOnStart;

            Gamepad1.ItemsSource = JoystickNames; Gamepad1.SelectedIndex = Math.Clamp(settings.Input.Gamepad1Emulates, 0, 4);
            Gamepad2.ItemsSource = JoystickNames; Gamepad2.SelectedIndex = Math.Clamp(settings.Input.Gamepad2Emulates, 0, 4);
            KeyJoy.ItemsSource = JoystickNames; KeyJoy.SelectedIndex = Math.Clamp(settings.Input.KeyboardJoystickType, 0, 4);
            KeyJoyEnabled.IsChecked = settings.Input.EnableKeyboardJoystick;
            KempstonPort1F.IsChecked = settings.Input.KempstonUsesPort1F;
            MouseEnabled.IsChecked = settings.Input.EnableKempstonMouse;
            MouseSensitivity.ItemsSource = Enumerable.Range(1, 10).Select(i => i.ToString()).ToArray();
            MouseSensitivity.SelectedIndex = Math.Clamp(settings.Input.MouseSensitivity, 1, 10) - 1;

            ThemeBox.ItemsSource = ThemeCatalog.Names;
            ThemeBox.SelectedItem = ThemeCatalog.Normalise(settings.Render.UiTheme);

            var pads = session?.Gamepads;
            if (pads != null)
            {
                var names = new List<string>();
                for (int i = 0; i < pads.Count; i++) names.Add($"{i + 1}: {pads.GetName(i)}");
                GamepadList.Text = names.Count == 0 ? "No gamepads detected." : "Detected: " + string.Join(", ", names);
            }
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            ThemeDescription.Text = ThemeCatalog.Describe(ThemeBox.SelectedItem as string);
        }

        private async void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            var box = this.FindControl<TextBox>((string)((Button)sender).Tag);
            var options = new FolderPickerOpenOptions { Title = "Choose folder", AllowMultiple = false };
            string current = AppPaths.Resolve(box.Text, "");
            if (Directory.Exists(current)) options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(current);
            IReadOnlyList<IStorageFolder> picked = await StorageProvider.OpenFolderPickerAsync(options);
            string path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path != null) box.Text = path;
        }

        private async void OnBrowseRom(object sender, RoutedEventArgs e)
        {
            var box = this.FindControl<TextBox>((string)((Button)sender).Tag);
            var options = new FilePickerOpenOptions
            {
                Title = "Choose ROM image",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("ROM images") { Patterns = new[] { "*.rom", "*.bin" } }, FilePickerFileTypes.All }
            };
            string romDir = AppPaths.Resolve(RomsPath.Text, "roms");
            if (Directory.Exists(romDir)) options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(romDir);
            IReadOnlyList<IStorageFile> picked = await StorageProvider.OpenFilePickerAsync(options);
            string path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
            if (path == null) return;
            // Keep just the file name when it lives in the ROM folder; otherwise store the full path.
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            box.Text = string.Equals(dir, Path.GetFullPath(romDir).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(path) : path;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            RomsChanged = RomsPath.Text != _settings.Paths.Roms
                          || Rom48k.Text != _settings.Roms.Rom48k || Rom128k.Text != _settings.Roms.Rom128k
                          || Rom128ke.Text != _settings.Roms.Rom128ke || RomPlus3.Text != _settings.Roms.RomPlus3
                          || RomPentagon.Text != _settings.Roms.RomPentagon;

            _settings.Paths.Roms = RomsPath.Text;
            _settings.Paths.Programs = ProgramsPath.Text;
            _settings.Paths.Saves = SavesPath.Text;
            _settings.Paths.Screenshots = ScreenshotsPath.Text;

            _settings.Roms.Rom48k = Rom48k.Text;
            _settings.Roms.Rom128k = Rom128k.Text;
            _settings.Roms.Rom128ke = Rom128ke.Text;
            _settings.Roms.RomPlus3 = RomPlus3.Text;
            _settings.Roms.RomPentagon = RomPentagon.Text;

            _settings.Emulation.CpuMultiplier = Math.Max(0, CpuMultiplier.SelectedIndex) + 1;
            _settings.Emulation.Use128keForSnapshots = Use128ke.IsChecked == true;
            _settings.Emulation.PauseOnFocusLost = PauseOnFocusLost.IsChecked == true;
            _settings.Emulation.ConfirmOnExit = ConfirmOnExit.IsChecked == true;
            _settings.Emulation.RestorePreviousSessionOnStart = RestoreSession.IsChecked == true;

            _settings.Input.Gamepad1Emulates = Math.Max(0, Gamepad1.SelectedIndex);
            _settings.Input.Gamepad2Emulates = Math.Max(0, Gamepad2.SelectedIndex);
            _settings.Input.KeyboardJoystickType = Math.Max(0, KeyJoy.SelectedIndex);
            _settings.Input.EnableKeyboardJoystick = KeyJoyEnabled.IsChecked == true;
            string theme = ThemeCatalog.Normalise(ThemeBox.SelectedItem as string);
            ThemeChanged = theme != ThemeCatalog.Normalise(_settings.Render.UiTheme);
            _settings.Render.UiTheme = theme;

            _settings.Input.KempstonUsesPort1F = KempstonPort1F.IsChecked == true;
            _settings.Input.EnableKempstonMouse = MouseEnabled.IsChecked == true;
            _settings.Input.MouseSensitivity = Math.Max(0, MouseSensitivity.SelectedIndex) + 1;

            Accepted = true;
            Close();
        }

        private async void OnConfigureButtons(object sender, RoutedEventArgs e)
        {
            var dialog = new GamepadWindow(_settings, _session, Math.Max(0, Gamepad1.SelectedIndex == 0 && Gamepad2.SelectedIndex > 0 ? 1 : 0));
            await dialog.ShowDialog(this);
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();
    }
}
