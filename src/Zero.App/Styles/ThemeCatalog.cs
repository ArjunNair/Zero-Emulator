using System;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Themes.Simple;
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
        public const string Semi = "Semi";
        public const string Simple = "Simple";

        /// <summary>Selectable themes. The first is the default.</summary>
        public static readonly string[] Names = { Semi, Simple };

        public static string Default => Names[0];

        public static string Describe(string name)
        {
            switch (Normalise(name))
            {
                case Simple: return "Plain and compact, in the style of an older desktop application. Fits the most rows in the tape deck and button lists.";
                default: return "Zero's default: modern and roomy, with a light and a dark appearance.";
            }
        }

        /// <summary>Maps a stored or user-supplied name to a supported one, falling back to the default.</summary>
        public static string Normalise(string name)
        {
            foreach (string known in Names)
                if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
                    return known;
            return Default;
        }

        public static IStyle Create(string name)
        {
            switch (Normalise(name))
            {
                case Simple: return new SimpleTheme();
                default: return new SemiTheme();
            }
        }

        /// <summary>Install a theme as the application's base styles. Call before any window is created.</summary>
        public static void Apply(Application app, string name)
        {
            app.Styles.Insert(0, Create(Normalise(name)));
        }
    }
}
