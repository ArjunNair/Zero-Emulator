namespace ZeroWin
{
    internal class PrecisionTimer
    {
        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceCounter(
            out long lpPerformanceCount);

        [System.Runtime.InteropServices.DllImport("Kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(
            out long lpFrequency);

        private long startTime, stopTime;
        private static long freq;
        private double duration;
        private static bool freqIsInitialized = false;

        public double DurationInSeconds { get { return duration; } }

        public double DurationInMilliseconds {
            get { return duration * 1000; }
        }

        // Constructor
        public PrecisionTimer() {
            startTime = 0;
            stopTime = 0;
            duration = 0;
            if (QueryPerformanceFrequency(out freq) == false) {
                // high-performance counter not supported
                throw new System.ComponentModel.Win32Exception();
            }
            freqIsInitialized = true;
        }

        // Start the timer
        public void Start() {
            QueryPerformanceCounter(out startTime);
            System.Threading.Thread.Sleep(0);
        }

        // Stop the timer
        public void Stop() {
            System.Threading.Thread.Sleep(0);
            QueryPerformanceCounter(out stopTime);
            duration = (double)(stopTime - startTime) / (double)freq; //save the difference
            System.Threading.Thread.Sleep(0);
        }

        // Returns the current time
        public static double TimeInSeconds() {
            if (!freqIsInitialized) {
                if (QueryPerformanceFrequency(out freq) == false) {
                    // high-performance counter not supported
                    throw new System.ComponentModel.Win32Exception();
                }
                freqIsInitialized = true;
            }
            long currentTime;
            System.Threading.Thread.Sleep(0);
            QueryPerformanceCounter(out currentTime);
            return ((double)currentTime / (double)freq); //save the difference
        }

        public static double TimeInMilliseconds() {
            if (!freqIsInitialized) {
                if (QueryPerformanceFrequency(out freq) == false) {
                    // high-performance counter not supported
                    throw new System.ComponentModel.Win32Exception();
                }
                freqIsInitialized = true;
            }
            long currentTime;
            System.Threading.Thread.Sleep(0);
            QueryPerformanceCounter(out currentTime);
            return (((double)currentTime * 1000) / (double)freq); //save the difference
        }
    }
}