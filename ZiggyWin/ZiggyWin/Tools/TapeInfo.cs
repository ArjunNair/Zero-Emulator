using System.Windows.Forms;

namespace ZeroWin
{
    public partial class TapeInfo : Form
    {
        public TapeInfo() {
            InitializeComponent();
        }

        public void SetText(string text)
        {
            textBox1.Text = text;
            textBox1.SelectionLength = 0;
        }

        private void TapeInfo_FormClosing(object sender, FormClosingEventArgs e) {
            e.Cancel = true;
            this.Hide();
        }

    }
}
