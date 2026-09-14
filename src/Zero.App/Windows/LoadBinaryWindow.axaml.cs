using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Zero.Emulation;

namespace Zero.App.Windows
{
    /// <summary>Load a raw file into memory, or dump memory to a file, by address or 16K RAM bank.</summary>
    public partial class LoadBinaryWindow : Window
    {
        private readonly EmulatorSession _session;
        private readonly bool _saveMode;

        /// <summary>Set after a successful operation: bytes transferred.</summary>
        public int BytesTransferred { get; private set; } = -1;

        public LoadBinaryWindow() : this(null, false) { }

        public LoadBinaryWindow(EmulatorSession session, bool saveMode)
        {
            InitializeComponent();
            _session = session;
            _saveMode = saveMode;
            Title = saveMode ? "Save Binary" : "Load Binary";
            GoButton.Content = saveMode ? "Save" : "Load";
            LengthLabel.IsVisible = LengthBox.IsVisible = saveMode;
            BankBox.ItemsSource = Enumerable.Range(0, 8).Select(i => "Bank " + i).ToArray();
            BankBox.SelectedIndex = 0;
            bool banks = session?.HasRamBanks ?? false;
            BankRadio.IsEnabled = banks;
            if (banks) BankRadio.IsChecked = true;
        }

        private async void OnBrowse(object sender, RoutedEventArgs e)
        {
            string path;
            if (_saveMode)
            {
                IStorageFile f = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save binary as", SuggestedFileName = "memory.bin" });
                path = f?.TryGetLocalPath();
            }
            else
            {
                IReadOnlyList<IStorageFile> f = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose a binary file", AllowMultiple = false });
                path = f.Count > 0 ? f[0].TryGetLocalPath() : null;
            }
            if (path != null) FileBox.Text = path;
        }

        private void ShowError(string text) { ErrorText.Text = text; ErrorText.IsVisible = true; }

        /// <summary>Decimal, or hexadecimal written as 0x1234 or $1234 (both common in Spectrum tooling).</summary>
        internal static bool TryParseNumber(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string digits = text.Trim();
            NumberStyles style = NumberStyles.Integer;
            if (digits.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) { digits = digits.Substring(2); style = NumberStyles.HexNumber; }
            else if (digits.StartsWith("$", StringComparison.Ordinal) || digits.StartsWith("#", StringComparison.Ordinal)) { digits = digits.Substring(1); style = NumberStyles.HexNumber; }
            return digits.Length > 0 && int.TryParse(digits, style, CultureInfo.InvariantCulture, out value) && value >= 0;
        }

        private async void OnGo(object sender, RoutedEventArgs e)
        {
            ErrorText.IsVisible = false;
            string file = FileBox.Text?.Trim();
            if (string.IsNullOrEmpty(file)) { ShowError("Choose a file."); return; }
            bool byAddress = AddressRadio.IsChecked == true;
            int bank = Math.Max(0, BankBox.SelectedIndex);

            int address = 0, length = 0;
            if (byAddress && !TryParseNumber(AddressBox.Text, out address)) { ShowError("Enter an address as a number, for example 32768 or 0x8000."); return; }
            if (_saveMode && !TryParseNumber(LengthBox.Text, out length)) { ShowError("Enter a length as a number, for example 6912 or 0x1B00."); return; }
            if (_saveMode && length < 1) { ShowError("Length must be at least 1."); return; }

            try
            {
                if (_saveMode)
                {
                    if (byAddress && address + length > 65536) { ShowError("Address plus length runs past the end of memory (65536)."); return; }
                    byte[] data = byAddress ? await _session.SaveBinaryAsync(address, length) : await _session.SaveBankAsync(bank, length);
                    File.WriteAllBytes(file, data);
                    BytesTransferred = data.Length;
                }
                else
                {
                    if (!File.Exists(file)) { ShowError("File not found: " + file); return; }
                    if (byAddress && (address < 16384 || address > 65535)) { ShowError("Enter an address from 16384 to 65535 (ROM cannot be overwritten)."); return; }
                    byte[] data = File.ReadAllBytes(file);
                    if (data.Length == 0) { ShowError("The file is empty."); return; }
                    BytesTransferred = byAddress ? await _session.LoadBinaryAsync(data, address) : await _session.LoadBinaryToBankAsync(data, bank);
                }
                Close();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();
    }
}
