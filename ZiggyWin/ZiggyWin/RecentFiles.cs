using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Text;

using System.Runtime.InteropServices;

namespace ZeroWin
{
    class RecentFilesManager
    {
        private Action<object, EventArgs> OnRecentFileClick;
        private List<string> fullPath = new List<string>();
        private const int MAX_FILE_PATH_CHARS = 40;
        private ZeroConfig configRef;
        public class DynamicToolStripMenuItem : ToolStripMenuItem
        {

        }

        private ToolStripMenuItem ParentMenuItem;

        [DllImport("shlwapi.dll")]
        static extern bool PathCompactPathEx([Out] StringBuilder pszOut, string szPath, int cchMax, int dwFlags);

        static string TruncatePath(string path, int length) {
            StringBuilder sb = new StringBuilder(length + 1);
            PathCompactPathEx(sb, path, length, 0);
            return sb.ToString();
        }

        private void _onClearRecentFiles_Click(object obj, EventArgs evt) {
            try {
                //Properties.Settings.Default.RecentFiles.Clear();
                configRef.recentFiles.files.Clear();
                this.ParentMenuItem.DropDownItems.Clear();
                fullPath.Clear();
                this.ParentMenuItem.Enabled = false;
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        private void _refreshRecentFilesMenu() {
            ToolStripItem tSI;
            fullPath.Clear();

            try {
                //if (Properties.Settings.Default.RecentFiles == null || Properties.Settings.Default.RecentFiles.Count == 0) {
                if (configRef.recentFiles.files.Count == 0) { 
                    this.ParentMenuItem.Enabled = false;
                    return;
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Cannot open recent files list:\n" + ex.ToString());
                return;
            }

            this.ParentMenuItem.DropDownItems.Clear();

            //foreach (string name in Properties.Settings.Default.RecentFiles) {
            foreach (string name in configRef.recentFiles.files) { 
                fullPath.Add(name);
                tSI = this.ParentMenuItem.DropDownItems.Add(TruncatePath(name, MAX_FILE_PATH_CHARS));
                tSI.Click += new EventHandler(this.OnRecentFileClick);
            }

            if (this.ParentMenuItem.DropDownItems.Count == 0) {
                this.ParentMenuItem.Enabled = false;
                return;
            }
            this.ParentMenuItem.DropDownItems.Add("-");
            tSI = this.ParentMenuItem.DropDownItems.Add("Clear list");
            tSI.Click += new EventHandler(this._onClearRecentFiles_Click);
            this.ParentMenuItem.Enabled = true;
        }

        public void AddRecentFile(string fileNameWithFullPath) {
            try {
                if (configRef.recentFiles.files != null && configRef.recentFiles.files.Contains(fileNameWithFullPath)) {                    return;
                }

                //Properties.Settings.Default.RecentFiles.Insert(0, fileNameWithFullPath);
                configRef.recentFiles.files.Insert(0, fileNameWithFullPath);
                if (configRef.recentFiles.files.Count > 10) {
                    configRef.recentFiles.files.RemoveAt(10);
                }
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
            this._refreshRecentFilesMenu();
        }
        public void RemoveRecentFile(string fileNameWithFullPath) {
            try {
                configRef.recentFiles.files.Remove(fileNameWithFullPath);
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
            this._refreshRecentFilesMenu();
        }

        public string GetFullFilePath(int index) {
            if (index < fullPath.Count)
                return fullPath[index];

            return null;
        }

        public RecentFilesManager(ZeroConfig config, ToolStripMenuItem parentMenuItem, Action<object, EventArgs> onRecentFileClick) {
            this.ParentMenuItem = parentMenuItem;
            this.OnRecentFileClick = onRecentFileClick;
            configRef = config;
            _refreshRecentFilesMenu();
        }

    }
}
