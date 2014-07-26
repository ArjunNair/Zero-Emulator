using System;
using System.IO;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class Profiler : Form
    {
        private Monitor monitor = null;

        public Profiler(Monitor _monitor) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            monitor = _monitor;
            //dataGridView3.DoubleBuffered(true);

            dataGridView3.AutoGenerateColumns = false;
            DataGridViewTextBoxColumn dgridColLogAddress = new DataGridViewTextBoxColumn();
            dgridColLogAddress.HeaderText = "Address";
            dgridColLogAddress.Name = "Address";
            // dgridColLogAddress.Width = 120;
            dgridColLogAddress.DataPropertyName = "Address";
            dataGridView3.Columns.Add(dgridColLogAddress);

            DataGridViewTextBoxColumn dgridColLogTstates = new DataGridViewTextBoxColumn();
            dgridColLogTstates.HeaderText = "T-State";
            dgridColLogTstates.Name = "Tstates";
            //dgridColLogTstates.Width = 150;
            dgridColLogTstates.DataPropertyName = "Tstates";
            dataGridView3.Columns.Add(dgridColLogTstates);

            DataGridViewTextBoxColumn dgridColLogInstructions = new DataGridViewTextBoxColumn();
            dgridColLogInstructions.HeaderText = "Instruction";
            dgridColLogInstructions.Name = "Opcodes";
            //dgridColLogInstructions.Width = 195;
            dgridColLogInstructions.DataPropertyName = "Opcodes";
            dataGridView3.Columns.Add(dgridColLogInstructions);
            dataGridView3.DataSource = monitor.logList;

            if (monitor.logList.Count == 0) {
                clearButton.Enabled = false;
                saveButton.Enabled = false;
            } else {
                clearButton.Enabled = true;
                saveButton.Enabled = true;
                if (monitor.isTraceOn) {
                    traceButton.Image = Properties.Resources.logStop;
                    traceButton.Text = "Stop";
                }
            }
        }

        public void RefreshData() {
            dataGridView3.DataSource = null;
            System.Threading.Thread.Sleep(1);
            dataGridView3.DataSource = monitor.logList;
        }

        private void saveButton_Click(object sender, EventArgs e) {
            FileStream fs;
            try {
                saveFileDialog1.Title = "Save Log";
                saveFileDialog1.FileName = "trace.log";

                if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                    fs = new FileStream(saveFileDialog1.FileName, FileMode.Create, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    if (monitor.useHexNumbers) {
                        sw.WriteLine("All numbers in hex.");
                        sw.WriteLine("-------------------");
                    } else {
                        sw.WriteLine("All numbers in decimal.");
                        sw.WriteLine("-----------------------");
                    }
                    foreach (Monitor.LogMessage log in monitor.logList) {
                        sw.WriteLine("{0,-5}   {1,-5}   {2,-20}", log.Address, log.Tstates, log.Opcodes);
                    }
                    sw.Close();
                }
            } catch {
                System.Windows.Forms.MessageBox.Show("Zero was unable to create a file! Either the disk is full, or there is a problem with access rights to the folder or something else entirely!",
                        "File Write Error!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void traceButton_Click(object sender, EventArgs e) {
            if (monitor.isTraceOn) {
                monitor.isTraceOn = false;
                traceButton.Image = Properties.Resources.logStart;
                traceButton.Text = "Start";
                if (monitor.logList.Count > 0) {
                    saveButton.Enabled = true;
                    clearButton.Enabled = true;
                }
            } else {
                monitor.logList.Clear();
                monitor.isTraceOn = true;
                traceButton.Image = Properties.Resources.logStop;
                traceButton.Text = "Stop";
            }
        }

        private void clearButton_Click(object sender, EventArgs e) {
            if (MessageBox.Show("Are you sure you wish to clear the execution log?", "Clear Log", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes) {
                monitor.logList.Clear();
                dataGridView3.Refresh();
                clearButton.Enabled = false;
                saveButton.Enabled = false;
            }
        }
    }
}