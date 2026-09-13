using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Zero.Emulation;
using Zero.Emulation.Host;
using Zero.Emulation.Input;
using Zero.Emulation.Settings;

namespace Zero.App.Windows
{
    /// <summary>Edit which action each gamepad button performs. Rows light up while the button is held.</summary>
    public partial class GamepadWindow : Window
    {
        private readonly EmulatorSettings _settings;
        private readonly EmulatorSession _session;
        private readonly GamepadMapping[] _edit = new GamepadMapping[2];
        private readonly ComboBox[] _combos = new ComboBox[GamepadButtons.Count];
        private readonly Border[] _rows = new Border[GamepadButtons.Count];
        private readonly DispatcherTimer _poll;
        private int _pad;
        private bool _loading;

        public bool Accepted { get; private set; }

        public GamepadWindow() : this(new EmulatorSettings(), null, 0) { }

        public GamepadWindow(EmulatorSettings settings, EmulatorSession session, int pad)
        {
            InitializeComponent();
            _settings = settings;
            _session = session;
            for (int i = 0; i < 2; i++)
                _edit[i] = new GamepadMapping { Buttons = new Dictionary<int, string>(settings.Input.BindingsFor(i).Buttons) };

            for (int b = 0; b < GamepadButtons.Count; b++)
            {
                Rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var label = new TextBlock { Text = GamepadButtons.Names[b], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(6, 0) };
                var combo = new ComboBox { ItemsSource = GamepadActions.All, Margin = new Thickness(4, 2), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
                int button = b;
                combo.SelectionChanged += (_, __) => { if (!_loading) _edit[_pad].Set(button, combo.SelectedItem as string); };
                var row = new Border { Background = Brushes.Transparent, CornerRadius = new CornerRadius(3), Child = label };
                Grid.SetRow(row, b); Grid.SetColumn(row, 0);
                Grid.SetRow(combo, b); Grid.SetColumn(combo, 1);
                Rows.Children.Add(row);
                Rows.Children.Add(combo);
                _rows[b] = row;
                _combos[b] = combo;
            }

            PadSelector.ItemsSource = new[] { "1", "2" };
            _pad = Math.Clamp(pad, 0, 1);
            PadSelector.SelectedIndex = _pad;
            LoadPad();

            _poll = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, __) => Highlight());
            _poll.Start();
            Closed += (_, __) => _poll.Stop();
        }

        private void LoadPad()
        {
            _loading = true;
            for (int b = 0; b < GamepadButtons.Count; b++)
                _combos[b].SelectedItem = _edit[_pad].ActionFor(b);
            _loading = false;
            string name = _session?.Gamepads != null && _session.Gamepads.Count > _pad ? _session.Gamepads.GetName(_pad) : "not connected";
            PadName.Text = name;
        }

        private void Highlight()
        {
            GamepadInput raw = _session?.LastGamepadInput(_pad) ?? default;
            for (int b = 0; b < GamepadButtons.Count; b++)
                _rows[b].Background = raw.IsDown(b) ? new SolidColorBrush(Color.Parse("#3A6EA5")) : Brushes.Transparent;
        }

        private void OnPadChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PadSelector.SelectedIndex < 0) return;
            _pad = PadSelector.SelectedIndex;
            LoadPad();
        }

        private void OnDefaults(object sender, RoutedEventArgs e)
        {
            _edit[_pad] = GamepadMapping.Default();
            LoadPad();
        }

        internal void SetAction(int button, string action)
        {
            _combos[button].SelectedItem = action;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            _settings.Input.Gamepad1Buttons = _edit[0];
            _settings.Input.Gamepad2Buttons = _edit[1];
            Accepted = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();
    }
}
