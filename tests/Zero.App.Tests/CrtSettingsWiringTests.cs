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

        [AvaloniaFact]
        public void Scanlines_do_not_follow_the_crt_switch()
        {
            // Two separate effects: scanlines on their own, with the rest of the CRT look off.
            var w = Open(new CrtSettings { Enabled = false, Scanlines = true, Curvature = 0.3, Glow = 0.4, Bezel = 0.04 });
            Controls.CrtShaderOptions o = w.Display.CrtOptions;
            Assert.True(o.Enabled);
            Assert.True(o.Scanlines > 0);
            Assert.Equal(0f, o.Curvature);
            Assert.Equal(0f, o.Glow);
            Assert.Equal(0f, o.Bezel);
            Assert.True(o.Any);

            // And the CRT look without scanlines.
            w.Session.Settings.Render.Crt.Enabled = true;
            w.Session.Settings.Render.Crt.Scanlines = false;
            w.ApplyCrtSettings();
            o = w.Display.CrtOptions;
            Assert.Equal(0f, o.Scanlines);
            Assert.True(o.Curvature > 0);
            w.Close();
        }

        [AvaloniaTheory]
        [InlineData(false, false, false)]   // none
        [InlineData(true, false, false)]    // smooth
        [InlineData(true, true, true)]      // sharp
        public void The_pixel_filter_reaches_the_display(bool smoothing, bool sharpEdges, bool expectShader)
        {
            // Sharp is the shader placing the sample, so the picture has to go through the shader for
            // it -- including with every CRT effect switched off, which is the case this covers.
            MainWindow.SettingsLoader = () =>
            {
                var s = new EmulatorSettings();
                s.Paths.Roms = TestPaths.RomDir;
                s.Emulation.PauseOnFocusLost = false;
                s.Audio.Mute = true;
                s.Render.Crt = new CrtSettings { Enabled = false, Scanlines = false };
                s.Render.PixelSmoothing = smoothing;
                s.Render.SharpPixelEdges = sharpEdges;
                return s;
            };
            var w = new MainWindow();
            w.Show();

            Assert.Equal(smoothing, w.Display.Smooth);
            Assert.Equal(sharpEdges, w.Display.SharpPixels);
            Assert.Equal(expectShader, w.Display.UseShader);
            w.Close();
        }
    }
}
