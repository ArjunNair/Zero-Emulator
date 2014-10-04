﻿using System;
using System.IO;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class LoadBinary : Form
    {
        private Form1 ziggyWin;
        private bool loadMode = true;

        public LoadBinary(Form1 zw, bool lm) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            loadMode = lm;
            ziggyWin = zw;
            if (loadMode) {
                label3.Visible = false;
                maskedTextBox2.Visible = false;
                this.Text = "Load Binary";
                button2.Text = "Load";
                if (zw.zx.model == Speccy.MachineModel._48k) {
                    addressRadioButton.Checked = true;
                    ramPageRadioButton.Enabled = false;
                    pageComboBox.Enabled = false;
                } else {
                    ramPageRadioButton.Checked = true;
                    pageComboBox.SelectedIndex = 0;
                }
            } else {
                maskedTextBox2.Visible = true;
                this.Text = "Save Binary";
                button2.Text = "Save";

                if (zw.zx.model == Speccy.MachineModel._48k) {
                    addressRadioButton.Checked = true;
                    ramPageRadioButton.Enabled = false;
                    pageComboBox.Enabled = false;
                } else {
                    ramPageRadioButton.Checked = true;
                    pageComboBox.SelectedIndex = 0;
                }
                label3.Visible = true;
            }
        }

        private void button2_Click(object sender, EventArgs e) {
            if (addressRadioButton.Checked && maskedTextBox1.Text == "") {
                return;
            }

            if (loadMode) {
                int start = 16384;
                if (addressRadioButton.Checked) {
                    start = Convert.ToInt32(maskedTextBox1.Text);
                    if ((start < 16384) || (start > 65535)) {
                        MessageBox.Show("Enter a valid address from 16384 to 65535.", "Invalid address", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }

                FileStream fs;
                String filename = textBox1.Text;

                //Check if we can find the ROM file!
                try {
                    fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                } catch {
                    MessageBox.Show("Couldn't load file! Aborting.", "File invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                //fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                using (BinaryReader r = new BinaryReader(fs)) {
                    //int bytesRead = ReadBytes(r, mem, 0, 16384);
                    byte[] buffer = new byte[fs.Length];
                    int bytesRead = r.Read(buffer, 0, (int)fs.Length);

                    if (bytesRead == 0) {
                        MessageBox.Show("Error while loading file! Aborting.", "File invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    if (addressRadioButton.Checked) {
                        int end = start + (int)fs.Length;
                        if (end > 65536)
                            end = 65536;

                        for (int f = start; f < end; f++) {
                            ziggyWin.zx.PokeByteNoContend(f, buffer[f - start]);
                        }
                    } else {
                        int end = (int)fs.Length;
                        if (end > 16384)
                            end = 16384;

                        for (int f = 0; f < end; f++) {
                            ziggyWin.zx.PokeRAMPage(pageComboBox.SelectedIndex, f, buffer[f]);
                        }
                    }
                }
                fs.Close();
            } else {
                int start = 16384;
                int end = 16384;
                if (addressRadioButton.Checked) {
                    start = Convert.ToInt32(maskedTextBox1.Text);
                    if ((start < 0) || (start > 65535)) {
                        MessageBox.Show("Enter a valid address from 0 to 65535.", "Invalid address", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    end = start + Convert.ToInt32(maskedTextBox2.Text);

                    if (end > 65536) {
                        MessageBox.Show("Far too many bytes to write than that exist in memory!", "Invalid address range", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                } else {
                    end = Convert.ToInt32(maskedTextBox2.Text);
                    if (end > 16384)
                        end = 16384;
                }

                FileStream fs;
                String filename = textBox1.Text;

                try {
                    fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
                } catch {
                    MessageBox.Show("Couldn't create file! Aborting.", "File error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                using (BinaryWriter r = new BinaryWriter(fs)) {
                    if (addressRadioButton.Checked) {
                        for (int f = start; f < end; f++)
                            r.Write((byte)ziggyWin.zx.PeekByteNoContend(f));
                    } else {
                        byte[] ramData = ziggyWin.zx.GetPageData(pageComboBox.SelectedIndex);
                        r.Write(ramData, 0, end);
                    }
                }
                fs.Close();
                MessageBox.Show("Binary file saved successfully.", "File saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            this.Close();
        }

        private void browseButton_Click(object sender, EventArgs e) {
            if (loadMode) {
                openFileDialog1.Title = "Choose a file";
                openFileDialog1.FileName = "";
                openFileDialog1.Filter = "All files|*.*;*.*";
                if (openFileDialog1.ShowDialog() == DialogResult.OK) {
                    textBox1.Text = openFileDialog1.FileName;
                }
            } else {
                saveFileDialog1.FileName = "";
                saveFileDialog1.Filter = "All files|*.*";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                    textBox1.Text = saveFileDialog1.FileName;
                }
            }
        }

        private void button3_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void ramPageRadioButton_CheckedChanged(object sender, EventArgs e) {
            if (ramPageRadioButton.Checked) {
                addressRadioButton.Checked = false;
                maskedTextBox1.Enabled = false;
                pageComboBox.Enabled = true;
            }
        }

        private void addressRadioButton_CheckedChanged(object sender, EventArgs e) {
            if (addressRadioButton.Checked) {
                pageComboBox.Enabled = false;
                maskedTextBox1.Enabled = true;
                ramPageRadioButton.Checked = false;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            if (textBox1.Text == "")
                button2.Enabled = false;
            else
                button2.Enabled = true;
        }
    }
}