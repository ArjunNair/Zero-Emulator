using System;
using System.IO;
using System.Windows.Forms;

namespace ZeroWin.Tools
{
    public partial class BASICImporter : Form
    {
        private Form1 zw = null;

        public BASICImporter(Form1 _zw)
        {
            this.zw = _zw;
            InitializeComponent();
        }

        private void importButton_Click(object sender, EventArgs e)
        {
            if(textBox1.Text.Length < 1)
                return;
            File.WriteAllText(Application.LocalUserAppDataPath + "//_tempbas.bas", textBox1.Text);
            zw.LoadZXFile(Application.LocalUserAppDataPath + "//_tempbas.bas");
            //File.Delete(Application.LocalUserAppDataPath + "//_tempbas.bas");
        }
    }
}
