using System.Windows.Forms;

namespace ZeroWin
{
    /*
    public static class ExtensionMethods {
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            System.Type dgvType = dgv.GetType();
            System.Reflection.PropertyInfo pi = dgvType.GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi.SetValue(dgv, setting, null);
        }
    }
    */

    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [System.STAThread]
        private static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 form = new Form1();
            Application.Idle += new System.EventHandler(form.OnApplicationIdle);

            Application.Run(form);
        }
    }
}