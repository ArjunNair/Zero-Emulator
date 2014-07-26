using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class Breakpoints : Form
    {
        private Monitor monitor = null;

        public Breakpoints(Monitor _monitor) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }

            monitor = _monitor;
            dataGridView2.ColumnHeadersBorderStyle = Monitor.ProperColumnHeadersBorderStyle;
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 =
                new System.Windows.Forms.DataGridViewCellStyle();

            //Define Header Style
            dataGridViewCellStyle2.Alignment =
                System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Consolas",
                10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.WrapMode =
                System.Windows.Forms.DataGridViewTriState.False;

            //Apply Header Style
            dataGridView2.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;

            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.ControlLightLight;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Consolas",
                10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;

            dataGridView2.DefaultCellStyle = dataGridViewCellStyle3;

            //Set up the datagridview for breakpoints
            dataGridView2.AutoGenerateColumns = false;
            dataGridView2.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView2.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            DataGridViewTextBoxColumn dgrid2ColCondition = new DataGridViewTextBoxColumn();
            dgrid2ColCondition.HeaderText = "Condition";
            dgrid2ColCondition.Name = "Condition";
            dgrid2ColCondition.Width = 141;
            dgrid2ColCondition.DataPropertyName = "Condition";
            dataGridView2.Columns.Add(dgrid2ColCondition);

            DataGridViewTextBoxColumn dgrid2ColAddress = new DataGridViewTextBoxColumn();
            dgrid2ColAddress.HeaderText = "Address";
            dgrid2ColAddress.Name = "Address";
            dgrid2ColAddress.Width = 141;
            dgrid2ColAddress.DataPropertyName = "AddressAsString";
            dataGridView2.Columns.Add(dgrid2ColAddress);

            DataGridViewTextBoxColumn dgrid3ColData = new DataGridViewTextBoxColumn();
            dgrid3ColData.HeaderText = "Value";
            dgrid3ColData.Name = "Data";
            dgrid3ColData.Width = 141;
            dgrid3ColData.DataPropertyName = "DataAsString";
            dataGridView2.Columns.Add(dgrid3ColData);

            dataGridView2.DataSource = monitor.breakPointConditions;

            //Setup the listbox for valid breakpoint registers
            comboBox2.Items.Add("PC");
            comboBox2.Items.Add("HL");
            comboBox2.Items.Add("BC");
            comboBox2.Items.Add("DE");
            comboBox2.Items.Add("A");
            comboBox2.Items.Add("IX");
            comboBox2.Items.Add("IY");
            comboBox2.Items.Add("SP");
            comboBox2.Items.Add("Memory Execute");
            comboBox2.Items.Add("Memory Write");
            comboBox2.Items.Add("Memory Read");
            comboBox2.Items.Add("Port Write");
            comboBox2.Items.Add("Port Read");
            comboBox2.Items.Add("ULA Write");
            comboBox2.Items.Add("ULA Read");
            comboBox2.Items.Add("Interrupt (INT)");
            comboBox2.Items.Add("Retriggered INT");
            comboBox2.SelectedIndex = 0;
            comboBox2_SelectedIndexChanged(this, null); //sanity check for case ULA port breakpoints are selected
        }

        public void RefreshView(bool isHexView) {
            if (isHexView)
                this.dataGridView2.Columns[1].DefaultCellStyle.Format = "x2";
            else
                this.dataGridView2.Columns[1].DefaultCellStyle.Format = "";
        }

        private void clearSelectedButton_Click(object sender, EventArgs e) {
            DataGridViewSelectedRowCollection rowCollection = dataGridView2.SelectedRows;
            if (rowCollection.Count < 1)
                return;

            foreach (DataGridViewRow row in rowCollection) {
                KeyValuePair<String, Monitor.BreakPointCondition> kv;

                //convert dashes (-) to -1 (int) where required,
                //else convert the actual value to int.
                int _addr = -1;
                int _val = -1;
                if ((String)row.Cells[1].Value != "-")
                    _addr = Convert.ToInt32(row.Cells[1].Value);

                if ((String)row.Cells[2].Value != "-")
                    _val = Convert.ToInt32(row.Cells[2].Value);

                kv = new KeyValuePair<String, Monitor.BreakPointCondition>((String)row.Cells[0].Value, new Monitor.BreakPointCondition((String)row.Cells[0].Value, _addr, _val));
                monitor.RemoveBreakpoint(kv);
            }
        }

        private void clearAllButton_Click(object sender, EventArgs e) {
            monitor.RemoveAllBreakpoints();
        }

        private void addOtherBreakpointButton_Click(object sender, EventArgs e) {
            if ((comboBox2.SelectedIndex < 0) || (comboBox2.SelectedIndex < 14 && maskedTextBox2.Text.Length < 1))
                return;

            int addr = -1;
            int val = -1;
            bool validInput = true;

            if (comboBox2.SelectedIndex < 14) {
                if (maskedTextBox2.Text[0] == '$') {
                    for (int t = 1; t < maskedTextBox2.Text.Length; t++) {
                        if (!(maskedTextBox2.Text[t] >= '0' && maskedTextBox2.Text[t] <= '9') &&
                            !(maskedTextBox2.Text[t] >= 'a' && maskedTextBox2.Text[t] <= 'f') &&
                            !(maskedTextBox2.Text[t] >= 'A' && maskedTextBox2.Text[t] <= 'F')) {
                            validInput = false;
                            break;
                        }
                    }
                    if (!validInput || (maskedTextBox2.Text.Length < 2)) {
                        System.Windows.Forms.MessageBox.Show("Invalid hex number!", "Invalid input", MessageBoxButtons.OK);
                        return;
                    }

                    addr = Int32.Parse(maskedTextBox2.Text.Substring(1, maskedTextBox2.Text.Length - 1), System.Globalization.NumberStyles.HexNumber);
                } else {
                    for (int t = 1; t < maskedTextBox2.Text.Length; t++) {
                        if (!(maskedTextBox2.Text[t] >= '0' && maskedTextBox2.Text[t] <= '9')) {
                            System.Windows.Forms.MessageBox.Show("Invalid decimal number!", "Invalid input", MessageBoxButtons.OK);
                            validInput = false;
                            break;
                        }
                    }

                    if (!validInput)
                        return;

                    addr = Convert.ToInt32(maskedTextBox2.Text);
                }

                if (addr > 65535 || addr < 0) {
                    System.Windows.Forms.MessageBox.Show("The address is not within 0 to 65535!", "Invalid input", MessageBoxButtons.OK);
                    return;
                }
            } else if (comboBox2.Text == "ULA Write" || comboBox2.Text == "ULA Read")
                addr = 254; //0xfe

            validInput = true;
            if (maskedTextBox3.Text.Length > 0) {
                if (maskedTextBox3.Text[0] == '$') {
                    for (int t = 1; t < maskedTextBox3.Text.Length; t++) {
                        if (!(maskedTextBox3.Text[t] >= '0' && maskedTextBox3.Text[t] <= '9') &&
                            !(maskedTextBox3.Text[t] >= 'a' && maskedTextBox3.Text[t] <= 'f') &&
                            !(maskedTextBox3.Text[t] >= 'A' && maskedTextBox3.Text[t] <= 'F')) {
                            validInput = false;
                            break;
                        }
                    }
                    if (!validInput || (maskedTextBox3.Text.Length < 2)) {
                        System.Windows.Forms.MessageBox.Show("Invalid hex number!", "Invalid input", MessageBoxButtons.OK);
                        return;
                    }

                    val = Int32.Parse(maskedTextBox3.Text.Substring(1, maskedTextBox3.Text.Length - 1), System.Globalization.NumberStyles.HexNumber);
                } else {
                    for (int t = 1; t < maskedTextBox3.Text.Length; t++) {
                        if (!(maskedTextBox3.Text[t] >= '0' && maskedTextBox3.Text[t] <= '9')) {
                            System.Windows.Forms.MessageBox.Show("Invalid decimal number!", "Invalid input", MessageBoxButtons.OK);
                            validInput = false;
                            break;
                        }
                    }

                    if (!validInput)
                        return;

                    val = Convert.ToInt32(maskedTextBox3.Text);
                }

                if (val > 255 || val < 0) {
                    System.Windows.Forms.MessageBox.Show("The value is not within 0 to 255!", "Invalid input", MessageBoxButtons.OK);
                    return;
                }
            } else
                val = -1;

            string _str = comboBox2.SelectedItem.ToString();// +"@" + addr.ToString();
            KeyValuePair<String, Monitor.BreakPointCondition> kv = new KeyValuePair<String, Monitor.BreakPointCondition>(_str, new Monitor.BreakPointCondition(_str, addr, val));

            monitor.AddBreakpoint(kv);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e) {
            maskedTextBox3.Text = "";
            if ((comboBox2.Text == "ULA Write") || (comboBox2.Text == "ULA Read")) {
                maskedTextBox2.Text = "$fe";
                maskedTextBox2.ReadOnly = true;
                maskedTextBox3.ReadOnly = false;
            } else if (comboBox2.Text == "Interrupt") {
                maskedTextBox2.Text = "";

                maskedTextBox3.ReadOnly = true;
                maskedTextBox2.ReadOnly = true;
            } else {
                maskedTextBox3.ReadOnly = false;
                maskedTextBox2.ReadOnly = false;
            }
        }
    }
}