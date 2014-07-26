using System.Windows.Forms;

namespace ZeroWin
{
    public partial class PokeMemory : Form
    {
        private Monitor monitorRef;

        public PokeMemory(Monitor m) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            monitorRef = m;
        }

        private void button1_Click(object sender, System.EventArgs e) {
            int addr = -1;
            int val = -1;

            if (textBox1.Text[0] == '$')
                addr = System.Int32.Parse(textBox1.Text.Substring(1, textBox1.Text.Length - 1), System.Globalization.NumberStyles.HexNumber);
            else
                addr = System.Convert.ToInt32(textBox1.Text);

            if (textBox2.Text[0] == '$')
                val = (System.Int32.Parse(textBox2.Text.Substring(1, textBox2.Text.Length - 1), System.Globalization.NumberStyles.HexNumber)) & 0xff;
            else
                val = (System.Convert.ToInt32(textBox2.Text)) & 0xff;

            if (addr > -1 && val > -1) {
                monitorRef.PokeByte(addr, val);
                this.Close();
            }
        }
    }
}