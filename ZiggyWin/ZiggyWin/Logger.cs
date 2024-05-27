using System;
using System.IO;

namespace ZeroWin
{
    public class Logger: IDisposable
    {
        StreamWriter sw;
        String filePath;
        public void Log(string s, bool finalise = false)
        {
            filePath = System.Windows.Forms.Application.StartupPath + "\\TempLog " + System.DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".txt";
            if (sw == null)
               sw = new System.IO.StreamWriter(filePath);

            sw.WriteLine(s);
            sw.Flush();

            if (finalise)
                sw.Close();
        }

        public void DebugLog(string s)
        {
            System.Diagnostics.Debug.WriteLine(s);
        }

        public void Dispose()
        {
            if(sw != null)
            {
                sw.Close();
                File.Delete(filePath);
            }
        }
    }
}
