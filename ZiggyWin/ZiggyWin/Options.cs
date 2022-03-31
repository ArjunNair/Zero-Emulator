using System;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class Options : Form
    {
        private Form1 zwRef;
        private int currentModelIndex = -1;
        private string current48kRom = "";
        private string current128kRom = "";
        private string current128keRom = "";
        private string currentPlus3Rom = "";
        private string currentPentagonRom = "";
        private bool stereoSound = true;
        private bool ayFor48k = false;
        private int joy1 = 0;
        private int joy2 = 0;

        #region Accessors

        public bool EnableKey2Joy {
            get {
                return key2joyCheckBox.Checked;
            }
            set {
                key2joyCheckBox.Checked = value;
            }
        }

        public int Key2JoyStickType {
            get {
                return key2joyComboBox.SelectedIndex;
            }
            set {
                key2joyComboBox.SelectedIndex = value;
            }
        }

        public int MouseSensitivity {
            get {
                return (11 - mouseTrackBar.Value);
            }
            set {
                mouseTrackBar.Value = 11 - value;
            }
        }

        public bool EnableKempstonMouse {
            get { return mouseCheckBox.Checked; }
            set { mouseCheckBox.Checked = value; }
        }

        public int Joystick1Choice {
            get
            {
                return joystickComboBox1.SelectedIndex;
            }
            set {
                // joystickComboBox1.SelectedIndex = value;
                joy1 = value;
            }
        }

        public int Joystick2Choice {
            get {
                return joystick2ComboBox1.SelectedIndex;
            }
            set {
                // joystick2ComboBox1.SelectedIndex = value;
                joy2 = value;
            }
        }

        public int Joystick1EmulationChoice {
            get {
                return joystickComboBox2.SelectedIndex;
            }
            set {
                joystickComboBox2.SelectedIndex = value;
            }
        }

        public int Joystick2EmulationChoice {
            get {
                return joystick2ComboBox2.SelectedIndex;
            }
            set {
                joystick2ComboBox2.SelectedIndex = value;
            }
        }

        public bool HighCompatibilityMode {
            get { return Use128keCheckbox.Checked; }
            set { Use128keCheckbox.Checked = value; }
        }

        public bool RestoreLastState {
            get { return lastStateCheckbox.Checked; }
            set { lastStateCheckbox.Checked = value; }
        }

        public bool ShowOnScreenLEDS {
            get { return onScreenLEDCheckbox.Checked; }
            set { onScreenLEDCheckbox.Checked = value; }
        }

        public int SpeakerSetup {
            get {
                if (!stereoRadioButton.Checked)
                    return 0;

                if (acbRadioButton.Checked)
                    return 1;

                return 2;
            }
            set {
                if (value == 0) {
                    stereoRadioButton.Checked = false;
                    monoRadioButton.Checked = true;
                } else {
                    stereoRadioButton.Checked = true;
                    monoRadioButton.Checked = false;

                    if (value == 1) {
                        acbRadioButton.Checked = true;
                        abcRadioButton.Checked = false;
                    } else {
                        acbRadioButton.Checked = false;
                        abcRadioButton.Checked = true;
                    }
                }
            }
        }

        public bool EnableStereoSound {
            get { return stereoSound; }
            set {
                stereoSound = value;
                stereoRadioButton.Checked = value;
                monoRadioButton.Checked = !value;
            }
        }

        public bool EnableAYFor48K {
            get { return ayFor48k; }
            set {
                ayFor48k = value;
                ayFor48kCheckbox.Checked = value;
            }
        }

        public String RomPath {
            get { return romPathTextBox.Text; }
            set { romPathTextBox.Text = value; }
        }

        public String GamePath {
            get { return gamePathTextBox.Text; }
            set { gamePathTextBox.Text = value; }
        }

        public String RomToUse48k {
            get { return current48kRom; }
            set { current48kRom = value; }
        }

        public String RomToUse128k {
            get { return current128kRom; }
            set { current128kRom = value; }
        }

        public String RomToUse128ke {
            get { return current128keRom; }
            set { current128keRom = value; }
        }

        public String RomToUsePlus3 {
            get { return currentPlus3Rom; }
            set { currentPlus3Rom = value; }
        }

        public String RomToUsePentagon {
            get { return currentPentagonRom; }
            set { currentPentagonRom = value; }
        }

        public bool FileAssociateSNA {
            get { return snaCheckBox.Checked; }
            set { snaCheckBox.Checked = value; }
        }

        public bool FileAssociateSZX {
            get { return szxCheckBox.Checked; }
            set { szxCheckBox.Checked = value; }
        }

        public bool FileAssociateZ80 {
            get { return z80CheckBox.Checked; }
            set { z80CheckBox.Checked = value; }
        }

        public bool FileAssociatePZX {
            get { return pzxCheckBox.Checked; }
            set { pzxCheckBox.Checked = value; }
        }

        public bool FileAssociateTZX {
            get { return tzxCheckBox.Checked; }
            set { tzxCheckBox.Checked = value; }
        }

        public bool FileAssociateTAP {
            get { return tapCheckBox.Checked; }
            set { tapCheckBox.Checked = value; }
        }

        public bool FileAssociateDSK {
            get { return dskCheckBox.Checked; }
            set { dskCheckBox.Checked = value; }
        }

        public bool FileAssociateTRD {
            get { return trdCheckBox.Checked; }
            set { trdCheckBox.Checked = value; }
        }

        public bool FileAssociateSCL {
            get { return sclCheckBox.Checked; }
            set { sclCheckBox.Checked = value; }
        }

        public int SpectrumModel {
            get { return modelComboBox.SelectedIndex; }
            set { modelComboBox.SelectedIndex = value; }
        }

        public bool InterlacedMode {
            get { return interlaceCheckBox.Checked; }
            set { interlaceCheckBox.Checked = value; }
        }

        public bool PixelSmoothing {
            get { return pixelSmoothingCheckBox.Checked; }
            set { pixelSmoothingCheckBox.Checked = value; }
        }

        public bool EnableVSync {
            get { return vsyncCheckbox.Checked; }
            set { vsyncCheckbox.Checked = value; }
        }

        public bool UseIssue2Keyboard {
            get {
                if (issue2RadioButton.Checked)
                    return true;
                return false;
            }
            set {
                if (value == true) {
                    issue2RadioButton.Checked = true;
                    issue3radioButton.Checked = false;
                } else {
                    issue2RadioButton.Checked = false;
                    issue3radioButton.Checked = true;
                };
            }
        }

        public bool UseDirectX {
            get {
                return directXRadioButton.Checked;
            }
            set {
                if (value == true) {
                    directXRadioButton.Checked = true;
                    gdiRadioButton.Checked = false;
                    interlaceCheckBox.Enabled = true;
                    pixelSmoothingCheckBox.Enabled = true;
                    vsyncCheckbox.Enabled = true;
                } else {
                    directXRadioButton.Checked = false;
                    gdiRadioButton.Checked = true;
                    interlaceCheckBox.Enabled = false;
                    pixelSmoothingCheckBox.Enabled = false;
                    vsyncCheckbox.Enabled = false;
                }
            }
        }

        public int Palette {
            get { return paletteComboBox.SelectedIndex; }
            set { paletteComboBox.SelectedIndex = value; }
        }

        public int borderSize {
            get { return borderSizeComboBox.SelectedIndex; }
            set { borderSizeComboBox.SelectedIndex = value; }
        }

        public int windowSize {
            get { return windowSizeComboBox.SelectedIndex; }
            set { windowSizeComboBox.SelectedIndex = value; }
        }

        public bool UseLateTimings {
            get { return timingCheckBox.Checked; }
            set { timingCheckBox.Checked = value; }
        }

        public bool PauseOnFocusChange {
            get { return pauseCheckBox.Checked; }
            set { pauseCheckBox.Checked = value; }
        }

        public bool ConfirmOnExit {
            get { return exitConfirmCheckBox.Checked; }
            set { exitConfirmCheckBox.Checked = value; }
        }

        public bool MaintainAspectRatioInFullScreen
        {
            get { return aspectRatioFullscreenCheckBox.Checked; }
            set { aspectRatioFullscreenCheckBox.Checked = value; }
        }

        public bool DisableTapeTraps
        {
            get { return disableTapeTrapCheckbox.Checked; }
            set { disableTapeTrapCheckbox.Checked = value; }
        }

        public bool KempstonUsesPort1F
        {
            get { return port1FCheckbox.Checked; }
            set { port1FCheckbox.Checked = value; }
        }
        #endregion Accessors

        public Options(Form1 parentRef) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            zwRef = parentRef;
        }

        private void Options_Load(object sender, EventArgs e) {
            this.Location = new System.Drawing.Point(zwRef.Location.X + 20, zwRef.Location.Y + 20);
            if (UseDirectX) {
                interlaceCheckBox.Enabled = true;
                pixelSmoothingCheckBox.Enabled = true;
            } else {
                interlaceCheckBox.Enabled = false;
                pixelSmoothingCheckBox.Enabled = false;
            }
            button1.Enabled = Joystick1Choice > 0;
            button2.Enabled = Joystick2Choice > 0;
        }

        private void romBrowseButton_Click(object sender, EventArgs e) {
            openFileDialog1.InitialDirectory = RomPath;
            openFileDialog1.Title = "Choose a ROM";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "All supported files|*.rom;";

            if (openFileDialog1.ShowDialog() == DialogResult.OK) {
                romTextBox.Text = openFileDialog1.SafeFileName;
                RomPath = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);

                switch (currentModelIndex) {
                    case 0:
                        current48kRom = romTextBox.Text;
                        break;

                    case 1:
                        current128kRom = romTextBox.Text;
                        break;

                    case 2:
                        current128keRom = romTextBox.Text;
                        break;

                    case 3:
                        currentPlus3Rom = romTextBox.Text;
                        break;

                    case 4:
                        currentPentagonRom = romTextBox.Text;
                        break;
                }
            }
        }

        private void gamePathButton_Click(object sender, EventArgs e) {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK) {
                GamePath = folderBrowserDialog1.SelectedPath;
            }
        }

        private void romPathButton_Click(object sender, EventArgs e) {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK) {
                String romInUse = "";
                switch (currentModelIndex) {
                    case 0:
                        romInUse = RomToUse48k;
                        break;

                    case 1:
                        romInUse = RomToUse128k;
                        break;

                    case 2:
                        romInUse = RomToUse128ke;
                        break;

                    case 3:
                        romInUse = RomToUsePlus3;
                        break;

                    case 4:
                        romInUse = RomToUsePentagon;
                        break;
                }
                if (!System.IO.File.Exists(folderBrowserDialog1.SelectedPath + "\\" + romInUse)) {
                    System.Windows.Forms.MessageBox.Show("The current ROM couldn't be found in this path.\n\nEnsure this path is correct, or specify a new ROM \nin the Hardware section.",
                             "File Warning", System.Windows.Forms.MessageBoxButtons.OK,
                             System.Windows.Forms.MessageBoxIcon.Warning);
                }

                RomPath = folderBrowserDialog1.SelectedPath;
            }
        }

        private void defaultSettingsButton_Click(object sender, EventArgs e) {
            if (System.Windows.Forms.MessageBox.Show("This will cause you to lose all your current settings!\nAre you sure you want to revert to default settings?",
                          "Confirm settings reset", System.Windows.Forms.MessageBoxButtons.YesNo,
                          System.Windows.Forms.MessageBoxIcon.Question) == DialogResult.Yes) {

                #region old default method

                /*
                 System.Xml.Linq.XElement configXML = System.Xml.Linq.XElement.Load(Application.StartupPath + @"\ziggyDefaultConfig.xml");
                RomToUse48k = (string)configXML.Element("rom48k");
                RomToUse128k = (string)configXML.Element("rom128k");
                RomToUse128ke = (string)configXML.Element("rom128ke");

                //Don't revert path to defaults, since there is no default path.
                //Instead try to use application start up path.
                RomPath = Application.StartupPath + "\\roms";
                GamePath = Application.StartupPath + "\\programs";
                string model = (string)configXML.Element("model");
                switch (model)
                {
                    case "ZX Spectrum 48k":
                        SpectrumModel = 0;
                        break;

                    case "ZX Spectrum 128k":
                        SpectrumModel = 1;
                        break;

                    case "ZX Spectrum 128ke":
                        SpectrumModel = 2;
                        break;

                    case "ZX Spectrum +3":
                        SpectrumModel = 3;
                        break;

                    case "Pentagon 128k":
                        SpectrumModel = 4;
                        break;
                }
                UseDirectX = (bool)configXML.Element("display").Element("useDirectX");
                borderSize = (int)configXML.Element("display").Element("borderSize");

                string paletteMode = (string)configXML.Element("display").Element("palette");
                switch (paletteMode)
                {
                    case "Grayscale":
                        Palette = 1;
                        break;

                    case "ULA Plus":
                        Palette = 2;
                        break;

                    default:
                        Palette = 0;
                        break;
                }

                PauseOnFocusChange = (bool)configXML.Element("emulation").Element("pauseOnFocusChange");
                ConfirmOnExit = (bool)configXML.Element("emulation").Element("confirmOnExit");
                UseLateTimings = ((int)configXML.Element("emulation").Element("timingModel") == 0? false: true);
                UseIssue2Keyboard = (bool)configXML.Element("emulation").Element("issue2keyboard");
                 */

                #endregion old default method

                zwRef.config.Default();
                RomToUse48k = zwRef.config.romOptions.Current48kROM;
                RomToUse128k = zwRef.config.romOptions.Current128kROM;
                RomToUse128ke = zwRef.config.romOptions.Current128keROM;
                RomToUsePlus3 = zwRef.config.romOptions.CurrentPlus3ROM;
                RomToUsePentagon = zwRef.config.romOptions.CurrentPentagonROM;
                //Don't revert path to defaults, since there is no default path.
                //Instead try to use application start up path.
                RomPath = Application.StartupPath + "\\roms";
                GamePath = Application.StartupPath + "\\programs";
                string model = zwRef.config.emulationOptions.CurrentModelName;
                switch (model) {
                    case "ZX Spectrum 48k":
                        SpectrumModel = 0;
                        break;

                    case "ZX Spectrum 128k":
                        SpectrumModel = 1;
                        break;

                    case "ZX Spectrum 128ke":
                        SpectrumModel = 2;
                        break;

                    case "ZX Spectrum +3":
                        SpectrumModel = 3;
                        break;

                    case "Pentagon 128k":
                        SpectrumModel = 4;
                        break;
                }
                //UseDirectX = zwRef.config.UseDirectX;
                borderSize = zwRef.config.renderOptions.BorderSize;

                string paletteMode = zwRef.config.renderOptions.Palette;
                switch (paletteMode) {
                    case "Grayscale":
                        Palette = 1;
                        break;

                    case "ULA Plus":
                        Palette = 2;
                        break;

                    default:
                        Palette = 0;
                        break;
                }
                PauseOnFocusChange = zwRef.config.emulationOptions.PauseOnFocusLost;
                ConfirmOnExit = zwRef.config.emulationOptions.ConfirmOnExit;
                UseLateTimings = zwRef.config.emulationOptions.LateTimings;
                UseIssue2Keyboard = zwRef.config.emulationOptions.UseIssue2Keyboard;
                RestoreLastState = zwRef.config.emulationOptions.RestorePreviousSessionOnStart;
                //ShowOnScreenLEDS = defCon.ShowOnscreenIndicators;
                Use128keCheckbox.Checked = zwRef.config.emulationOptions.Use128keForSnapshots;
                interlaceCheckBox.Checked = zwRef.config.renderOptions.Scanlines;
            }
        }

        private void modelComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            switch (currentModelIndex) {
                case 0:
                    current48kRom = romTextBox.Text;
                    break;

                case 1:
                    current128kRom = romTextBox.Text;
                    break;

                case 2:
                    current128keRom = romTextBox.Text;
                    break;

                case 3:
                    currentPlus3Rom = romTextBox.Text;
                    break;

                case 4:
                    break;
            }

            switch (modelComboBox.SelectedIndex) {
                case 0:
                    romTextBox.Text = current48kRom;
                    break;

                case 1:
                    romTextBox.Text = current128kRom;
                    break;

                case 2:
                    romTextBox.Text = current128keRom;
                    break;

                case 3:
                    romTextBox.Text = currentPlus3Rom;
                    break;

                case 4:
                    romTextBox.Text = currentPentagonRom;
                    break;
            }

            currentModelIndex = modelComboBox.SelectedIndex;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e) {
        }

        private void stereoRadioButton_CheckedChanged(object sender, EventArgs e) {
            monoRadioButton.Checked = !stereoRadioButton.Checked;
        }

        private void monoRadioButton_CheckedChanged(object sender, EventArgs e) {
            stereoRadioButton.Checked = !monoRadioButton.Checked;
        }

        private void ayFor48kCheckbox_CheckedChanged(object sender, EventArgs e) {
            EnableAYFor48K = ayFor48kCheckbox.Checked;
        }

        private void tabControl1_TabIndexChanged(object sender, EventArgs e) {
        }

        private void tabPage5_Enter(object sender, EventArgs e) {
            joystickComboBox1.Items.Clear();
            joystick2ComboBox1.Items.Clear();
            joystickComboBox1.Items.Add("None");
            joystick2ComboBox1.Items.Add("None");
            JoystickController.EnumerateJosticks();
            string[] devNames = JoystickController.GetDeviceNames();
            for (int f = 0; f < devNames.Length; f++) {
                joystickComboBox1.Items.Add(devNames[f]);
                joystick2ComboBox1.Items.Add(devNames[f]);
            }

            joystickComboBox1.SelectedIndex = joy1;
            joystick2ComboBox1.SelectedIndex = joy2;

            if (joystickComboBox2.SelectedIndex < 0)
                joystickComboBox2.SelectedIndex = 0;

            if (joystick2ComboBox2.SelectedIndex < 0)
                joystick2ComboBox2.SelectedIndex = 0;

        }

        private void joystick2ComboBox1_SelectedIndexChanged(object sender, EventArgs e) {
            if (joystick2ComboBox1.SelectedIndex == joystickComboBox1.SelectedIndex)
                joystickComboBox1.SelectedIndex = 0;

            if (Joystick2Choice <= 0)
            {
                joystick2ComboBox2.SelectedIndex = 0;
            }
        }

        private void joystickComboBox1_SelectedIndexChanged(object sender, EventArgs e) {
            if (joystickComboBox1.SelectedIndex == joystick2ComboBox1.SelectedIndex)
                joystick2ComboBox1.SelectedIndex = 0;

            if (Joystick1Choice <= 0)
            {
                joystickComboBox2.SelectedIndex = 0;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
        }

        private void lastStateCheckbox_CheckedChanged(object sender, EventArgs e) {
        }

        private void pixelSmoothingCheckBox_CheckedChanged(object sender, EventArgs e) {
            PixelSmoothing = pixelSmoothingCheckBox.Checked;
        }

        private void directXRadioButton_CheckedChanged(object sender, EventArgs e) {
            interlaceCheckBox.Enabled = true;
            pixelSmoothingCheckBox.Enabled = true;
            vsyncCheckbox.Enabled = true;
        }

        private void gdiRadioButton_CheckedChanged(object sender, EventArgs e) {
            interlaceCheckBox.Enabled = false;
            pixelSmoothingCheckBox.Enabled = false;
            vsyncCheckbox.Enabled = false;
        }

        private void button1_Click(object sender, EventArgs e) {
            /*
            if (this.zwRef.joystick1.isInitialized)
                this.zwRef.joystick1.Release();
            this.zwRef.joystick1 = new JoystickController();
            this.zwRef.joystick1.InitJoystick(this.zwRef, Joystick1Choice - 1);
            JoystickRemap jsRemap = new JoystickRemap(this.zwRef, this.zwRef.joystick1);
            jsRemap.ShowDialog();
            */
        }
    }
}