using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Zero.Emulation
{
    /// <summary>Per-platform locations for config and user data.</summary>
    public static class AppPaths
    {
        public const string AppName = "Zero";

        /// <summary>%APPDATA%\Zero, ~/Library/Application Support/Zero, or $XDG_CONFIG_HOME/Zero.</summary>
        public static string ConfigDirectory
        {
            get
            {
                string root;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");
                else
                {
                    root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrEmpty(root))
                        root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                return Path.Combine(root, AppName);
            }
        }

        /// <summary>Folder the executable runs from; bundled ROMs and sample programs live under it.</summary>
        public static string ApplicationDirectory => AppContext.BaseDirectory;

        public static string TempDirectory => Path.Combine(Path.GetTempPath(), AppName);

        public static string EnsureDirectory(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Resolve a configured path: absolute paths pass through, relative ones hang off the app folder.</summary>
        public static string Resolve(string configured, string fallbackSubfolder)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return Path.Combine(ApplicationDirectory, fallbackSubfolder);
            string trimmed = configured.Trim().TrimStart('\\', '/').Replace('\\', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(configured) ? configured : Path.Combine(ApplicationDirectory, trimmed);
        }
    }
}
