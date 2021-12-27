using System;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class ArchiveHandler : Form
    {
        public string FileToOpen = "";

        public ArchiveHandler(String[] fileList) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            listView1.Columns.Add("File");
            listView1.Columns.Add("Size (bytes)");
            //listView1.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            for (int f = 0; f < fileList.Length; ) {
                ListViewItem listItem = new ListViewItem();
                listItem.Text = fileList[f++];
                ListViewItem.ListViewSubItem subItem = new ListViewItem.ListViewSubItem(listItem, fileList[f++]);
                listItem.SubItems.Add(subItem);
                listView1.Items.Add(listItem);
            }
            listView1.AutoResizeColumn(0,
                ColumnHeaderAutoResizeStyle.ColumnContent);
            listView1.AutoResizeColumn(1,
               ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void button1_Click(object sender, EventArgs e) {
            //ListView.SelectedListViewItemCollection itemList = listView1.SelectedItems;
            ListViewItem li = listView1.SelectedItems[0];
            FileToOpen = li.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}