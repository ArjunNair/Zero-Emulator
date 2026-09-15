using Avalonia.Headless.XUnit;
using Xunit;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// That each CRT setting reaches the shader. The shader's own tests set its options directly, so
    /// they would pass just the same if nothing ever carried a setting into them.
    /// </summary>
    public class CrtSettingsWiringTests
    {
        private static MainWindow Open(CrtSettings crt)
        {
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                s.Render.Crt = crt;
                return s;
            };
            var w = new MainWindow();
            w.Show();
            return w;
        }

        [AvaloniaFact]
        public void Every_crt_setting_reaches_the_shader()
        {
            var crt = new CrtSettings
            {
                Enabled = true,
                Curvature = 0.31,
                Glow = 0.42,
                GlassReflect = 0.53,
                EdgeLight = 0.64,
                Bezel = 0.04,
                Diffuse = 0.62,
            };
            var w = Open(crt);
            Controls.CrtShaderOptions o = w.Display.CrtOptions;

            Assert.True(o.Enabled);
            Assert.Equal(0.31f, o.Curvature, 3);
            Assert.Equal(0.42f, o.Glow, 3);
            Assert.Equal(0.53f, o.Reflection, 3);
            Assert.Equal(0.64f, o.EdgeLight, 3);
            Assert.Equal(0.04f, o.Bezel, 3);
            Assert.Equal(0.62f, o.Diffuse, 3);
            w.Close();
        }

        [AvaloniaFact]
        public void Turning_the_effect_off_takes_the_housing_with_it()
        {
            // The housing is part of the effect, not a separate frame that outlives it.
            var w = Open(new CrtSettings { Enabled = false, Bezel = 0.04 });
            Assert.Equal(0f, w.Display.CrtOptions.Bezel);
            w.Close();
        }
    }
}
