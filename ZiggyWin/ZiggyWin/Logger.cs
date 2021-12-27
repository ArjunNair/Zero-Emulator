using System;
using System.IO;

namespace ZeroWin
{
    public class Logger: IDisposable
    {
        StreamWriter sw;

        public void Log(string s, bool finalise = false)
        {
            if (sw == null)
               sw = new System.IO.StreamWriter(@"ZeroLog.txt");

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
            if (sw != null)
                sw.Close();
        }
    }
}
