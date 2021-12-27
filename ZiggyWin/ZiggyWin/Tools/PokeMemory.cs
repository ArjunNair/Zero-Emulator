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
            int addr = Utilities.ConvertToInt(textBox1.Text);
            int val = Utilities.ConvertToInt(textBox2.Text);
            
            if (addr > -1 && val > -1) {
                monitorRef.PokeByte((ushort)addr, (byte)val);
                this.Close();
            }
        }
    }
}