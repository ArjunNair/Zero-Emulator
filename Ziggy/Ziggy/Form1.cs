using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Text;
using System.IO;
using System.Windows.Forms;
using Speccy;
using System.Runtime.InteropServices;

namespace Ziggy
{
    public partial class Form1 : Form
    {
        Speccy.zxmachine zx;
        System.Drawing.Bitmap screenDisplay; 

        enum MachineModel
        {
            _48k,
            _128k,
            _plus2,
            _plus3
        };

        MachineModel model = MachineModel._48k;
        private int volume = 50;
        private bool mute = false;
        private int windowSize = 200;
        private bool fullScreen = false;
        private string romPath = ".\\ROMS\\";
        private string gamePath = ".\\GAMES\\";
        private string gameSavePath = ".\\SAVES\\";
        private string screenshotPath = ".\\SCREENSHOTS\\";
        private string gameCheatPath = ".\\CHEATS\\";
        private string gameInfoPath = ".\\INFO\\";

        public enum keyCode
        {
            Q, W, E, R, T, Y, U, I, O, P, A, S, D, F, G, H, J, K, L, Z, X, C, V, B, N, M,
            _0, _1, _2, _3, _4, _5, _6, _7, _8, _9,
            SPACE, SHIFT, CTRL, ALT, TAB, CAPS,ENTER, BACK, 
            DEL, INS, HOME, END, PGUP, PGDOWN, NUMLOCK,
            ESC, PRINT_SCREEN, SCROLL_LOCK, PAUSE_BREAK,
            TILDE, EXCLAMATION, AT, HASH, DOLLAR, PERCENT, CARAT,
            AMPERSAND, ASTERISK, LBRACKET, RBRACKET, HYPHEN, PLUS, VBAR,
            LCURLY, RCURLY, COLON, DQUOTE, LESS_THAN, GREATER_THAN, QMARK,
            UNDER_SCORE, EQUALS, BSLASH, LSQUARE, RSQUARE, SEMI_COLON,
            APOSTROPHE, COMMA, STOP, FSLASH, LEFT, RIGHT, UP, DOWN,
            F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
            LAST
        };

        public Form1()
        {
           
            InitializeComponent();
            pictureBox1.Image = screenDisplay;
            screenDisplay = new Bitmap(256 + 96, 192 + 48 + 48, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            ReadConfigFile();
        }

        public string GetConfigData(StreamReader sr, string section, string data)
        {
            //StreamReader sr = new StreamReader(fs);
            String readStr = "dummy";

            while (readStr != section)
            {
                if (sr.EndOfStream == true)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid config file!", "Config file error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    return "error";
                }
                readStr = sr.ReadLine();
            }

            while (true)
            {
                readStr = sr.ReadLine();
                if (readStr.IndexOf(data) >= 0)
                    break;
                if (sr.EndOfStream == true)
                {
                    System.Windows.Forms.MessageBox.Show("Invalid config file!", "Config file error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    return "error";
                }
            }

            int startIndex = readStr.IndexOf("=") + 1;
            String dataString = readStr.Substring(startIndex, readStr.Length - startIndex);

            return dataString;
        }

        public bool ReadConfigFile()
        {
            FileStream fs;
            try
            {
                fs = new FileStream("ziggy.ini", FileMode.Open, FileAccess.Read);
            }
            catch
            {
                fs = new FileStream("ziggy.ini", FileMode.Create, FileAccess.Write);

                if (fs == null)
                    return false;

                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine("\n[EMULATION]");
                sw.WriteLine("MACHINE=" + model);
                sw.WriteLine("\n[PATHS]");
                sw.WriteLine("ROMS=" + romPath);
                sw.WriteLine("GAMES=" + gamePath);
                sw.WriteLine("SAVED GAMES=" + gameSavePath);
                sw.WriteLine("SCREENSHOTS=" + screenshotPath);
                sw.WriteLine("GAME INFO=" + gameInfoPath);
                sw.WriteLine("GAME CHEATS=" + gameCheatPath);
                sw.WriteLine("\n[DISPLAY]");
                sw.WriteLine("FULL SCREEN=" + fullScreen);
                sw.WriteLine("WINDOW SIZE=" + windowSize);
                sw.WriteLine("\n[AUDIO]");
                sw.WriteLine("MUTE=" + mute);
                sw.WriteLine("VOLUME=" + volume);
                sw.Close();
            }

            fs = new FileStream("ziggy.ini", FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            //Find Machine Last Emulated
            String modelName = GetConfigData(sr, "[EMULATION]", "MACHINE");
            switch (modelName)
            {
                case "_48k":
                    model = MachineModel._48k;
                    zx = new zx48(3.5);
                    break;

                case "_128k":
                    model = MachineModel._128k;
                    break;

                case "_plus2":
                    model = MachineModel._plus2;
                    break;
                case "_plus3":
                    model = MachineModel._plus3;
                    break;
            }

            //Load the ROM
            romPath = GetConfigData(sr, "[PATHS]", "ROMS");
            sr.Close();
            fs.Close();
            return zx.LoadROM(romPath + "48k.ROM");
        }

        protected override void OnKeyDown(KeyEventArgs keyEvent)
        {
            switch (keyEvent.KeyCode)
            {
                case Keys.A:
                    zx.keyBuffer[(int)keyCode.A] = true;
                    break;

                case Keys.B:
                    zx.keyBuffer[(int)keyCode.B] = true;
                    break;

                case Keys.C:
                    zx.keyBuffer[(int)keyCode.C] = true;
                    break;

                case Keys.D:
                    zx.keyBuffer[(int)keyCode.D] = true;
                    break;

                case Keys.E:
                    zx.keyBuffer[(int)keyCode.E] = true;
                    break;

                case Keys.F:
                    zx.keyBuffer[(int)keyCode.F] = true;
                    break;

                case Keys.G:
                    zx.keyBuffer[(int)keyCode.G] = true;
                    break;

                case Keys.H:
                    zx.keyBuffer[(int)keyCode.H] = true;
                    break;

                case Keys.I:
                    zx.keyBuffer[(int)keyCode.I] = true;
                    break;

                case Keys.J:
                    zx.keyBuffer[(int)keyCode.J] = true;
                    break;

                case Keys.K:
                    zx.keyBuffer[(int)keyCode.K] = true;
                    break;

                case Keys.L:
                    zx.keyBuffer[(int)keyCode.L] = true;
                    break;

                case Keys.M:
                    zx.keyBuffer[(int)keyCode.M] = true;
                    break;

                case Keys.N:
                    zx.keyBuffer[(int)keyCode.N] = true;
                    break;

                case Keys.O:
                    zx.keyBuffer[(int)keyCode.O] = true;
                    break;

                case Keys.P:
                    zx.keyBuffer[(int)keyCode.P] = true;
                    break;

                case Keys.Q:
                    zx.keyBuffer[(int)keyCode.Q] = true;
                    break;

                case Keys.R:
                    zx.keyBuffer[(int)keyCode.R] = true;
                    break;

                case Keys.S:
                    zx.keyBuffer[(int)keyCode.S] = true;
                    break;
                case Keys.T:
                    zx.keyBuffer[(int)keyCode.T] = true;
                    break;

                case Keys.U:
                    zx.keyBuffer[(int)keyCode.U] = true;
                    break;

                case Keys.V:
                    zx.keyBuffer[(int)keyCode.V] = true;
                    break;

                case Keys.W:
                    zx.keyBuffer[(int)keyCode.W] = true;
                    break;

                case Keys.X:
                    zx.keyBuffer[(int)keyCode.X] = true;
                    break;

                case Keys.Y:
                    zx.keyBuffer[(int)keyCode.Y] = true;
                    break;

                case Keys.Z:
                    zx.keyBuffer[(int)keyCode.Z] = true;
                    break;

                case Keys.D0:
                    zx.keyBuffer[(int)keyCode._0] = true;
                    break;

                case Keys.D1:
                    zx.keyBuffer[(int)keyCode._1] = true;
                    break;

                case Keys.D2:
                    zx.keyBuffer[(int)keyCode._2] = true;
                    break;

                case Keys.D3:
                    zx.keyBuffer[(int)keyCode._3] = true;
                    break;

                case Keys.D4:
                    zx.keyBuffer[(int)keyCode._4] = true;
                    break;

                case Keys.D5:
                    zx.keyBuffer[(int)keyCode._5] = true;
                    break;

                case Keys.D6:
                    zx.keyBuffer[(int)keyCode._6] = true;
                    break;

                case Keys.D7:
                    zx.keyBuffer[(int)keyCode._7] = true;
                    break;

                case Keys.D8:
                    zx.keyBuffer[(int)keyCode._8] = true;
                    break;

                case Keys.D9:
                    zx.keyBuffer[(int)keyCode._9] = true;
                    break;

                case Keys.Enter:
                    zx.keyBuffer[(int)keyCode.ENTER] = true;
                    break;

                case Keys.Space:
                    zx.keyBuffer[(int)keyCode.SPACE] = true;
                    break;

                case Keys.ControlKey:
                    zx.keyBuffer[(int)keyCode.CTRL] = true;
                    break;

                case Keys.ShiftKey:
                    zx.keyBuffer[(int)keyCode.SHIFT] = true;
                    break;

                case Keys.Up:
                    zx.keyBuffer[(int)keyCode.UP] = true;
                    break;

                case Keys.Left:
                    zx.keyBuffer[(int)keyCode.LEFT] = true;
                    break;

                case Keys.Right:
                    zx.keyBuffer[(int)keyCode.RIGHT] = true;
                    break;

                case Keys.Down:
                    zx.keyBuffer[(int)keyCode.DOWN] = true;
                    break;

                case Keys.Back:
                    zx.keyBuffer[(int)keyCode.BACK] = true;
                    break;

                case Keys.Escape:
                    this.Close();
                    break;
            }
        }

        protected override void OnKeyUp(KeyEventArgs keyEvent)
        {
            switch (keyEvent.KeyCode)
            {
                case Keys.A:
                    zx.keyBuffer[(int)keyCode.A] = false;
                    break;

                case Keys.B:
                    zx.keyBuffer[(int)keyCode.B] = false;
                    break;

                case Keys.C:
                    zx.keyBuffer[(int)keyCode.C] = false;
                    break;

                case Keys.D:
                    zx.keyBuffer[(int)keyCode.D] = false;
                    break;

                case Keys.E:
                    zx.keyBuffer[(int)keyCode.E] = false;
                    break;

                case Keys.F:
                    zx.keyBuffer[(int)keyCode.F] = false;
                    break;

                case Keys.G:
                    zx.keyBuffer[(int)keyCode.G] = false;
                    break;

                case Keys.H:
                    zx.keyBuffer[(int)keyCode.H] = false;
                    break;

                case Keys.I:
                    zx.keyBuffer[(int)keyCode.I] = false;
                    break;

                case Keys.J:
                    zx.keyBuffer[(int)keyCode.J] = false;
                    break;

                case Keys.K:
                    zx.keyBuffer[(int)keyCode.K] = false;
                    break;

                case Keys.L:
                    zx.keyBuffer[(int)keyCode.L] = false;
                    break;

                case Keys.M:
                    zx.keyBuffer[(int)keyCode.M] = false;
                    break;

                case Keys.N:
                    zx.keyBuffer[(int)keyCode.N] = false;
                    break;

                case Keys.O:
                    zx.keyBuffer[(int)keyCode.O] = false;
                    break;

                case Keys.P:
                    zx.keyBuffer[(int)keyCode.P] = false;
                    break;

                case Keys.Q:
                    zx.keyBuffer[(int)keyCode.Q] = false;
                    break;

                case Keys.R:
                    zx.keyBuffer[(int)keyCode.R] = false;
                    break;

                case Keys.S:
                    zx.keyBuffer[(int)keyCode.S] = false;
                    break;
                case Keys.T:
                    zx.keyBuffer[(int)keyCode.T] = false;
                    break;

                case Keys.U:
                    zx.keyBuffer[(int)keyCode.U] = false;
                    break;

                case Keys.V:
                    zx.keyBuffer[(int)keyCode.V] = false;
                    break;

                case Keys.W:
                    zx.keyBuffer[(int)keyCode.W] = false;
                    break;

                case Keys.X:
                    zx.keyBuffer[(int)keyCode.X] = false;
                    break;

                case Keys.Y:
                    zx.keyBuffer[(int)keyCode.Y] = false;
                    break;

                case Keys.Z:
                    zx.keyBuffer[(int)keyCode.Z] = false;
                    break;

                case Keys.D0:
                    zx.keyBuffer[(int)keyCode._0] = false;
                    break;

                case Keys.D1:
                    zx.keyBuffer[(int)keyCode._1] = false;
                    break;

                case Keys.D2:
                    zx.keyBuffer[(int)keyCode._2] = false;
                    break;

                case Keys.D3:
                    zx.keyBuffer[(int)keyCode._3] = false;
                    break;

                case Keys.D4:
                    zx.keyBuffer[(int)keyCode._4] = false;
                    break;

                case Keys.D5:
                    zx.keyBuffer[(int)keyCode._5] = false;
                    break;

                case Keys.D6:
                    zx.keyBuffer[(int)keyCode._6] = false;
                    break;

                case Keys.D7:
                    zx.keyBuffer[(int)keyCode._7] = false;
                    break;

                case Keys.D8:
                    zx.keyBuffer[(int)keyCode._8] = false;
                    break;

                case Keys.D9:
                    zx.keyBuffer[(int)keyCode._9] = false;
                    break;

                case Keys.Enter:
                    zx.keyBuffer[(int)keyCode.ENTER] = false;
                    break;

                case Keys.Space:
                    zx.keyBuffer[(int)keyCode.SPACE] = false;
                    break;

                case Keys.ControlKey:
                    zx.keyBuffer[(int)keyCode.CTRL] = false;
                    break;

                case Keys.ShiftKey:
                    zx.keyBuffer[(int)keyCode.SHIFT] = false;
                    break;

                case Keys.Up:
                    zx.keyBuffer[(int)keyCode.UP] = false;
                    break;

                case Keys.Left:
                    zx.keyBuffer[(int)keyCode.LEFT] = false;
                    break;

                case Keys.Right:
                    zx.keyBuffer[(int)keyCode.RIGHT] = false;
                    break;

                case Keys.Down:
                    zx.keyBuffer[(int)keyCode.DOWN] = false;
                    break;

                case Keys.Back:
                    zx.keyBuffer[(int)keyCode.BACK] = false;
                    break;

                case Keys.CapsLock:
                    zx.keyBuffer[(int)keyCode.CAPS] = true;
                    break;
               
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            zx.Run();
            CopyBufferToBitmap();
            pictureBox1.Image = null;
            pictureBox1.Image = screenDisplay;

            pictureBox1.Update();   
            button1.Update();
            this.Invalidate();
            base.OnPaint(e);
        }

        public void CopyBufferToBitmap()
        {
            //Here create the Bitmap to the know height, width and format
            //Bitmap bmp = new Bitmap(352, 288, PixelFormat.Format24bppRgb);

            //Create a BitmapData and Lock all pixels to be written 
            System.Drawing.Imaging.BitmapData bmpData = screenDisplay.LockBits(
                                 new Rectangle(0, 0, screenDisplay.Width, screenDisplay.Height),
                                 System.Drawing.Imaging.ImageLockMode.WriteOnly, screenDisplay.PixelFormat);

            //Copy the data from the byte array into BitmapData.Scan0
            Marshal.Copy(zx.ScreenBuffer, 0, bmpData.Scan0, screenDisplay.Width * screenDisplay.Height);

            
            //Unlock the pixels
            screenDisplay.UnlockBits(bmpData);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = gamePath;
            openFileDialog1.Title = "Choose a game";
            openFileDialog1.FileName = "";
            openFileDialog1.Filter = "Snapshots|*.sna;*.z80";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                zx.LoadSnapshot(openFileDialog1.FileName);
            }
            pictureBox1.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            zx.Shutdown();
            base.OnClosed(e);
        }
    }
}
