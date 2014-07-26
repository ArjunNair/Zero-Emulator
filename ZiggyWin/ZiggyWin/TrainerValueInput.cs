namespace ZeroWin
{
    public partial class TrainerValueInput : System.Windows.Forms.Form
    {
        public int PokeValue {
            get {
                if (maskedTextBox1.Text == "")
                    maskedTextBox1.Text = "0000";
                return System.Convert.ToInt32(maskedTextBox1.Text);
            }
        }

        public string Title {
            set {
                this.Text = value;
            }
        }

        public TrainerValueInput() {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (System.Windows.Forms.Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
        }
    }
}