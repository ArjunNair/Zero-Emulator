using System;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class SpectrumKeyboard : Form
    {
        private Form1 ziggyWin;

        public SpectrumKeyboard(Form1 _zw) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            ziggyWin = _zw;
            comboBox1.Items.AddRange(Speccy.SpectrumCharSet.Keywords);
            //Since the items are sorted, we can remove the symbols <= <> and >= from the keyword list
            //by repeatedly removing the first item in the list.
            comboBox1.Items.RemoveAt(0);
            comboBox1.Items.RemoveAt(0);
            comboBox1.Items.RemoveAt(0);
            comboBox1.IntegralHeight = false;
            comboBox1.MaxDropDownItems = 7;
            comboBox1.SelectedIndex = 0;
        }

        private void button1_Click(object sender, EventArgs e) {
            int index = Array.IndexOf(Speccy.SpectrumCharSet.Keywords, comboBox1.Items[comboBox1.SelectedIndex]);
            if (index >= 0) {
                ziggyWin.AddKeywordToEditorBuffer((byte)(165 + index));
            }
        }
    }
}