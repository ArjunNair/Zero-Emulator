using System.Drawing;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class CoverFlowImage : UserControl
    {
        Bitmap img;
        public int originalWidth;
        public int originalHeight;
        public double angle;

        public CoverFlowImage() 
        {
            InitializeComponent();
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.AutoSize = false;
            img = ZeroWin.Properties.Resources.NoImage;
            image.SizeMode = PictureBoxSizeMode.StretchImage;
            //image.Visible = false;
        }
        public CoverFlowImage(string title)
        {
            InitializeComponent();
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.AutoSize = false;
            originalWidth = this.Width = 150;
            originalHeight = this.Height = 200;
        }

        public void SetImage(Bitmap bmp)
        {
            //image.Image = bmp;
            img = bmp;
            image.SizeMode = PictureBoxSizeMode.StretchImage;
            image.Visible = true;
        }

        public void SetSize(int _width, int _height)
        {
            this.Width = _width;
            this.Height = _height;
        }

        public void SetImageSize(int _width, int _height)
        {
            image.Width = _width;
            image.Height = _height;
        }

        public void SetScale(float s)
        {
            //this.Width = (int)(originalWidth * s);
            //this.Height = (int) (originalHeight * s);
            this.Width = (int)(s * originalWidth) / 100;
            this.Height = (int)(s * originalHeight) / 100;
        }

        public void SetPosition(int _x, int _y)
        {
            this.Location = new Point(_x, _y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);
            int _height = img.Height + 50;
            Bitmap bmp = new Bitmap(img.Width, _height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            Brush brsh = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, img.Width + 10,
                          _height), Color.Transparent, Color.Black, System.Drawing.Drawing2D.LinearGradientMode.Vertical);

            bmp.SetResolution(img.HorizontalResolution, img.VerticalResolution);

            using (Graphics grfx = Graphics.FromImage(bmp)) //A graphics to be generated 
            //from an image (here, the new Bitmap we've created (BMP)).
            {
                Bitmap bm = (Bitmap)img;                        //Generates a bitmap from the original image (img).
                grfx.DrawImage(bm, 0, 0, img.Width, img.Height); //Draws the generated 
                                                                 //bitmap (bm) to the new bitmap (bmp).
                Bitmap bm1 = (Bitmap)img; 	//Generates a bitmap again 
                                             //from the original image (img).
                bm1.RotateFlip(RotateFlipType.Rotate180FlipX); //Flips and rotates the 
                                                                 //image (bm1).
                grfx.DrawImage(bm1, 0, img.Height + 10); 	//Draws (bm1) below (bm) so it serves 
                                                            //as the reflection image.
                bm1.RotateFlip(RotateFlipType.Rotate180FlipX);
                Rectangle rt = new Rectangle(0, img.Height, img.Width, 50); //A new rectangle 
                                                                              //to paint our gradient effect.
                grfx.FillRectangle(brsh, rt); //Brushes the gradient on (rt).
            }
            image.Image = bmp;
        }
    }
}
