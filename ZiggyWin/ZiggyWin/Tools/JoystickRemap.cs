using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class JoystickRemap : Form
    {
        public class ButtonKeyCombo
        {
            public String Button {
                get;
                set;
            }

            public String Key {
                get;
                set;
            }
        }

        private System.Threading.Thread joystickPollThread;
        public BindingList<ButtonKeyCombo> ButtonKeyList = new BindingList<ButtonKeyCombo>();
        public bool dataChanged = false;
        private JoystickController joystick = null;
        private bool running = false;

        //Since the search button cannot be updated from Async web callback as it's on another thread.
        public delegate void SelectDataGridViewCell(int index);

        public JoystickRemap(Form1 zw, JoystickController jc) {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            joystick = jc;
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            if (System.IO.File.Exists(Application.UserAppDataPath + jc.name + ".xml")) {
                System.Data.DataSet ds = new System.Data.DataSet();
                ds.ReadXml(Application.UserAppDataPath + jc.name + ".xml", System.Data.XmlReadMode.InferSchema);
                dataGridView1.DataSource = ds;
            } else {
                for (int f = 0; f < jc.joystick.Caps.NumberButtons; f++) {
                    ButtonKeyCombo buttonKey = new ButtonKeyCombo();
                    buttonKey.Button = "Button " + (f + 1).ToString();
                    /*
                    if (f == 0) {
                        buttonKey.Key = "Fire";
                        jc.fireButtonIndex = 0;
                    } else
                     */
                    buttonKey.Key = "None";
                    ButtonKeyList.Add(buttonKey);
                }
                dataGridView1.AutoGenerateColumns = true;
                dataGridView1.DataSource = ButtonKeyList;
            }
            running = true;
            joystickPollThread = new System.Threading.Thread(new System.Threading.ThreadStart(JoystickPoll));
            joystickPollThread.Name = "Joystick Poll Thread";
            joystickPollThread.Priority = System.Threading.ThreadPriority.Lowest;
            joystickPollThread.Start();
        }

        private void button1_Click(object sender, EventArgs e) {
            DataGridViewSelectedRowCollection rowCollection = dataGridView1.SelectedRows;
            if (rowCollection.Count < 1)
                return;
            ButtonKeyList[rowCollection[0].Index].Key = comboBox1.SelectedItem.ToString();
            dataGridView1.Refresh();
        }

        private void SelectCell(int index) {
            if (index > dataGridView1.DisplayedRowCount(false) + dataGridView1.FirstDisplayedScrollingRowIndex) {
                dataGridView1.FirstDisplayedScrollingRowIndex = index;
                dataGridView1.Refresh();
            }
            dataGridView1.CurrentCell = dataGridView1.Rows[index].Cells[1];
            dataGridView1.Refresh();
            comboBox1.SelectedItem = ButtonKeyList[index].Key;
        }

        private void JoystickPoll() {
            while (running) {
                joystick.Update();
                byte[] buttons = joystick.state.GetButtons();
                for (int f = 0; f < buttons.Length; f++)
                    if (buttons[f] > 0) {
                        SelectDataGridViewCell selectCell = new SelectDataGridViewCell(SelectCell);
                        this.Invoke(selectCell, new object[] { f });
                        break;
                    }
                System.Threading.Thread.Sleep(1);
            }
        }

        private void JoystickRemap_FormClosed(object sender, FormClosedEventArgs e) {
            running = false;
        }

        private void dataGridView1_SelectionChanged(object sender, EventArgs e) {
            DataGridView dgv = (DataGridView)sender;
            comboBox1.SelectedItem = ButtonKeyList[dgv.CurrentRow.Index].Key;
        }
    }
}