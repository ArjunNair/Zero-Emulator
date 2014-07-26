using System.Windows.Forms;

namespace ZeroWin
{
    public partial class CallStackViewer : Form
    {
        private Monitor monitor = null;

        public CallStackViewer(Monitor _monitor) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            monitor = _monitor;
            //dataGridView1.DoubleBuffered(true);
            //Set up the datagridview for memory
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.DataSource = monitor.ziggyWin.zx.callStackList;
            dataGridView1.RowHeadersVisible = false;
        }

        private void dataGridView1_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e) {
            dataGridView1.ClearSelection();
            dataGridView1.Rows[0].Selected = true;
        }

        private void dataGridView1_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e) {
            dataGridView1.ClearSelection();
            if (dataGridView1.Rows.Count > 0)
                dataGridView1.Rows[0].Selected = true;
        }

        public void RefreshView() {
            dataGridView1.DataSource = null;
            System.Threading.Thread.Sleep(1);
            dataGridView1.DataSource = monitor.ziggyWin.zx.callStackList;
            dataGridView1.Invalidate();
        }
    }
}