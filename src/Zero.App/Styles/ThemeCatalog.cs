using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Classic.Avalonia.Theme;
using Semi.Avalonia;

namespace Zero.App.Styles
{
    /// <summary>
    /// The UI themes Zero can run under. A theme has to be chosen before any window is built:
    /// replacing it later leaves open windows without control templates, so the setting takes
    /// effect on restart.
    /// </summary>
    public static class ThemeCatalog
    {
        public const string Fluent = "Fluent";
        public const string Semi = "Semi";
        public const string Simple = "Simple";
        public const string Classic = "Classic";

        public static readonly string[] Names = { Fluent, Semi, Simple, Classic };

        public static string Describe(string name)
        {
            switch (Normalise(name))
            {
                case Semi: return "Modern and roomy; fewer rows fit in the tape deck and button lists.";
                case Simple: return "Plain and compact, in the style of an older desktop application.";
                case Classic: return "Windows 95 chrome, in keeping with the machine being emulated.";
                default: return "Zero's default. The macOS refinements apply only to this theme.";
            }
        }

        public static string Normalise(string name)
        {
            foreach (string known in Names)
                if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
                    return known;
            return Fluent;
        }

        public static IStyle Create(string name)
        {
            switch (Normalise(name))
            {
                case Semi: return new SemiTheme();
                case Simple: return new SimpleTheme();
                case Classic: return new ClassicTheme();
                default: return new FluentTheme();
            }
        }

        /// <summary>
        /// Install a theme into the application. App.axaml keeps the theme at Styles[0]; the macOS
        /// refinements are appended afterwards, and only for Fluent, because they reach into Fluent's
        /// own control templates by name.
        /// </summary>
        public static void Apply(Application app, string name)
        {
            string theme = Normalise(name);
            app.Styles[0] = Create(theme);

            bool mac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (mac && theme == Fluent && Environment.GetEnvironmentVariable("ZERO_NO_MAC_STYLES") != "1")
                app.Styles.Add(new StyleInclude(new Uri("avares://Zero/")) { Source = new Uri("avares://Zero/Styles/MacStyles.axaml") });
        }
    }
}
