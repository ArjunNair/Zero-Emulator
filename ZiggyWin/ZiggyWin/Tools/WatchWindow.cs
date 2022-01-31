using System;
using System.Windows.Forms;
using SpeccyCommon;

namespace ZeroWin
{
    public partial class WatchWindow : Form
    {
        private Monitor monitor = null;

        public WatchWindow(Monitor _monitor)
        {
            InitializeComponent();

            monitor = _monitor;

            dataGridView1.ColumnHeadersBorderStyle = Monitor.ProperColumnHeadersBorderStyle;
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 =
                new System.Windows.Forms.DataGridViewCellStyle();

            //Define Header Style
            dataGridViewCellStyle2.Alignment =
                System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle2.BackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Consolas",
                8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = Control.DefaultBackColor;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.WrapMode =
                System.Windows.Forms.DataGridViewTriState.False;


            dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;

            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.ControlLightLight;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Consolas",
                8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;

            dataGridView1.DefaultCellStyle = dataGridViewCellStyle3;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridView1.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            
            DataGridViewTextBoxColumn dgrid1ColAddress = new DataGridViewTextBoxColumn();
            dgrid1ColAddress.HeaderText = "Address";
            dgrid1ColAddress.Name = "Address";
            dgrid1ColAddress.Width = 120;
            dgrid1ColAddress.DataPropertyName = "AddressAsString";
            dataGridView1.Columns.Add(dgrid1ColAddress);

            DataGridViewTextBoxColumn dgrid2ColData = new DataGridViewTextBoxColumn();
            dgrid2ColData.HeaderText = "Label";
            dgrid2ColData.Name = "Label";
            dgrid2ColData.Width = 141;
            dgrid2ColData.DataPropertyName = "Label";
            dataGridView1.Columns.Add(dgrid2ColData);

            DataGridViewTextBoxColumn dgrid3ColData = new DataGridViewTextBoxColumn();
            dgrid3ColData.HeaderText = "Value";
            dgrid3ColData.Name = "Data";
            dgrid3ColData.Width = 50;
            dgrid3ColData.DataPropertyName = "DataAsString";
            dataGridView1.Columns.Add(dgrid3ColData);

            dataGridView1.DataSource = monitor.watchVariableList;
            //monitor.ziggyWin.zx.MemoryWriteEvent += new MemoryWriteEventHandler(Monitor_MemoryWrite);
        }

        private void Monitor_MemoryWrite(object sender, MemoryEventArgs e) {
            throw new NotImplementedException();
        }

        public void RefreshData(bool isHex)
        {
            dataGridView1.DataSource = null;
            System.Threading.Thread.Sleep(1);
            dataGridView1.DataSource = monitor.watchVariableList;
            if(isHex)
            {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "x2";
            }
            else
            {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if(maskedTextBox1.Text.Length < 1)
                return;

            int addr = Utilities.ConvertToInt(maskedTextBox1.Text);

            if(addr > 65535)
            {
                System.Windows.Forms.MessageBox.Show("The address is not within 0 to 65535!", "Invalid input", MessageBoxButtons.OK);
                return;
            }

            if (addr >= 0) {
                foreach (Monitor.WatchVariable wv in monitor.watchVariableList) {
                    if (wv.Address == addr)
                        return;
                }
                monitor.AddWatchVariable(addr, textBox1.Text);
                dataGridView1.Refresh();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            monitor.RemoveAllWatchVariables();
            dataGridView1.Refresh();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DataGridViewSelectedRowCollection rowCollection = dataGridView1.SelectedRows;
            if(rowCollection.Count < 1)
                return;

            foreach(DataGridViewRow row in rowCollection)
            {
                int _addr = Convert.ToInt32(row.Cells[0].Value);
                monitor.RemoveWatchVariable(_addr);
            }
            dataGridView1.Refresh();
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }
    }
}
