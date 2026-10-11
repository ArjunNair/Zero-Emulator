using System.Collections.Generic;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Xunit;
using Zero.Emulation.Settings;
using Zero.TestSupport;

namespace Zero.App.Tests
{
    /// <summary>
    /// Command-line flags last one session. Saving compares each setting before the flags, after them,
    /// and now, and puts the pre-launch value back wherever a flag changed it and nothing has since.
    /// </summary>
    public class CommandLineOverrideTests
    {
        private static EmulatorSettings Base()
        {
            var s = new EmulatorSettings();
            s.Paths.Roms = TestPaths.RomDir;
            s.Emulation.PauseOnFocusLost = false;
            s.Audio.Mute = true;
            // CRT defaults to on. Start from off, as a user who has turned it off would, so that the
            // -cs and -cv flags actually change something for the tests to strip back out.
            s.Render.Crt.Enabled = false;
            s.Render.Crt.Scanlines = false;
            s.Render.Crt.Vignette = false;
            return s;
        }

        /// <summary>Run StripFlags over three states built from real settings objects.</summary>
        private static EmulatorSettings Strip(EmulatorSettings before, EmulatorSettings afterFlags, EmulatorSettings now)
        {
            JsonObject current = now.ToJson();
            MainWindow.StripFlags(before.ToJson(), afterFlags.ToJson(), current);
            return EmulatorSettings.FromJson(current);
        }

        [AvaloniaFact]
        public void A_flag_nobody_touched_is_not_saved()
        {
            EmulatorSettings before = Base(), flagged = Base();
            flagged.Emulation.LateTimings = true;                      // -lt

            EmulatorSettings saved = Strip(before, flagged, flagged);
            Assert.False(saved.Emulation.LateTimings);
        }

        [AvaloniaFact]
        public void A_flagged_setting_changed_by_hand_is_saved()
        {
            EmulatorSettings before = Base(), flagged = Base();
            flagged.Render.Palette = "Grayscale";                       // -pl:grayscale
            EmulatorSettings now = Base();
            now.Render.Palette = "Ula Plus";                            // then chosen from the View menu

            Assert.Equal("Ula Plus", Strip(before, flagged, now).Render.Palette);
        }

        [AvaloniaFact]
        public void Changes_made_during_the_session_without_a_flag_are_saved()
        {
            EmulatorSettings before = Base(), flagged = Base();
            flagged.Emulation.LateTimings = true;                       // -lt
            EmulatorSettings now = Base();
            now.Emulation.LateTimings = true;
            now.Render.BorderCrop = 48;                                 // unrelated change, no flag

            EmulatorSettings saved = Strip(before, flagged, now);
            Assert.Equal(48, saved.Render.BorderCrop);
            Assert.False(saved.Emulation.LateTimings);
        }

        [AvaloniaFact]
        public void Two_flags_sharing_a_setting_unwind_to_the_original()
        {
            EmulatorSettings before = Base(), flagged = Base();
            flagged.Render.Crt.Scanlines = true;                        // -cs
            flagged.Render.Crt.Vignette = true;                         // -cv
            flagged.Render.Crt.Enabled = true;                          // set by both

            EmulatorSettings saved = Strip(before, flagged, flagged);
            Assert.False(saved.Render.Crt.Enabled);
            Assert.False(saved.Render.Crt.Scanlines);
            Assert.False(saved.Render.Crt.Vignette);
        }

        [AvaloniaFact]
        public void A_flag_that_sets_two_settings_gives_both_back()
        {
            EmulatorSettings before = Base();
            before.Render.PixelSmoothing = true;
            before.Render.SharpPixelEdges = true;                       // saved filter: Sharp
            EmulatorSettings flagged = Base();
            flagged.Render.PixelSmoothing = true;
            flagged.Render.SharpPixelEdges = false;                     // -pf:smooth

            Assert.True(Strip(before, flagged, flagged).Render.SharpPixelEdges);
        }

        [AvaloniaFact]
        public void Full_screen_from_the_flag_is_not_remembered_but_a_hand_change_is()
        {
            EmulatorSettings before = Base(), flagged = Base();
            flagged.Render.FullScreen = true;                           // -fs

            Assert.False(Strip(before, flagged, flagged).Render.FullScreen);

            EmulatorSettings windowed = Base();                         // pressed F11 to leave it
            Assert.False(Strip(before, flagged, windowed).Render.FullScreen);
        }

        [AvaloniaFact]
        public void A_window_launched_with_flags_saves_the_settings_it_was_launched_with()
        {
            EmulatorSettings loaded = Base();
            loaded.Render.PixelSmoothing = true;
            loaded.Render.SharpPixelEdges = true;
            loaded.Render.WindowScale = 2;
            MainWindow.SettingsLoader = () => loaded;

            var saves = new List<EmulatorSettings>();
            MainWindow.SettingsSaver = saves.Add;
            try
            {
                var w = new MainWindow(new[] { "-lt", "-cs", "-pf:smooth", "-pl:grayscale", "-ws:3" });

                // The flags are live for the session...
                EmulatorSettings live = w.Session.Settings;
                Assert.True(live.Emulation.LateTimings);
                Assert.True(live.Render.Crt.Enabled);
                Assert.False(live.Render.SharpPixelEdges);
                Assert.Equal("Grayscale", live.Render.Palette);
                Assert.Equal(3, live.Render.WindowScale);

                w.SaveSettings();

                // ...but none of them reach the file.
                EmulatorSettings saved = Assert.Single(saves);
                Assert.False(saved.Emulation.LateTimings);
                Assert.False(saved.Render.Crt.Enabled);
                Assert.False(saved.Render.Crt.Scanlines);
                Assert.True(saved.Render.SharpPixelEdges);
                Assert.Equal("Normal", saved.Render.Palette);
                Assert.Equal(2, saved.Render.WindowScale);

                // And saving mid-session (Options, gamepads) must not switch them off.
                Assert.True(live.Emulation.LateTimings);
                Assert.Equal("Grayscale", live.Render.Palette);
            }
            finally
            {
                MainWindow.SettingsSaver = _ => { };
            }
        }
    }
}
