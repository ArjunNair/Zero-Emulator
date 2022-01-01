using SpeccyCommon;
using Newtonsoft.Json;
using System.Text;

namespace ZeroWin
{
    public class PathSettings
    {
        public string Application { get; set; }
        public string Roms { get; set; }
        public string Programs { get; set; }
        public string Saves { get; set; }
        public string Screenshots { get; set; }
    }
    public class TapeSettings
    {
        public bool EdgeLoad { get; set; }
        public bool FastLoad { get; set; }
        public bool AutoPlay { get; set; }
        public bool AutoLoad { get; set; }
        public bool ROMTraps { get; set; }
    }

    public class ROMSettings
    {
        public string Current48kROM { get; set; }
        public string Current128kROM { get; set; }
        public string Current128keROM { get; set; }
        public string CurrentPlus3ROM{ get; set; }
        public string CurrentPentagonROM { get; set; }
    }

    public class RenderSettings {
        public bool FullScreenMode { get; set; }
        public bool MaintainAspectRatioInFullScreen { get; set; }
        public bool UseDirectX { get; set; }
        public bool PixelSmoothing { get; set; }
        public bool Scanlines { get; set; }
        public bool Vsync { get; set; }
        public string Palette { get; set; }
        public int BorderSize { get; set; }
        public int WindowSize { get; set; }
    }

    public class AudioSettings
    {
        public int Volume { get; set; }
        public bool Mute { get; set; }
        public bool EnableAYFor48K { get; set; }

        //Speaker setup: 0 = Mono, 1 = ACB, 2 = ABC
        public int StereoSoundMode { get; set; }

    }

    public class EmulationSettings
    {
        public bool Use128keForSnapshots { get; set; }
        public bool UseIssue2Keyboard { get; set; }
        public bool LateTimings { get; set; }
        public bool PauseOnFocusLost { get; set; }
        public bool ConfirmOnExit { get; set; }
        public bool RestorePreviousSessionOnStart { get; set; }
        public int CPUMultiplier { get; set; }
        public int EmulationSpeed { get; set; }
        public string CurrentModelName { get; set; }
        public MachineModel CurrentModel { get; set; }
    }

    public class InputDeviceSettings
    {
        public bool EnableKempstonMouse { get; set; }
        public bool EnableKey2Joy { get; set; }
        public bool KempstonUsesPort1F { get; set; }
        public int MouseSensitivity { get; set; }
        public int Key2JoystickType { get; set; }
        public int Joystick1ToEmulate { get; set; }
        public int Joystick2ToEmulate { get; set; }
        public string Joystick1Name { get; set; }
        public string Joystick2Name { get; set; }
    }

    public class FileAssociationSettings
    {
        public bool AccociateCSWFiles { get; set; }
        public bool AccociatePZXFiles { get; set; }
        public bool AccociateTZXFiles { get; set; }
        public bool AccociateTAPFiles { get; set; }
        public bool AccociateSNAFiles { get; set; }
        public bool AccociateSZXFiles { get; set; }
        public bool AccociateZ80Files { get; set; }
        public bool AccociateDSKFiles { get; set; }
        public bool AccociateTRDFiles { get; set; }
        public bool AccociateSCLFiles { get; set; }
    }

    public class RecentFiles
    {
        public System.Collections.Generic.List<string> files = new System.Collections.Generic.List<string>();
    }
    public class ZeroConfig
    {
        #region properties
        public PathSettings pathOptions = new PathSettings();
        public TapeSettings tapeOptions = new TapeSettings();
        public ROMSettings romOptions = new ROMSettings();
        public RenderSettings renderOptions = new RenderSettings();
        public AudioSettings audioOptions = new AudioSettings();
        public EmulationSettings emulationOptions = new EmulationSettings();
        public InputDeviceSettings inputDeviceOptions = new InputDeviceSettings();
        public FileAssociationSettings fileAssociationOptions = new FileAssociationSettings();
        public RecentFiles recentFiles = new RecentFiles();
        public void Default() {
            pathOptions.Application = "";
            pathOptions.Roms = @"\roms\";
            pathOptions.Saves = @"\saves\";
            pathOptions.Programs =  @"\programs\";

            tapeOptions.EdgeLoad = true;
            tapeOptions.AutoLoad = false;
            tapeOptions.AutoPlay = true;
            tapeOptions.FastLoad = false;
            tapeOptions.ROMTraps = true;

            romOptions.Current48kROM = @"48k.rom";
            romOptions.Current128kROM = @"128k.rom";
            romOptions.Current128keROM = @"128ke.rom";
            romOptions.CurrentPlus3ROM = @"plus3.rom";
            romOptions.CurrentPentagonROM = @"pentagon.rom";

            renderOptions.BorderSize = 0;
            renderOptions.FullScreenMode = false;
            renderOptions.MaintainAspectRatioInFullScreen = true;
            renderOptions.Palette = "Normal";
            renderOptions.PixelSmoothing = true;
            renderOptions.Scanlines = false;
            renderOptions.Vsync = false;
            renderOptions.UseDirectX = true;
            renderOptions.WindowSize = 100; //200%

            audioOptions.EnableAYFor48K = false;
            audioOptions.Mute = false;
            audioOptions.StereoSoundMode = 1;
            audioOptions.Volume = 50;

            emulationOptions.ConfirmOnExit = true;
            emulationOptions.EmulationSpeed = 1;
            emulationOptions.CPUMultiplier = 1;
            emulationOptions.LateTimings = false;
            emulationOptions.PauseOnFocusLost = true;
            emulationOptions.RestorePreviousSessionOnStart = false;
            emulationOptions.Use128keForSnapshots = false;
            emulationOptions.UseIssue2Keyboard = false;
            emulationOptions.CurrentModelName = "ZX Spectrum 48k";
            emulationOptions.CurrentModel = MachineModel._48k;

            inputDeviceOptions = new InputDeviceSettings();
            inputDeviceOptions.MouseSensitivity = 3;
            inputDeviceOptions.KempstonUsesPort1F = true;
            fileAssociationOptions = new FileAssociationSettings();
        }

        /*
        private string applicationPath = "";

        public string ApplicationPath {
            get { return applicationPath; }
            set { applicationPath = value; }
        }

        private bool use128keForSnapshots = false;

        public bool HighCompatibilityMode {
            get { return use128keForSnapshots; }
            set { use128keForSnapshots = value; }
        }

        private bool showInterlace = false;

        public bool EnableInterlacedOverlay {
            get { return showInterlace; }
            set { showInterlace = value; }
        }

        private MachineModel model = MachineModel._48k;

        public MachineModel Model {
            get { return model; }
            set { model = value; }
        }

        private int volume = 50;

        public int Volume {
            get { return volume; }
            set { volume = value; }
        }

        private bool mute = false;

        public bool MuteSound {
            get { return mute; }
            set { mute = value; }
        }

        //Speaker setup: 0 = Mono, 1 = ACB, 2 = ABC
        private int stereoSoundOption = 1;

        public int StereoSoundOption {
            get { return stereoSoundOption; }
            set { stereoSoundOption = value; }
        }

        private int windowSize = 0;

        public int WindowSize {
            get { return windowSize; }
            set { windowSize = value; }
        }

        private int fullScreenWidth = 800;

        public int FullScreenWidth {
            get { return fullScreenWidth; }
            set { fullScreenWidth = value; }
        }
        private int fullScreenHeight = 600;

        public int FullScreenHeight {
            get { return fullScreenHeight; }
            set { fullScreenHeight = value; }
        }

        private bool fullScreenFormat16 = false;

        public bool FullScreenFormat16 {
            get { return fullScreenFormat16; }
            set { fullScreenFormat16 = value; }
        }

        private bool fullScreen = false;

        public bool FullScreen {
            get { return fullScreen; }
            set { fullScreen = value; }
        }

        //private int interpolationMode = 0;
        private string romPath = @"\roms\";

        public string PathRoms {
            get { return romPath; }
            set { romPath = value; }
        }

        private string gamePath = @"\programs\";

        public string PathGames {
            get { return gamePath; }
            set { gamePath = value; }
        }

        private string gameSavePath = @"\saves\";

        public string PathGameSaves {
            get { return gameSavePath; }
            set { gameSavePath = value; }
        }

        private string screenshotPath = @"\screenshots\";

        public string PathScreenshots {
            get { return screenshotPath; }
            set { screenshotPath = value; }
        }

        private string gameCheatPath = @"\cheats\";

        public string PathCheats {
            get { return gameCheatPath; }
            set { gameCheatPath = value; }
        }

        private string gameInfoPath = @"\info\";

        public string PathInfos {
            get { return gameInfoPath; }
            set { gameInfoPath = value; }
        }

        private string current48kRom = @"48k.rom";

        public string Current48kROM {
            get { return current48kRom; }
            set { current48kRom = value; }
        }

        public string current128kRom = @"128k.rom";

        public string Current128kROM {
            get { return current128kRom; }
            set { current128kRom = value; }
        }

        public string current128keRom = @"128ke.rom";

        public string Current128keROM {
            get { return current128keRom; }
            set { current128keRom = value; }
        }

        public string currentPlus3Rom = @"plus3.rom";

        public string CurrentPlus3ROM {
            get { return currentPlus3Rom; }
            set { currentPlus3Rom = value; }
        }

        public string currentPentagonRom = @"pentagon.rom";

        public string CurrentPentagonROM {
            get { return currentPentagonRom; }
            set { currentPentagonRom = value; }
        }

        public string currentModel = "ZX Spectrum 48k";

        public string CurrentSpectrumModel {
            get { return currentModel; }
            set { currentModel = value; }
        }

        private string paletteMode = "Normal";

        public string PaletteMode {
            get { return paletteMode; }
            set { paletteMode = value; }
        }

        private int emulationSpeed = 100;

        public int EmulationSpeed {
            get { return emulationSpeed; }
            set { emulationSpeed = value; }
        }

        private bool lateTiming = false;

        public bool UseLateTimings {
            get { return lateTiming; }
            set { lateTiming = value; }
        }

        private bool issue2keyboard = false;

        public bool UseIssue2Keyboard {
            get { return issue2keyboard; }
            set { issue2keyboard = value; }
        }

        private bool useDirectX = true;

        public bool UseDirectX {
            get { return useDirectX; }
            set { useDirectX = value; }
        }

        private bool fileAssociateCSW = false;

        public bool AccociateCSWFiles {
            get { return fileAssociateCSW; }
            set { fileAssociateCSW = value; }
        }

        private bool fileAssociatePZX = false;

        public bool AccociatePZXFiles {
            get { return fileAssociatePZX; }
            set { fileAssociatePZX = value; }
        }

        private bool fileAssociateTZX = false;

        public bool AccociateTZXFiles {
            get { return fileAssociateTZX; }
            set { fileAssociateTZX = value; }
        }

        private bool fileAssociateTAP = false;

        public bool AccociateTAPFiles {
            get { return fileAssociateTAP; }
            set { fileAssociateTAP = value; }
        }

        private bool fileAssociateSNA = false;

        public bool AccociateSNAFiles {
            get { return fileAssociateSNA; }
            set { fileAssociateSNA = value; }
        }

        private bool fileAssociateSZX = false;

        public bool AccociateSZXFiles {
            get { return fileAssociateSZX; }
            set { fileAssociateSZX = value; }
        }

        private bool fileAssociateZ80 = false;

        public bool AccociateZ80Files {
            get { return fileAssociateZ80; }
            set { fileAssociateZ80 = value; }
        }

        private bool fileAssociateDSK = false;

        public bool AccociateDSKFiles {
            get { return fileAssociateDSK; }
            set { fileAssociateDSK = value; }
        }

        private bool fileAssociateTRD = false;

        public bool AccociateTRDFiles {
            get { return fileAssociateTRD; }
            set { fileAssociateTRD = value; }
        }

        private bool fileAssociateSCL = false;

        public bool AccociateSCLFiles {
            get { return fileAssociateSCL; }
            set { fileAssociateSCL = value; }
        }

        private bool pauseOnFocusChange = true;

        public bool PauseOnFocusLost {
            get { return pauseOnFocusChange; }
            set { pauseOnFocusChange = value; }
        }

        private bool confirmOnExit = true;

        public bool ConfirmOnExit {
            get { return confirmOnExit; }
            set { confirmOnExit = value; }
        }

        private bool soundOn = true;

        public bool EnableSound {
            get { return soundOn; }
            set { soundOn = value; }
        }

        private bool fullSpeed = false;

        public bool FullSpeedEmulation {
            get { return fullSpeed; }
            set { fullSpeed = value; }
        }

        private int borderWidthAdjust = 0;

        public int BorderSize {
            get { return borderWidthAdjust; }
            set { borderWidthAdjust = value; }
        }

        private bool ayFor48K = false;

        public bool EnableAYFor48K {
            get { return ayFor48K; }
            set { ayFor48K = value; }
        }

        //Tape Deck settings
        private bool tapeAutoStart = true;

        public bool TapeAutoStart {
            get { return tapeAutoStart; }
            set { tapeAutoStart = value; }
        }

        private bool tapeAutoLoad = true;

        public bool TapeAutoLoad {
            get { return tapeAutoLoad; }
            set { tapeAutoLoad = value; }
        }

        private bool tapeEdgeLoad = true;

        public bool TapeEdgeLoad {
            get { return tapeEdgeLoad; }
            set { tapeEdgeLoad = value; }
        }

        private bool tapeAccelerateLoad = true;

        public bool TapeAccelerateLoad {
            get { return tapeAccelerateLoad; }
            set { tapeAccelerateLoad = value; }
        }

        private bool tapeInstaLoad = true;

        public bool TapeInstaLoad {
            get { return tapeInstaLoad; }
            set { tapeInstaLoad = value; }
        }

        private bool disableTapeTraps = false;

        public bool DisableTapeTraps
        {
            get { return disableTapeTraps; }
            set { disableTapeTraps = value; }
        }

        private bool enableKempstonMouse = false;

        public bool EnableKempstonMouse {
            get { return enableKempstonMouse; }
            set { enableKempstonMouse = value; }
        }

        private int mouseSensitivity = 3;

        public int MouseSensitivity {
            get { return mouseSensitivity; }
            set { mouseSensitivity = value; }
        }

        private bool key2joyEnabled = false;

        public bool EnableKey2Joy {
            get { return key2joyEnabled; }
            set { key2joyEnabled = value; }
        }

        private int key2joystick = 0;

        public int Key2JoystickType {
            get { return key2joystick; }
            set { key2joystick = value; }
        }

        private int joystick1Emulate = 0;

        public int Joystick1ToEmulate {
            get { return joystick1Emulate; }
            set { joystick1Emulate = value; }
        }

        private int joystick2Emulate = 0;

        public int Joystick2ToEmulate {
            get { return joystick2Emulate; }
            set { joystick2Emulate = value; }
        }

        private string joystick1 = "";
        public string Joystick1Name {
            get { return joystick1; }
            set { joystick1 = value; }
        }

        private string joystick2 = "";
        public string Joystick2Name {
            get { return joystick2; }
            set { joystick2 = value; }
        }

        public bool KempstonUsesPort1F
        {
            get;
            set;
        }

        private bool onscreenLED = true;

        public bool ShowOnscreenIndicators {
            get { return onscreenLED; }
            set { onscreenLED = value; }
        }

        private bool restoreLastStateOnStart = false;

        public bool RestoreLastStateOnStart {
            get { return restoreLastStateOnStart; }
            set { restoreLastStateOnStart = value; }
        }

        private bool pixelSmoothing = false;

        public bool EnablePixelSmoothing {
            get { return pixelSmoothing; }
            set { pixelSmoothing = value; }
        }

        private bool enableVsync = false;

        public bool EnableVSync {
            get { return enableVsync; }
            set { enableVsync = value; }
        }

        private bool maintainAspectRatioInFullScreen = true;

        public bool MaintainAspectRatioInFullScreen
        {
            get { return maintainAspectRatioInFullScreen; }
            set { maintainAspectRatioInFullScreen = value; }
        }
        */
        #endregion properties
        public void Load(string path) {
            if (!System.IO.File.Exists(path + "\\zero_config.json")) {
                Default();
                return;
            }

            string json = System.IO.File.ReadAllText(path + "\\zero_config.json");
            ZeroConfig cfg = JsonConvert.DeserializeObject<ZeroConfig>(json);
            this.audioOptions = cfg.audioOptions;
            this.emulationOptions = cfg.emulationOptions;
            this.fileAssociationOptions = cfg.fileAssociationOptions;
            this.inputDeviceOptions = cfg.inputDeviceOptions;
            this.pathOptions = cfg.pathOptions;
            this.renderOptions = cfg.renderOptions;
            this.romOptions = cfg.romOptions;
            this.tapeOptions = cfg.tapeOptions;
            this.recentFiles = cfg.recentFiles;

        }

        public void Save(string path) {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            System.IO.File.WriteAllText(path + "\\zero_config.json", json);
        }
        /*
        public void Load() {

            Properties.Settings.Default.Reload();
            current48kRom = Properties.Settings.Default.ROM48k;
            current128kRom = Properties.Settings.Default.ROM128k;
            current128keRom = Properties.Settings.Default.ROM128ke;
            currentPlus3Rom = Properties.Settings.Default.ROMPlus3;
            currentPentagonRom = Properties.Settings.Default.ROMPentagon;
            romPath = Properties.Settings.Default.PathROM;
            gamePath = Properties.Settings.Default.PathPrograms;
            if (gamePath == "") {
                gamePath = System.Windows.Forms.Application.StartupPath + "\\programs";
            }
            screenshotPath = Properties.Settings.Default.PathScreenshots;
            gameSavePath = Properties.Settings.Default.PathSaves;
            gameInfoPath = Properties.Settings.Default.PathInfos;
            gameCheatPath = Properties.Settings.Default.PathCheats;
            currentModel = Properties.Settings.Default.Model;
            useDirectX = Properties.Settings.Default.UseDirectX;
            fullScreen = Properties.Settings.Default.StartFullscreen;
            maintainAspectRatioInFullScreen = Properties.Settings.Default.MaintainAspectRatioInFullScreen;
            windowSize = (int)Properties.Settings.Default.WindowSize;
            fullScreenWidth = Properties.Settings.Default.FullScreenWidth;
            fullScreenHeight = Properties.Settings.Default.FullScreenHeight;
            fullScreenFormat16 = Properties.Settings.Default.FullScreenFormat16;
            windowSize = (int)Properties.Settings.Default.WindowSize;
            paletteMode = Properties.Settings.Default.Palette;
            borderWidthAdjust = (int)Properties.Settings.Default.BorderSize;
            showInterlace = Properties.Settings.Default.Interlaced;
            pixelSmoothing = Properties.Settings.Default.PixelSmoothing;
            enableVsync = Properties.Settings.Default.EnableVSync;
            volume = Properties.Settings.Default.Volume;
            mute = Properties.Settings.Default.Mute;
            ayFor48K = Properties.Settings.Default.AySoundFor48k;
            stereoSoundOption = Properties.Settings.Default.SpeakerSetup;
            use128keForSnapshots = Properties.Settings.Default.HighCompatabilityMode;
            pauseOnFocusChange = Properties.Settings.Default.PauseOnFocusChange;
            onscreenLED = Properties.Settings.Default.ShowOnScreenLEDs;
            restoreLastStateOnStart = Properties.Settings.Default.RestoreLastStateOnStart;
            confirmOnExit = Properties.Settings.Default.ConfirmOnExit;
            lateTiming = Properties.Settings.Default.TimingModel;
            issue2keyboard = Properties.Settings.Default.Issue2Keyboard;
            emulationSpeed = Properties.Settings.Default.EmulationSpeed;
            fileAssociateSZX = Properties.Settings.Default.FileAssociationSZX;
            fileAssociateSNA = Properties.Settings.Default.FileAssociationSNA;
            fileAssociateZ80 = Properties.Settings.Default.FileAssociationZ80;
            fileAssociateTZX = Properties.Settings.Default.FileAssociationTZX;
            fileAssociatePZX = Properties.Settings.Default.FileAssociationPZX;
            fileAssociateTAP = Properties.Settings.Default.FileAssociationTAP;
            fileAssociateDSK = Properties.Settings.Default.FileAssociationDSK;
            fileAssociateTRD = Properties.Settings.Default.FileAssociationTRD;
            fileAssociateSCL = Properties.Settings.Default.FileAssociationSCL;
            tapeAutoStart = Properties.Settings.Default.TapeAutoStart;
            tapeAutoLoad = Properties.Settings.Default.TapeAutoLoad;
            tapeEdgeLoad = Properties.Settings.Default.TapeEdgeLoad;
            tapeAccelerateLoad = Properties.Settings.Default.TapeAccelerateLoad;
            tapeInstaLoad = Properties.Settings.Default.TapeInstaLoad;
            disableTapeTraps = Properties.Settings.Default.DisableTapeTraps;
            enableKempstonMouse = Properties.Settings.Default.KempstonMouse;
            mouseSensitivity = (int)Properties.Settings.Default.MouseSensitivity;
            key2joyEnabled = Properties.Settings.Default.Key2Joy;
            key2joystick = (int)Properties.Settings.Default.Key2JoystickType;
            joystick1 = Properties.Settings.Default.joystick1;
            joystick2 = Properties.Settings.Default.joystick2;
            joystick1Emulate = (int)Properties.Settings.Default.joystick1ToEmulate;
            joystick2Emulate = (int)Properties.Settings.Default.joystick2ToEmulate;
            KempstonUsesPort1F = (bool)Properties.Settings.Default.KempstonUsesPort1F;
        }
        */
        #region Old XML file method

        /*
        public void Load2()
        {
            //Attempt to load settings from a custom file
            try
            {
                configXML = XElement.Load(ApplicationPath + @"\zeroConfig.xml");
            }
            catch //not found!
            {
                //Attempt to load from a default file
               // try
               // {
               //     configXML = XElement.Load(ApplicationPath + @"\zeroDefaultConfig.xml");
               // }
               // catch //default is missing, so create one from scratch.
                {
                    //System.Windows.Forms.MessageBox.Show("Zero couldn't find the default config file.\nIt will now attempt to create one.",
                    //       "Config file missing!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);

                    configXML = new XElement("config",
                                new XElement("model", currentModel),
                                new XElement("rom48k", current48kRom),
                                new XElement("rom128k", current128kRom),
                                new XElement("rom128ke", current128keRom),
                                new XElement("romPlus3", currentPlus3Rom),
                                new XElement("romPentagon", currentPentagonRom),
                                new XElement("paths",
                                    new XElement("roms", ApplicationPath + romPath),
                                    new XElement("programs", ApplicationPath + gamePath),
                                    new XElement("screenshots", ApplicationPath + screenshotPath),
                                    new XElement("saves", ApplicationPath + gameSavePath),
                                    new XElement("infos", ApplicationPath + gameInfoPath),
                                    new XElement("cheats", ApplicationPath + gameCheatPath)),
                                new XElement("display",
                                    new XElement("useDirectX", useDirectX),
                                    new XElement("startFullscreen", fullScreen),
                                    new XElement("windowSize", windowSize),
                                    new XElement("interlace", showInterlace),
                                    new XElement("pixelSmoothing", pixelSmoothing),
                                    new XElement("palette", paletteMode),
                                    new XElement("borderSize", borderWidthAdjust)),
                                new XElement("audio",
                                    new XElement("mute", mute),
                                    new XElement("volume", volume),
                                    new XElement("aySoundFor48k", ayFor48K),
                                    new XElement("speakerSetup", stereoSoundOption)),
                                new XElement("emulation",
                                    new XElement("issue2keyboard", issue2keyboard),
                                    new XElement("timingModel", lateTiming),
                                    new XElement("highCompatibilityMode", use128keForSnapshots),
                                    new XElement("pauseOnFocusChange", pauseOnFocusChange),
                                    new XElement("showOnScreenLEDS", onscreenLED),
                                    new XElement("restoreLastStateOnStart", restoreLastStateOnStart),
                                    new XElement("confirmOnExit", confirmOnExit),
                                    new XElement("emulationSpeed", emulationSpeed)),
                                new XElement("association",
                                    new XElement("szx", fileAssociateSZX),
                                    new XElement("sna", fileAssociateSNA),
                                    new XElement("z80", fileAssociateZ80),
                                    new XElement("tzx", fileAssociateTZX),
                                    new XElement("pzx", fileAssociatePZX),
                                    new XElement("tap", fileAssociateTAP),
                                    new XElement("dsk", fileAssociateDSK),
                                    new XElement("trd", fileAssociateTRD),
                                    new XElement("scl", fileAssociateSCL)),
                                new XElement("tapedeck",
                                    new XElement("autoStart", tapeAutoStart),
                                    new XElement("autoLoad", tapeAutoLoad),
                                    new XElement("edgeLoad", tapeEdgeLoad),
                                    new XElement("accelerateLoad", tapeAccelerateLoad)),
                                new XElement("controller",
                                    new XElement("kempstonMouse", enableKempstonMouse),
                                    new XElement("mouseSensitivity", mouseSensitivity),
                                    new XElement("key2joy", key2joyEnabled),
                                    new XElement("key2joystickType", key2joystick))
                                    );
                    //configXML.Save(ApplicationPath + @"\zeroDefaultConfig.xml");
                }
            }
            current48kRom = (string)configXML.Element("rom48k");
            current128kRom = (string)configXML.Element("rom128k");
            current128keRom = (string)configXML.Element("rom128ke");
            currentPlus3Rom = (string)configXML.Element("romPlus3");
            currentPentagonRom = (string)configXML.Element("romPentagon");
            romPath = (string)configXML.Element("paths").Element("roms");
            gamePath = (string)configXML.Element("paths").Element("programs");
            screenshotPath = (string)configXML.Element("paths").Element("screenshots");
            gameSavePath = (string)configXML.Element("paths").Element("saves");
            gameInfoPath = (string)configXML.Element("paths").Element("infos");
            gameCheatPath = (string)configXML.Element("paths").Element("cheats");
            currentModel = (string)configXML.Element("model");
            useDirectX = (bool)configXML.Element("display").Element("useDirectX");
            fullScreen = (bool)configXML.Element("display").Element("startFullscreen");
            windowSize = (int)configXML.Element("display").Element("windowSize");
            paletteMode = (string)configXML.Element("display").Element("palette");
            borderWidthAdjust = (int)configXML.Element("display").Element("borderSize");
            showInterlace = (bool)configXML.Element("display").Element("interlace");
            pixelSmoothing = (bool)configXML.Element("display").Element("pixelSmoothing");
            volume = (int)configXML.Element("audio").Element("volume");
            mute = (bool)configXML.Element("audio").Element("mute");
            ayFor48K = (bool)configXML.Element("audio").Element("aySoundFor48k");
            stereoSoundOption = (int)configXML.Element("audio").Element("speakerSetup");
            use128keForSnapshots = (bool)configXML.Element("emulation").Element("highCompatibilityMode");
            pauseOnFocusChange = (bool)configXML.Element("emulation").Element("pauseOnFocusChange");
            onscreenLED = (bool)configXML.Element("emulation").Element("showOnScreenLEDS");
            restoreLastStateOnStart = (bool)configXML.Element("emulation").Element("restoreLastStateOnStart");
            confirmOnExit = (bool)configXML.Element("emulation").Element("confirmOnExit");
            lateTiming = (bool)configXML.Element("emulation").Element("timingModel");
            issue2keyboard = (bool)configXML.Element("emulation").Element("issue2keyboard");
            emulationSpeed = (int)configXML.Element("emulation").Element("emulationSpeed");
            fileAssociateSZX = (bool)configXML.Element("association").Element("szx");
            fileAssociateSNA = (bool)configXML.Element("association").Element("sna");
            fileAssociateZ80 = (bool)configXML.Element("association").Element("z80");
            fileAssociateTZX = (bool)configXML.Element("association").Element("tzx");
            fileAssociatePZX = (bool)configXML.Element("association").Element("pzx");
            fileAssociateTAP = (bool)configXML.Element("association").Element("tap");
            fileAssociateDSK = (bool)configXML.Element("association").Element("dsk");
            fileAssociateTRD = (bool)configXML.Element("association").Element("trd");
            fileAssociateSCL = (bool)configXML.Element("association").Element("scl");
            tapeAutoStart = (bool)configXML.Element("tapedeck").Element("autoStart");
            tapeAutoLoad = (bool)configXML.Element("tapedeck").Element("autoLoad");
            tapeEdgeLoad = (bool)configXML.Element("tapedeck").Element("edgeLoad");
            tapeAccelerateLoad = (bool)configXML.Element("tapedeck").Element("accelerateLoad");
            enableKempstonMouse = (bool)configXML.Element("controller").Element("kempstonMouse");
            mouseSensitivity = (int)configXML.Element("controller").Element("mouseSensitivity");
            key2joyEnabled = (bool)configXML.Element("controller").Element("key2joy");
            key2joystick = (int)configXML.Element("controller").Element("key2joystickType");
        }
        */

        #endregion Old XML file method
        /*
        public void Save() {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            System.IO.File.WriteAllText(@"zero_config.json", json);

            Properties.Settings.Default.ROM48k = current48kRom;
            Properties.Settings.Default.ROM128k = current128kRom;
            Properties.Settings.Default.ROM128ke = current128keRom;
            Properties.Settings.Default.ROMPlus3 = currentPlus3Rom;
            Properties.Settings.Default.ROMPentagon = currentPentagonRom;
            Properties.Settings.Default.PathROM = romPath;
            Properties.Settings.Default.PathPrograms = gamePath;
            Properties.Settings.Default.PathScreenshots = screenshotPath;
            Properties.Settings.Default.PathSaves = gameSavePath;
            Properties.Settings.Default.PathInfos = gameInfoPath;
            Properties.Settings.Default.PathCheats = gameCheatPath;
            Properties.Settings.Default.Model = currentModel;
            Properties.Settings.Default.UseDirectX = useDirectX;
            Properties.Settings.Default.EnableVSync = enableVsync;
            Properties.Settings.Default.StartFullscreen = fullScreen;
            Properties.Settings.Default.MaintainAspectRatioInFullScreen = maintainAspectRatioInFullScreen;
            Properties.Settings.Default.WindowSize = (byte)windowSize;
            Properties.Settings.Default.FullScreenWidth = fullScreenWidth;
            Properties.Settings.Default.FullScreenHeight = fullScreenHeight;
            Properties.Settings.Default.FullScreenFormat16 = fullScreenFormat16;
            Properties.Settings.Default.Palette = paletteMode;
            Properties.Settings.Default.BorderSize = (byte)borderWidthAdjust;
            Properties.Settings.Default.Interlaced = showInterlace;
            Properties.Settings.Default.PixelSmoothing = pixelSmoothing;
            Properties.Settings.Default.Volume = (byte)volume;
            Properties.Settings.Default.Mute = mute;
            Properties.Settings.Default.AySoundFor48k = ayFor48K;
            Properties.Settings.Default.SpeakerSetup = (byte)stereoSoundOption;
            Properties.Settings.Default.HighCompatabilityMode = use128keForSnapshots;
            Properties.Settings.Default.PauseOnFocusChange = pauseOnFocusChange;
            Properties.Settings.Default.ShowOnScreenLEDs = onscreenLED;
            Properties.Settings.Default.RestoreLastStateOnStart = restoreLastStateOnStart;
            Properties.Settings.Default.ConfirmOnExit = confirmOnExit;
            Properties.Settings.Default.TimingModel = lateTiming;
            Properties.Settings.Default.Issue2Keyboard = issue2keyboard;
            Properties.Settings.Default.EmulationSpeed = emulationSpeed;
            Properties.Settings.Default.FileAssociationSZX = fileAssociateSZX;
            Properties.Settings.Default.FileAssociationSNA = fileAssociateSNA;
            Properties.Settings.Default.FileAssociationZ80 = fileAssociateZ80;
            Properties.Settings.Default.FileAssociationTZX = fileAssociateTZX;
            Properties.Settings.Default.FileAssociationPZX = fileAssociatePZX;
            Properties.Settings.Default.FileAssociationTAP = fileAssociateTAP;
            Properties.Settings.Default.FileAssociationDSK = fileAssociateDSK;
            Properties.Settings.Default.FileAssociationTRD = fileAssociateTRD;
            Properties.Settings.Default.FileAssociationSCL = fileAssociateSCL;
            Properties.Settings.Default.TapeAutoStart = tapeAutoStart;
            Properties.Settings.Default.TapeAutoLoad = tapeAutoLoad;
            Properties.Settings.Default.TapeEdgeLoad = tapeEdgeLoad;
            Properties.Settings.Default.TapeAccelerateLoad = tapeAccelerateLoad;
            Properties.Settings.Default.TapeInstaLoad = tapeInstaLoad;
            Properties.Settings.Default.DisableTapeTraps = disableTapeTraps;
            Properties.Settings.Default.KempstonMouse = enableKempstonMouse;
            Properties.Settings.Default.MouseSensitivity = (byte)mouseSensitivity;
            Properties.Settings.Default.Key2Joy = key2joyEnabled;
            Properties.Settings.Default.Key2JoystickType = (byte)key2joystick;
            Properties.Settings.Default.joystick1 = joystick1;
            Properties.Settings.Default.joystick2 = joystick2;
            Properties.Settings.Default.joystick1ToEmulate = joystick1Emulate;
            Properties.Settings.Default.joystick2ToEmulate = joystick2Emulate;
            Properties.Settings.Default.KempstonUsesPort1F = KempstonUsesPort1F;

            Properties.Settings.Default.Save();
        }
        */
        #region Old XML file method

        /*
        public void Save2()
        {
            configXML.Element("model").ReplaceNodes(currentModel);
            configXML.Element("rom48k").ReplaceNodes(current48kRom);
            configXML.Element("rom128k").ReplaceNodes(current128kRom);
            configXML.Element("rom128ke").ReplaceNodes(current128keRom);
            configXML.Element("romPlus3").ReplaceNodes(currentPlus3Rom);
            configXML.Element("romPentagon").ReplaceNodes(currentPentagonRom);
            configXML.Element("paths").ReplaceNodes(new XElement("roms", romPath),
                                                    new XElement("programs", gamePath),
                                                    new XElement("screenshots", screenshotPath),
                                                    new XElement("saves", gameSavePath),
                                                    new XElement("infos", gameInfoPath),
                                                    new XElement("cheats", gameCheatPath));
            configXML.Element("display").ReplaceNodes(new XElement("useDirectX", useDirectX),
                                                        new XElement("startFullscreen", fullScreen),
                                                        new XElement("windowSize", windowSize),
                                                        new XElement("interlace", showInterlace),
                                                        new XElement("pixelSmoothing", pixelSmoothing),
                                                        new XElement("palette", paletteMode),
                                                        new XElement("borderSize", borderWidthAdjust));
            configXML.Element("audio").ReplaceNodes(new XElement("mute", mute),
                                                    new XElement("volume", volume),
                                                    new XElement("aySoundFor48k", ayFor48K),
                                                    new XElement("speakerSetup", stereoSoundOption));
            configXML.Element("emulation").ReplaceNodes(new XElement("issue2keyboard", issue2keyboard),
                                                        new XElement("timingModel", lateTiming),
                                                        new XElement("highCompatibilityMode", use128keForSnapshots),
                                                        new XElement("pauseOnFocusChange", pauseOnFocusChange),
                                                        new XElement("showOnScreenLEDS", onscreenLED),
                                                       new XElement("restoreLastStateOnStart", restoreLastStateOnStart),
                                                        new XElement("confirmOnExit", confirmOnExit),
                                                        new XElement("emulationSpeed", emulationSpeed));
            configXML.Element("association").ReplaceNodes(new XElement("szx", fileAssociateSZX),
                                                           new XElement("sna", fileAssociateSNA),
                                                           new XElement("z80", fileAssociateZ80),
                                                           new XElement("tzx", fileAssociateTZX),
                                                           new XElement("pzx", fileAssociatePZX),
                                                           new XElement("tap", fileAssociateTAP),
                                                           new XElement("dsk", fileAssociateDSK),
                                                            new XElement("trd", fileAssociateTRD),
                                                             new XElement("scl", fileAssociateSCL));
            configXML.Element("tapedeck").ReplaceNodes(new XElement("autoStart", tapeAutoStart),
                                                          new XElement("autoLoad", tapeAutoLoad),
                                                          new XElement("edgeLoad", tapeEdgeLoad),
                                                          new XElement("accelerateLoad", tapeAccelerateLoad));
            configXML.Element("controller").ReplaceNodes( new XElement("kempstonMouse", enableKempstonMouse),
                                                            new XElement("mouseSensitivity", mouseSensitivity),
                                                           new XElement("key2joy", key2joyEnabled),
                                                           new XElement("key2joystickType", key2joystick));

            try
            {
                configXML.Save(ApplicationPath + @"\zeroConfig.xml");
            }
            catch
            {
                System.Windows.Forms.MessageBox.Show("There was an error while saving emulator configuration.\nYour changes were not saved.", "Config File Write Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }
        */

        #endregion Old XML file method
    }
}