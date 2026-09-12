using System;

namespace Zero.Emulation
{
    /// <summary>Opt-in stderr tracing for thread/shutdown diagnostics: set ZERO_TRACE=1.</summary>
    public static class Trace
    {
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("ZERO_TRACE") == "1";

        public static void Log(string message)
        {
            if (Enabled)
                Console.Error.WriteLine($"[zero {DateTime.Now:HH:mm:ss.fff} t{System.Threading.Thread.CurrentThread.ManagedThreadId}] {message}");
        }
    }
}
