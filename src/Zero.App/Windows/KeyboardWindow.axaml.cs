using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SpeccyCommon;
using Zero.Emulation;

namespace Zero.App.Windows
{
    /// <summary>The Spectrum keyboard picture, a keyword typer, and a reminder of the shift keys.</summary>
    public partial class KeyboardWindow : Window
    {
        private readonly EmulatorSession _session;

        public KeyboardWindow() : this(null) { }

        public KeyboardWindow(EmulatorSession session)
        {
            InitializeComponent();
            _session = session;
            // Alphabetical, minus the comparison operators that sort to the front.
            KeywordBox.ItemsSource = SpeccyGlobals.Keywords.Where(k => !k.StartsWith("<") && !k.StartsWith(">")).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            KeywordBox.SelectedIndex = 0;
        }

        private void OnTypeKeyword(object sender, RoutedEventArgs e)
        {
            if (_session == null || !(KeywordBox.SelectedItem is string keyword)) return;
            _session.TypeToken(EmulatorSession.KeywordToken(keyword));
        }
    }
}
