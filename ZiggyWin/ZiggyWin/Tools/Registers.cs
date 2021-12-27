using System;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class Registers : Form
    {
        private Monitor monitor = null;

        public Registers(Monitor _monitor) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            monitor = _monitor;
        }

        //Returns the high byte of a word value
        private int High(int val) {
            return ((val >> 8) & 0xff);
        }

        //Returns the low byte of a word value
        private int Low(int val) {
            return (val & 0xff);
        }

        //This ugly sounding function updates the value of registers based on state of
        //hex and byte checkboxes. Called from the events of both checkboxes.
        public void RefreshView(bool isHexView) {
            if (checkBox1.Checked) {
                if (isHexView) {
                    AFlink.Text = High(monitor.ValueAF).ToString("x2") + "  " + Low(monitor.ValueAF).ToString("x2");
                    HLlink.Text = High(monitor.ValueHL).ToString("x2") + "  " + Low(monitor.ValueHL).ToString("x2");
                    DElink.Text = High(monitor.ValueDE).ToString("x2") + "  " + Low(monitor.ValueDE).ToString("x2");
                    BClink.Text = High(monitor.ValueBC).ToString("x2") + "  " + Low(monitor.ValueBC).ToString("x2");
                    IRlink.Text = High(monitor.ValueIR).ToString("x2") + "  " + Low(monitor.ValueIR).ToString("x2");
                    HL_link.Text = High(monitor.ValueHL_).ToString("x2") + "  " + Low(monitor.ValueHL_).ToString("x2");
                    BC_link.Text = High(monitor.ValueBC_).ToString("x2") + "  " + Low(monitor.ValueBC_).ToString("x2");
                    DE_link.Text = High(monitor.ValueDE_).ToString("x2") + "  " + Low(monitor.ValueDE_).ToString("x2");
                    AF_link.Text = High(monitor.ValueAF_).ToString("x2") + "  " + Low(monitor.ValueAF_).ToString("x2");
                    PClink.Text = High(monitor.ValuePC).ToString("x2") + "  " + Low(monitor.ValuePC).ToString("x2");
                    IMlink.Text = monitor.ValueIM.ToString("x");
                    MPlink.Text = High(monitor.ValueMP).ToString("x2") + "  " + Low(monitor.ValueMP).ToString("x2"); ;
                    SPlink.Text = High(monitor.ValueSP).ToString("x2") + "  " + Low(monitor.ValueSP).ToString("x2"); ;
                    IYlink.Text = High(monitor.ValueIY).ToString("x2") + "  " + Low(monitor.ValueIY).ToString("x2"); ;
                    IXlink.Text = High(monitor.ValueIX).ToString("x2") + "  " + Low(monitor.ValueIX).ToString("x2"); ;
                } else {
                    AFlink.Text = High(monitor.ValueAF).ToString("0##") + "  " + Low(monitor.ValueAF).ToString("0##");
                    AF_link.Text = High(monitor.ValueAF_).ToString("0##") + "  " + Low(monitor.ValueAF_).ToString("0##");
                    HLlink.Text = High(monitor.ValueHL).ToString("0##") + "  " + Low(monitor.ValueHL).ToString("0##");
                    BClink.Text = High(monitor.ValueBC).ToString("0##") + "  " + Low(monitor.ValueBC).ToString("0##");
                    DElink.Text = High(monitor.ValueDE).ToString("0##") + "  " + Low(monitor.ValueDE).ToString("0##");
                    HL_link.Text = High(monitor.ValueHL_).ToString("0##") + "  " + Low(monitor.ValueHL_).ToString("0##");
                    BC_link.Text = High(monitor.ValueBC_).ToString("0##") + "  " + Low(monitor.ValueBC_).ToString("0##");
                    DE_link.Text = High(monitor.ValueDE_).ToString("0##") + "  " + Low(monitor.ValueDE_).ToString("0##");
                    IRlink.Text = High(monitor.ValueIR).ToString("0##") + "  " + Low(monitor.ValueIR).ToString("0##");
                    IMlink.Text = monitor.ValueIM.ToString();
                    MPlink.Text = High(monitor.ValueMP).ToString("0##") + "  " + Low(monitor.ValueMP).ToString("0##");
                    SPlink.Text = High(monitor.ValueSP).ToString("0##") + "  " + Low(monitor.ValueSP).ToString("0##");
                    IYlink.Text = High(monitor.ValueIY).ToString("0##") + "  " + Low(monitor.ValueIY).ToString("0##");
                    IXlink.Text = High(monitor.ValueIX).ToString("0##") + "  " + Low(monitor.ValueIX).ToString("0##");
                    PClink.Text = High(monitor.ValuePC).ToString("0##") + "  " + Low(monitor.ValuePC).ToString("0##");
                }
            } else {
                if (isHexView) {
                    IMlink.Text = monitor.ValueIM.ToString("x");
                    MPlink.Text = monitor.ValueMP.ToString("x");
                    SPlink.Text = monitor.ValueSP.ToString("x");
                    PClink.Text = monitor.ValuePC.ToString("x");
                    IYlink.Text = monitor.ValueIY.ToString("x");
                    IXlink.Text = monitor.ValueIX.ToString("x");
                    IRlink.Text = monitor.ValueIR.ToString("x");
                    AFlink.Text = monitor.ValueAF.ToString("x");
                    AF_link.Text = monitor.ValueAF_.ToString("x");
                    HLlink.Text = monitor.ValueHL.ToString("x");
                    HL_link.Text = monitor.ValueHL_.ToString("x");
                    BClink.Text = monitor.ValueBC.ToString("x");
                    BC_link.Text = monitor.ValueBC_.ToString("x");
                    DElink.Text = monitor.ValueDE.ToString("x");
                    DE_link.Text = monitor.ValueDE_.ToString("x");
                } else {
                    IMlink.Text = monitor.ValueIM.ToString();
                    MPlink.Text = monitor.ValueMP.ToString();
                    SPlink.Text = monitor.ValueSP.ToString();
                    PClink.Text = monitor.ValuePC.ToString();
                    IYlink.Text = monitor.ValueIY.ToString();
                    IXlink.Text = monitor.ValueIX.ToString();
                    IRlink.Text = monitor.ValueIR.ToString();
                    AFlink.Text = monitor.ValueAF.ToString();
                    AF_link.Text = monitor.ValueAF_.ToString();
                    HLlink.Text = monitor.ValueHL.ToString();
                    HL_link.Text = monitor.ValueHL_.ToString();
                    BClink.Text = monitor.ValueBC.ToString();
                    BC_link.Text = monitor.ValueBC_.ToString();
                    DElink.Text = monitor.ValueDE.ToString();
                    DE_link.Text = monitor.ValueDE_.ToString();
                }
            }

            int low = Low(monitor.ValueAF);
            int hi = High(monitor.ValueAF);

            if ((low & 0x01) != 0)
                FlagCCheck.Checked = true;
            else
                FlagCCheck.Checked = false;

            if ((low & 0x02) != 0)
                FlagNCheck.Checked = true;
            else
                FlagNCheck.Checked = false;

            if ((low & 0x04) != 0)
                FlagVCheck.Checked = true;
            else
                FlagVCheck.Checked = false;

            if ((low & 0x08) != 0)
                Flag3Check.Checked = true;
            else
                Flag3Check.Checked = false;

            if ((low & 0x10) != 0)
                FlagHCheck.Checked = true;
            else
                FlagHCheck.Checked = false;

            if ((low & 0x20) != 0)
                Flag5Check.Checked = true;
            else
                Flag5Check.Checked = false;

            if ((low & 0x40) != 0)
                FlagZCheck.Checked = true;
            else
                FlagZCheck.Checked = false;

            if ((low & 0x80) != 0)
                FlagSCheck.Checked = true;
            else
                FlagSCheck.Checked = false;

            interruptCheckBox.Checked = monitor.cpu.iff_1;
        }

        private void panel1_Paint(object sender, PaintEventArgs e) {
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            RefreshView(monitor.useHexNumbers);
        }

        private void PClink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValuePC);
        }

        private void HLlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueHL);
        }

        private void BClink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueBC);
        }

        private void DElink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueDE);
        }

        private void IXlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueIX);
        }

        private void SPlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueSP);
        }

        private void HL_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueHL_);
        }

        private void AFlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueAF);
        }

        private void IRlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueIR);
        }

        private void BC_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueBC_);
        }

        private void DE_link_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueDE_);
        }

        private void AF_lnk_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueAF_);
        }

        private void IYlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueIY);
        }

        private void MPlink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e) {
            monitor.JumpToAddress(monitor.ValueMP);
        }
    }
}