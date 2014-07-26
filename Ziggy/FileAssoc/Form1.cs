using System;
using System.Windows.Forms;

namespace FileAssoc
{
    public partial class Form1 : Form
    {
        private bool runError = true;

        public Form1() {
            this.Visible = false;
            InitializeComponent();
            if (!IsUserAdministrator()) {
                MessageBox.Show("You need to have admin privileges to continue.", "Access denied!", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Environment.ExitCode = -1;
                return;
                //this.Close();
            }
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Length > 1) {
                try {
                    string assoc = "";
                    string progID = "";
                    string desc = "";
                    string app = "";
                    string iconFile = "";
                    for (int f = 1; f < commandLineArgs.Length; f++) {
                        bool bind = (commandLineArgs[f][0] != '0' ? true : false); //0 = unbind, 1 = bind
                        string fileAssoc = commandLineArgs[f].Substring(1); //file extension to bind
                        switch (fileAssoc) {
                            case ".pzx":
                                assoc = ".pzx";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroPZX" : "");
                                desc = (bind ? "Spectrum PZX Tape" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 4, app);
                                break;

                            case ".tzx":
                                assoc = ".tzx";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroTZX" : "");
                                desc = (bind ? "Spectrum TZX Tape" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 4, app);
                                break;

                            case ".tap":
                                assoc = ".tap";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroTAP" : "");
                                desc = (bind ? "Spectrum TAP Tape" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 4, app);
                                break;

                            case ".sna":
                                assoc = ".sna";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroSNA" : "");
                                desc = (bind ? "Spectrum SNA Snapshot" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 1, app);
                                break;

                            case ".szx":
                                assoc = ".szx";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroSZX" : "");
                                desc = (bind ? "Spectrum SZX Snapshot" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 2, app);
                                break;

                            case ".z80":
                                assoc = ".z80";// (optionWindow.FileAssociatePZX ? ".pzx" : "");
                                progID = (bind ? "ZeroZ80" : "");
                                desc = (bind ? "Spectrum Z80 Snapshot" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 3, app);
                                break;

                            case ".dsk":
                                assoc = ".dsk";// (optionWindow.FileAssociateCSW ? ".csw" : "");
                                progID = (bind ? "ZeroDSK" : "");
                                desc = (bind ? "Spectrum +3 disk" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 5, app);
                                break;

                            case ".trd":
                                assoc = ".trd";// (optionWindow.FileAssociateCSW ? ".csw" : "");
                                progID = (bind ? "ZeroTRD" : "");
                                desc = (bind ? "Spectrum TR DOS disk image" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 5, app);
                                break;

                            case ".scl":
                                assoc = ".scl";// (optionWindow.FileAssociateCSW ? ".csw" : "");
                                progID = (bind ? "ZeroSCL" : "");
                                desc = (bind ? "Sinclair TR DOS disk" : "");
                                app = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                iconFile = (bind ? Application.StartupPath + @"\zero.exe" : "");
                                FileAssociate(assoc, progID, desc, iconFile, 5, app);
                                break;
                        }
                    }
                    runError = false;
                } catch (Exception e) {
                    MessageBox.Show(e.Message, "File Association error", MessageBoxButtons.OK);
                    Environment.ExitCode = -1;
                    return;
                }
            }
        }

        public bool IsUserAdministrator() {
            //bool value to hold our return value
            bool isAdmin;
            try {
                //get the currently logged in user
                System.Security.Principal.WindowsIdentity user = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(user);
                isAdmin = principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            } catch (UnauthorizedAccessException ex) {
                isAdmin = false;
                MessageBox.Show(ex.Message);
            } catch (Exception ex) {
                isAdmin = false;
                MessageBox.Show(ex.Message);
            }
            return isAdmin;
        }

        private void button1_Click(object sender, EventArgs e) {
            //Environment.ExitCode = 0;
            //this.Close();
        }

        // Associate file extension with progID, description, icon and application
        public static void FileAssociate(string extension, string progID, string description, string iconFile, int iconIndex, string application) {
            if (FileIsAssociated(extension))
                return;

            Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(extension).SetValue("", progID);
            if (progID != null && progID.Length > 0)
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.CreateSubKey(progID)) {
                    if (description != null)
                        key.SetValue("", description);
                    //if (icon != null)
                    String iconPath = ToShortPathName(iconFile);
                    //String iconPath = iconFile;
                    if ((iconIndex >= 0) && (iconFile != null))
                        iconPath = iconPath + "," + iconIndex.ToString();
                    key.CreateSubKey("DefaultIcon").SetValue("", iconPath);
                    if (application != null)
                        key.CreateSubKey(@"Shell\Open\Command").SetValue("",
                                    ToShortPathName(application) + " \"%1\"");
                }
        }

        // Return true if extension already associated in registry
        public static bool FileIsAssociated(string extension) {
            Microsoft.Win32.RegistryKey currentAssoc = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(extension, false);
            if (currentAssoc == null)
                return false;

            string progID = "";
            currentAssoc.GetValue(extension, progID, Microsoft.Win32.RegistryValueOptions.None);
            if (progID == "ZeroEmulator")
                return true;

            return false;
            //return (Registry.ClassesRoot.OpenSubKey(extension, false) != null);
        }

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern uint GetShortPathName(string lpszLongPath,
            [System.Runtime.InteropServices.Out] System.Text.StringBuilder lpszShortPath, uint cchBuffer);

        // Return short path format of a file name
        private static string ToShortPathName(string longName) {
            System.Text.StringBuilder s = new System.Text.StringBuilder(1000);
            uint iSize = (uint)s.Capacity;
            uint iRet = GetShortPathName(longName, s, iSize);
            return s.ToString();
        }

        private void Form1_Load(object sender, EventArgs e) {
            if (runError) {
                this.Close();
            } else {
                Environment.ExitCode = 0;
                this.Close();
            }
        }
    }
}