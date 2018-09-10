using System.Windows.Forms;

namespace ZeroWin
{
    public partial class MemoryViewer : Form
    {
        private Monitor monitor = null;

        public MemoryViewer(Monitor _monitor) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            monitor = _monitor;
            // dataGridView1.DoubleBuffered(true);
            //Set up the datagridview for memory
            dataGridView1.AutoGenerateColumns = false;

            DataGridViewTextBoxColumn dgridColAddress2 = new DataGridViewTextBoxColumn();
            dgridColAddress2.HeaderText = "Address";
            dgridColAddress2.Name = "Address";
            dgridColAddress2.Width = 60;
            dgridColAddress2.DataPropertyName = "Address";
            dataGridView1.Columns.Add(dgridColAddress2);

            DataGridViewTextBoxColumn dgridColGetBytes = new DataGridViewTextBoxColumn();
            dgridColGetBytes.HeaderText = "Bytes";
            dgridColGetBytes.Name = "Bytes";
            dgridColGetBytes.Width = 280;
            dgridColGetBytes.DataPropertyName = "GetBytes";
            dataGridView1.Columns.Add(dgridColGetBytes);

            DataGridViewTextBoxColumn dgridColGetChars = new DataGridViewTextBoxColumn();
            dgridColGetChars.HeaderText = "Characters";
            dgridColGetChars.Name = "Characters";
            dgridColGetChars.Width = 115;
            dgridColGetChars.DataPropertyName = "GetCharacters";
            dataGridView1.Columns.Add(dgridColGetChars);

            dataGridView1.DataSource = monitor.memoryViewList;
        }

        public void RefreshData(bool isHex) {
            dataGridView1.DataSource = null;
            System.Threading.Thread.Sleep(1);
            dataGridView1.DataSource = monitor.memoryViewList;
            if (isHex) {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "x2";
            } else {
                this.dataGridView1.Columns[0].DefaultCellStyle.Format = "";
            }
        }

        private void MemoryViewer_FormClosing(object sender, FormClosingEventArgs e) {
            dataGridView1.DataSource = null;
        }
    }
}