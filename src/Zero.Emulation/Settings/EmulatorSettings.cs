using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpeccyCommon;

namespace Zero.Emulation.Settings
{
    public sealed class PathSettings
    {
        public string Roms { get; set; } = "roms";
        public string Programs { get; set; } = "programs";
        public string Saves { get; set; } = "saves";
        public string Screenshots { get; set; } = "screenshots";
    }

    public sealed class TapeSettings
    {
        public bool EdgeLoad { get; set; } = true;
        public bool FastLoad { get; set; } = false;
        public bool AutoPlay { get; set; } = true;
        public bool AutoLoad { get; set; } = false;
        public bool RomTraps { get; set; } = true;
    }

    public sealed class RomSettings
    {
        public string Rom48k { get; set; } = "48k.rom";
        public string Rom128k { get; set; } = "128k.rom";
        public string Rom128ke { get; set; } = "128ke.rom";
        public string RomPlus3 { get; set; } = "plus3.rom";
        public string RomPentagon { get; set; } = "pentagon.rom";

        public string For(MachineModel model)
        {
            switch (model)
            {
                case MachineModel._48k: return Rom48k;
                case MachineModel._128k: return Rom128k;
                case MachineModel._128ke: return Rom128ke;
                case MachineModel._plus3: return RomPlus3;
                case MachineModel._pentagon: return RomPentagon;
                default: throw new ArgumentOutOfRangeException(nameof(model), model, "Unsupported machine");
            }
        }
    }

    public sealed class RenderSettings
    {
        public bool FullScreen { get; set; }
        public bool MaintainAspectRatio { get; set; } = true;
        public bool PixelSmoothing { get; set; } = false;
        public bool Scanlines { get; set; }
        public bool Vsync { get; set; } = true;
        /// <summary>"Normal", "Grayscale" or "ULA Plus".</summary>
        public string Palette { get; set; } = "Normal";
        /// <summary>Border pixels to crop from each edge (0 = full border).</summary>
        public int BorderCrop { get; set; }
        /// <summary>Integer window scale factor.</summary>
        public int WindowScale { get; set; } = 2;
        /// <summary>UI theme for the windows and dialogs. Applied at start-up; unknown names fall back to the default.</summary>
        public string UiTheme { get; set; } = "Semi";
        public CrtSettings Crt { get; set; } = new CrtSettings();
    }

    /// <summary>
    /// CRT effects drawn over the emulated screen by Zero's own shader. Cosmetic only; they never
    /// touch emulation. Strengths run 0..1, and 0 switches an effect off entirely.
    /// </summary>
    public sealed class CrtSettings
    {
        public bool Enabled { get; set; }
        public bool Scanlines { get; set; } = true;
        public bool Vignette { get; set; } = true;
        public bool Flicker { get; set; }
        public bool Noise { get; set; }
        public double Curvature { get; set; } = 0.2;
        public double Glow { get; set; } = 0.35;
        public double GlassReflect { get; set; } = 0.2;
        /// <summary>Light from the picture spilling onto the surround, in place of plain black.</summary>
        public double EdgeLight { get; set; } = 0.5;
        /// <summary>
        /// Share of the window given to the housing the screen sits in, 0 for none. The picture is
        /// pushed in by this much to make room for it, so it is a trade against picture size.
        /// </summary>
        public double Bezel { get; set; } = 0.1;
    }

    public sealed class AudioSettings
    {
        public int Volume { get; set; } = 50;
        public bool Mute { get; set; }
        public bool EnableAYFor48K { get; set; }
        /// <summary>0 = mono, 1 = ACB, 2 = ABC.</summary>
        public int StereoSoundMode { get; set; } = 1;
    }

    public sealed class EmulationSettings
    {
        public bool Use128keForSnapshots { get; set; }
        public bool UseIssue2Keyboard { get; set; }
        public bool LateTimings { get; set; }
        public bool PauseOnFocusLost { get; set; } = true;
        public bool ConfirmOnExit { get; set; } = true;
        public bool RestorePreviousSessionOnStart { get; set; }
        public int CpuMultiplier { get; set; } = 1;
        public int EmulationSpeed { get; set; } = 1;
        public MachineModel Model { get; set; } = MachineModel._48k;
    }

    public sealed class InputSettings
    {
        public bool EnableKempstonMouse { get; set; }
        public bool EnableKeyboardJoystick { get; set; }
        public bool KempstonUsesPort1F { get; set; } = true;
        public int MouseSensitivity { get; set; } = 3;
        /// <summary>Which emulated joystick the cursor keys drive (JoysticksEmulated value).</summary>
        public int KeyboardJoystickType { get; set; } = 1;
        public int Gamepad1Emulates { get; set; } = 1;
        public int Gamepad2Emulates { get; set; } = 0;
        public string Gamepad1Name { get; set; } = "";
        public string Gamepad2Name { get; set; } = "";
        public Input.GamepadMapping Gamepad1Buttons { get; set; } = Input.GamepadMapping.Default();
        public Input.GamepadMapping Gamepad2Buttons { get; set; } = Input.GamepadMapping.Default();

        public Input.GamepadMapping BindingsFor(int pad) => pad == 0 ? Gamepad1Buttons : Gamepad2Buttons;
    }

    /// <summary>
    /// Zero's user settings, stored as JSON under <see cref="AppPaths.ConfigDirectory"/>.
    /// Replaces the WinForms ZeroConfig (Newtonsoft + registry paths).
    /// </summary>
    public sealed class EmulatorSettings
    {
        public PathSettings Paths { get; set; } = new PathSettings();
        public TapeSettings Tape { get; set; } = new TapeSettings();
        public RomSettings Roms { get; set; } = new RomSettings();
        public RenderSettings Render { get; set; } = new RenderSettings();
        public AudioSettings Audio { get; set; } = new AudioSettings();
        public EmulationSettings Emulation { get; set; } = new EmulationSettings();
        public InputSettings Input { get; set; } = new InputSettings();
        public List<string> RecentFiles { get; set; } = new List<string>();

        public const int MaxRecentFiles = 10;

        public static string DefaultFile => Path.Combine(AppPaths.ConfigDirectory, "zero_config.json");


        public static EmulatorSettings Load(string file = null)
        {
            file = file ?? DefaultFile;
            try
            {
                if (File.Exists(file))
                    return JsonSerializer.Deserialize(File.ReadAllText(file), SettingsJsonContext.Default.EmulatorSettings) ?? new EmulatorSettings();
            }
            catch (Exception)
            {
                // Corrupt config: fall back to defaults rather than refusing to start.
            }
            return new EmulatorSettings();
        }

        public void Save(string file = null)
        {
            file = file ?? DefaultFile;
            AppPaths.EnsureDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonSerializer.Serialize(this, SettingsJsonContext.Default.EmulatorSettings));
        }

        public void AddRecentFile(string path)
        {
            RecentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            RecentFiles.Insert(0, path);
            if (RecentFiles.Count > MaxRecentFiles)
                RecentFiles.RemoveRange(MaxRecentFiles, RecentFiles.Count - MaxRecentFiles);
        }
    }

    /// <summary>Source-generated JSON metadata: keeps settings working under PublishTrimmed.</summary>
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UseStringEnumConverter = true)]
    [JsonSerializable(typeof(EmulatorSettings))]
    internal sealed partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
