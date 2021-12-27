using System;

//using System.Collections.Generic;
//using System.ComponentModel;
using System.Drawing;

//using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace ZeroWin
{
    partial class AboutBox1 : Form
    {
        private Form zwRef;

        public AboutBox1(Form fref) {
            zwRef = fref;
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.SizeInPoints);
            }
            // this.Text = String.Format("About {0} {0}", AssemblyTitle);
        }

        #region Assembly Attribute Accessors

        public string AssemblyTitle {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0) {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "") {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion {
            get {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0) {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0) {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0) {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0) {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }

        #endregion Assembly Attribute Accessors

        private void labelVersion_Click(object sender, EventArgs e) {
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
        }

        private void AboutBox1_Load(object sender, EventArgs e) {
            this.Location = new Point(zwRef.Location.X + 20, zwRef.Location.Y + 20);
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = 0;
            versionLabel.Text = "Version " + Application.ProductVersion;
        }
    }
}