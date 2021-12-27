using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Threading;
using SpeccyCommon;

namespace ZeroWin
{
    public partial class MemoryProfiler : Form
    {
        Form1 ziggyWin;
        double lastTime;
        const int MAP_WIDTH = 512;
        const int MAP_HEIGHT= 512;
        uint[] heatMap = new uint[65536];
        Bitmap bmpOut = new Bitmap(MAP_WIDTH, MAP_HEIGHT, PixelFormat.Format32bppArgb);
        private Color[] heatColors = new Color[8] {Color.Black, Color.Cyan, Color.Blue, Color.LightGreen, Color.Green, Color.Yellow, Color.Red, Color.Crimson };
        Thread paintThread;
        bool run = true;

        public MemoryProfiler(Form1 zw)
        {
            InitializeComponent();
            ziggyWin = zw;
            lastTime = PrecisionTimer.TimeInSeconds();
            ziggyWin.zx.MemoryWriteEvent += MemoryWriteEventHandler;
            ziggyWin.zx.MemoryReadEvent += MemoryReadEventHandler;
            //ziggyWin.zx.FrameEndEvent += FrameEndEventHandler;

            this.Invalidate();
            
            paintThread = new Thread(new ThreadStart(PaintMap));
            paintThread.Name = "Heatmap Thread";
            paintThread.Priority = System.Threading.ThreadPriority.Normal;
            paintThread.Start();
        }

        void PaintMap()
        {
            while (run)
            {
                double currentTime = PrecisionTimer.TimeInSeconds();

                if (currentTime - lastTime < 1.0f)
                    continue;

                lastTime = currentTime;
                //panel1.Invalidate();
                //continue;
                Rectangle rect = new Rectangle(0, 0, bmpOut.Width, bmpOut.Height);
                System.Drawing.Imaging.BitmapData bmpData =
                    bmpOut.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    bmpOut.PixelFormat);
                IntPtr ptr = bmpData.Scan0;

                unsafe
                {
                    int* p = (int*)ptr.ToPointer();

                    for (int f = 0; f < 65536; f++)
                    {
                        int colorIndex = (int)(heatMap[f] % 7);

                        if (colorIndex > 7)
                            colorIndex = 7;

                        *(p) = heatColors[colorIndex].ToArgb();
                        *(p + 1) = heatColors[colorIndex].ToArgb();
                        *(p + MAP_WIDTH) = heatColors[colorIndex].ToArgb();
                        *(p + MAP_WIDTH + 1) = heatColors[colorIndex].ToArgb();
                        p++; p++;
                        //*(p++) = heatColors[colorIndex].ToArgb();
                        if((f+1) % 256 == 0)
                            p = p + MAP_WIDTH;
                        heatMap[f] = 0;
                    }
                }

                bmpOut.UnlockBits(bmpData);
                pictureBox1.Image = bmpOut;
                //pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
        }

        private void panel1_Paint(object sender, PaintEventArgs e) {
            
            Graphics g = e.Graphics;
            SolidBrush myBrush = new SolidBrush(Color.Red);

            for (int f = 0; f < 256; f++) {
                for (int i = 0; i < 256; i++) {
                    int colorIndex = (int)(heatMap[f * 256 + i] % 1000);
                    
                    if (colorIndex > 7)
                        colorIndex = 7;

                    myBrush.Color = heatColors[colorIndex];
                    g.FillRectangle(myBrush, new Rectangle(i * 3, f * 3, 2, 2));
                }
            }
             
        } 

        void FrameEndEventHandler(object sender)
        {   
            /*
            for (int f = 0; f < 65536; f++)
            {
                if (heatMap[f] > 0)
                    heatMap[f] = 0;
            }*/      
        }

        void MemoryWriteEventHandler(object sender, MemoryEventArgs e)
        {
            heatMap[e.Address] = 6;
        }

        void MemoryReadEventHandler(object sender, MemoryEventArgs e)
        {
            heatMap[e.Address] = 1;
        }

        private void CodeProfiler_FormClosing(object sender, FormClosingEventArgs e)
        {
            run = false;
            ziggyWin.zx.MemoryWriteEvent -= MemoryWriteEventHandler;
            ziggyWin.zx.FrameEndEvent -= FrameEndEventHandler;
        }
    }
}
