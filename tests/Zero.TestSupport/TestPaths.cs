using System;
using System.IO;

namespace Zero.TestSupport
{
    /// <summary>Locates repo assets (ROMs, sample programs) relative to the test assembly.</summary>
    public static class TestPaths
    {
        public static string RepoRoot { get; } = FindRepoRoot();

        public static string RomDir => Path.Combine(RepoRoot, "run", "roms") + Path.DirectorySeparatorChar;
        public static string ProgramsDir => Path.Combine(RepoRoot, "run", "programs");
        public static string AssetsDir => Path.Combine(RepoRoot, "tests", "assets");

        private static string FindRepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "Zero.sln")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            throw new InvalidOperationException("Could not locate Zero.sln above " + AppContext.BaseDirectory);
        }
    }
}
