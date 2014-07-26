using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;
using System.IO;
using Speccy;
using System.Windows;

namespace Ziggy
{
    public class Ziggy : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        ContentManager contentManager;

        SpriteBatch spriteBatch;
        Texture2D displayBuffer;
        KeyboardState oldState;
      
        bool active = true;
      

        Speccy.zxmachine zx;

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
            SPACE, SHIFT, CTRL, ALT, TAB, CAPS, ENTER, BACK,
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

        public Ziggy()
        {
            graphics = new GraphicsDeviceManager(this);
            contentManager = new ContentManager(Services);
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
               
                return true;
            }
            fs = new FileStream("ziggy.ini", FileMode.Open, FileAccess.Read);
            StreamReader sr = new StreamReader(fs);
            //Find Machine Last Emulated

            String modelName = GetConfigData(sr, "[EMULATION]", "MACHINE");
            switch (modelName)
            {
                case "_48k":
                    model = MachineModel._48k;
                    zx = new zx48(this.Window.Handle, 3.5);
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

        protected override void Initialize()
        {
            base.Initialize();
            oldState = Keyboard.GetState();
            zx.Reset();
        }

        /// <summary>
        /// Load your graphics content.  If loadAllContent is true, you should
        /// load content from both ResourceManagementMode pools.  Otherwise, just
        /// load ResourceManagementMode.Manual content.
        /// </summary>
        /// <param name="loadAllContent">Which type of content to load.</param>
        protected override void LoadContent()
        {
            //if (loadAllContent)
            // {
            // TODO: Load any ResourceManagementMode.Automatic content
            // }
            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);
            displayBuffer = new Texture2D(graphics.GraphicsDevice, 256+96, 192+48+48, 1, TextureUsage.Linear, SurfaceFormat.Bgr32);
        }

        /// <summary>
        /// Unload your graphics content.  If unloadAllContent is true, you should
        /// unload content from both ResourceManagementMode pools.  Otherwise, just
        /// unload ResourceManagementMode.Manual content.  Manual content will get
        /// Disposed by the GraphicsDevice during a Reset.
        /// </summary>
        /// <param name="unloadAllContent">Which type of content to unload.</param>
        protected override void UnloadContent()
        {
            // if (unloadAllContent == true)
            //{
            //zx.Shutdown();
            contentManager.Unload();
            //}
        }

        void Quit()
        {
            zx.Shutdown();
            this.Exit();
        }

        
        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            zx.Run();

            UpdateInput();
  
            graphics.GraphicsDevice.Textures[0] = null;

            displayBuffer.SetData<int>(zx.ScreenBuffer, 0,
                 displayBuffer.Width * displayBuffer.Height, SetDataOptions.Discard);

            base.Update(gameTime);

        }

        protected void UpdateInput()
        {
            KeyboardState newState = Keyboard.GetState();

            #region Alphabetical keys
            if (newState.IsKeyDown(Keys.A))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.A))
                {
                    zx.keyBuffer[(int)keyCode.A] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.A))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.A] = false;
            }

            if (newState.IsKeyDown(Keys.B))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.B))
                {
                    zx.keyBuffer[(int)keyCode.B] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.B))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.B] = false;
            }

            if (newState.IsKeyDown(Keys.C))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.C))
                {
                    zx.keyBuffer[(int)keyCode.C] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.C))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.C] = false;
            }

            if (newState.IsKeyDown(Keys.D))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D))
                {
                    zx.keyBuffer[(int)keyCode.D] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.D] = false;
            }

            if (newState.IsKeyDown(Keys.E))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.E))
                {
                    zx.keyBuffer[(int)keyCode.E] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.E))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.E] = false;
            }

            if (newState.IsKeyDown(Keys.F))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.F))
                {
                    zx.keyBuffer[(int)keyCode.F] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.F))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.F] = false;
            }

            if (newState.IsKeyDown(Keys.G))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.G))
                {
                    zx.keyBuffer[(int)keyCode.G] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.G))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.G] = false;
            }

            if (newState.IsKeyDown(Keys.H))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.H))
                {
                    zx.keyBuffer[(int)keyCode.H] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.H))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.H] = false;
            }

            if (newState.IsKeyDown(Keys.I))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.I))
                {
                    zx.keyBuffer[(int)keyCode.I] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.I))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.I] = false;
            }

            if (newState.IsKeyDown(Keys.J))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.J))
                {
                    zx.keyBuffer[(int)keyCode.J] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.J))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.J] = false;
            }

            if (newState.IsKeyDown(Keys.K))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.K))
                {
                    zx.keyBuffer[(int)keyCode.K] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.K))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.K] = false;
            }

            if (newState.IsKeyDown(Keys.L))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.L))
                {
                    zx.keyBuffer[(int)keyCode.L] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.L))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.L] = false;
            }

            if (newState.IsKeyDown(Keys.M))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.M))
                {
                    zx.keyBuffer[(int)keyCode.M] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.M))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.M] = false;
            }

            if (newState.IsKeyDown(Keys.N))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.N))
                {
                    zx.keyBuffer[(int)keyCode.N] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.N))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.N] = false;
            }

            if (newState.IsKeyDown(Keys.O))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.O))
                {
                    zx.keyBuffer[(int)keyCode.O] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.O))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.O] = false;
            }

            if (newState.IsKeyDown(Keys.P))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.P))
                {
                    zx.keyBuffer[(int)keyCode.P] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.P))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.P] = false;
            }

            if (newState.IsKeyDown(Keys.Q))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Q))
                {
                    zx.keyBuffer[(int)keyCode.Q] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Q))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.Q] = false;
            }

            if (newState.IsKeyDown(Keys.R))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.R))
                {
                    zx.keyBuffer[(int)keyCode.R] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.R))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.R] = false;
            }

            if (newState.IsKeyDown(Keys.S))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.S))
                {
                    zx.keyBuffer[(int)keyCode.S] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.S))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.S] = false;
            }

            if (newState.IsKeyDown(Keys.T))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.T))
                {
                    zx.keyBuffer[(int)keyCode.T] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.T))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.T] = false;
            }

            if (newState.IsKeyDown(Keys.U))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.U))
                {
                    zx.keyBuffer[(int)keyCode.U] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.U))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.U] = false;
            }

            if (newState.IsKeyDown(Keys.V))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.V))
                {
                    zx.keyBuffer[(int)keyCode.V] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.V))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.V] = false;
            }

            if (newState.IsKeyDown(Keys.W))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.W))
                {
                    zx.keyBuffer[(int)keyCode.W] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.W))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.W] = false;
            }

            if (newState.IsKeyDown(Keys.X))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.X))
                {
                    zx.keyBuffer[(int)keyCode.X] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.X))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.X] = false;
            }

            if (newState.IsKeyDown(Keys.Y))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Y))
                {
                    zx.keyBuffer[(int)keyCode.Y] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Y))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.Y] = false;
            }

            if (newState.IsKeyDown(Keys.Z))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Z))
                {
                    zx.keyBuffer[(int)keyCode.Z] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Z))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.Z] = false;
            }

            #endregion

            #region Numeric Keys
            if (newState.IsKeyDown(Keys.D0))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D0))
                {
                    zx.keyBuffer[(int)keyCode._0] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D0))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._0] = false;
            }

            if (newState.IsKeyDown(Keys.D1))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D1))
                {
                    zx.keyBuffer[(int)keyCode._1] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D1))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._1] = false;
            }

            if (newState.IsKeyDown(Keys.D2))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D2))
                {
                    zx.keyBuffer[(int)keyCode._2] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D2))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._2] = false;
            }

            if (newState.IsKeyDown(Keys.D3))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D3))
                {
                    zx.keyBuffer[(int)keyCode._3] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D3))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._3] = false;
            }

            if (newState.IsKeyDown(Keys.D4))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D4))
                {
                    zx.keyBuffer[(int)keyCode._4] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D4))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._4] = false;
            }

            if (newState.IsKeyDown(Keys.D5))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D5))
                {
                    zx.keyBuffer[(int)keyCode._5] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D5))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._5] = false;
            }

            if (newState.IsKeyDown(Keys.D6))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D6))
                {
                    zx.keyBuffer[(int)keyCode._6] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D6))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._6] = false;
            }

            if (newState.IsKeyDown(Keys.D7))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D7))
                {
                    zx.keyBuffer[(int)keyCode._7] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D7))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._7] = false;
            }

            if (newState.IsKeyDown(Keys.D8))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D8))
                {
                    zx.keyBuffer[(int)keyCode._8] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D8))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._8] = false;
            }

            if (newState.IsKeyDown(Keys.D9))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.D9))
                {
                    zx.keyBuffer[(int)keyCode._9] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.D9))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode._9] = false;
            }
            #endregion
               
            #region Editing keys
            if (newState.IsKeyDown(Keys.Enter))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Enter))
                {
                    zx.keyBuffer[(int)keyCode.ENTER] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Enter))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.ENTER] = false;
            }

            if (newState.IsKeyDown(Keys.Space))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Space))
                {
                    zx.keyBuffer[(int)keyCode.SPACE] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Space))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.SPACE] = false;
            }
              
            if (newState.IsKeyDown(Keys.LeftControl) || newState.IsKeyDown(Keys.RightControl))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.LeftControl) || !oldState.IsKeyDown(Keys.RightControl))
                {
                    zx.keyBuffer[(int)keyCode.CTRL] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.LeftControl) || oldState.IsKeyDown(Keys.RightControl))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.CTRL] = false;
            }

            if (newState.IsKeyDown(Keys.LeftShift) || newState.IsKeyDown(Keys.RightShift))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.LeftShift) || !oldState.IsKeyDown(Keys.RightShift))
                {
                    zx.keyBuffer[(int)keyCode.SHIFT] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.LeftShift) || oldState.IsKeyDown(Keys.RightShift))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.SHIFT] = false;
            }

            if (newState.IsKeyDown(Keys.Up))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Up))
                {
                    zx.keyBuffer[(int)keyCode.UP] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Up))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.UP] = false;
            }

            if (newState.IsKeyDown(Keys.Down))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Down))
                {
                    zx.keyBuffer[(int)keyCode.DOWN] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Down))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.DOWN] = false;
            }

            if (newState.IsKeyDown(Keys.Left))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Left))
                {
                    zx.keyBuffer[(int)keyCode.LEFT] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Left))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.LEFT] = false;
            }

            if (newState.IsKeyDown(Keys.Right))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Right))
                {
                    zx.keyBuffer[(int)keyCode.RIGHT] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Right))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.RIGHT] = false;
            }

            if (newState.IsKeyDown(Keys.Back))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Back))
                {
                    zx.keyBuffer[(int)keyCode.BACK] = true;
                }
            }
            else if (oldState.IsKeyDown(Keys.Back))
            {
                // Key was down last update, but not down now, so
                // it has just been released.
                zx.keyBuffer[(int)keyCode.BACK] = false;
            }

            if (newState.IsKeyDown(Keys.Escape))
            {
                // If not down last update, key has just been pressed.
                if (!oldState.IsKeyDown(Keys.Escape))
                {
                    Quit();
                }
            }
            
            #endregion

            #region Emulation specific keys]
            if (newState.IsKeyDown(Keys.Home))
            {
                
            }
            else if (oldState.IsKeyDown(Keys.Home))
            {
                
            }
            #endregion

            oldState = newState;
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            //graphics.GraphicsDevice.Clear(Color.Black);

            if (active)
            {

                spriteBatch.Begin(SpriteBlendMode.None);

                    spriteBatch.Draw(displayBuffer, new Rectangle(0, 0,
                                    graphics.PreferredBackBufferWidth, 
                                    graphics.PreferredBackBufferHeight), 
                                    Color.White);
                spriteBatch.End();

            }

            base.Draw(gameTime);
        }
    }
}
