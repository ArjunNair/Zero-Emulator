using System;

namespace Ziggy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (Ziggy game = new Ziggy())
            {
                game.Run();
            }
        }
    }
}

