using System;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class CoverFlow : UserControl
    {
        System.Collections.Generic.List<CoverFlowImage> coverList = new System.Collections.Generic.List<CoverFlowImage>();

        int activeIndex = 0;
        int targetIndex = 0;

        int coverSeperation = 10;
        int center_x = 0;
        int center_y = 0;
        double destAngle = 0;
        double currentAngle = 0;
        const int COVER_RADIUS_X = 200;
        const int COVER_RADIUS_Y = -00;
        int zeroPos_x = 0;
        //carousel
        int radiusX, radiusY;


        public int CoverSeperation
        {
            get { return coverSeperation; }
            set { coverSeperation = value; }
        }

        float reflectTransparency = 0.5f;
        public float ReflectionTransparency
        {
            get { return reflectTransparency; }
            set { reflectTransparency = value; }
        }

        public CoverFlow()
        {
            InitializeComponent();
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            timer1.Interval = 30;
            timer1.Enabled = true;
        }

        public void SetPosition(int _x, int _y)
        {
            this.Location = new System.Drawing.Point(_x, _y);
            center_x = (this.Location.X + this.Width) / 2;
            center_y = (this.Location.Y + this.Height) / 2;
            radiusX = this.Width / 2;
            radiusY = this.Height / 2;
        }

        public void SetSize(int _w, int _h)
        {
            this.Width = _w;
            this.Height = _h;
            center_x = (this.Location.X + _w) / 2;
            center_y = (this.Location.Y + _h) / 2;
            radiusX = this.Width / 2;
            radiusY = this.Height / 2;
        }

        public void SetBackgroundColour(System.Drawing.Color c)
        {
            this.BackColor = c;
        }

        public void SetupCarousel()
        {
            if (coverList.Count == 0)
                return;

            radiusX = this.Width / 2 - coverList[0].Width / 2;
            radiusY = this.Height / 2 -coverList[0].Height / 2;
            float smallRange = (1 - 0.5f) * 0.5f;
            
    
            for (int f = 0; f < coverList.Count; f++ )
            {
                

                double angle = f * ((Math.PI/ coverList.Count) * 2);
                double _x = Math.Sin(angle) * COVER_RADIUS_X + radiusX;
                double _y = Math.Cos(angle) * COVER_RADIUS_Y + radiusY;
                coverList[f].angle = angle;

                float scale = (float)( ((Math.Sin(angle) + 1) * smallRange) + 0.5f);

                coverList[f].SetPosition((int)_x, (int)_y);

                if (f == 0)
                    zeroPos_x = coverList[f].Location.X;

                float scaleX = 100 - ((float)(Math.Abs(coverList[f].Location.X - zeroPos_x)) / (float)(zeroPos_x)) * 100;
                //coverList[f].SetScale(scaleX);
                if (coverList[f].Location.Y < (radiusY))
                    coverList[f].Visible = false;
                else
                    coverList[f].Visible = true;
            }
            destAngle = currentAngle = coverList[0].angle;
        }

        public void AddCover(System.Drawing.Bitmap bmp, string title)
        {
            center_x = (this.Location.X + this.Width) / 2;
            center_y = (this.Location.Y + this.Height) / 2;
           
            CoverFlowImage cover = new CoverFlowImage(title);
            cover.SetImage(bmp);
            cover.SetImageSize(150, 200);
            coverList.Add(cover);
            this.Controls.Add(cover);
            
            //if (coverList.Count == 1)
            //{
            //   coverList[0].SetPosition(center_x - cover.Width/2, center_y - cover.Height/2);
            //}
            //else
            //    coverList[coverList.Count - 1].SetPosition(this.Controls[this.Controls.Count - 2].Location.X + coverSeperation + this.Controls[this.Controls.Count - 2].Width , this.Controls[this.Controls.Count - 2].Location.Y);
        }

        public void MoveLeft()
        {
            targetIndex--;
            if (targetIndex < 0)
            {
                targetIndex = coverList.Count - 1;
            }

           destAngle += ((Math.PI / coverList.Count) * 2);
        }

        public void MoveRight()
        {
            targetIndex++;
            if (targetIndex > coverList.Count - 1)
                targetIndex = 0;

            destAngle -= ((Math.PI / coverList.Count) * 2);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            /*
            if ((activeIndex != targetIndex) && (coverList.Count > 0))
            {
                int indexDelta = activeIndex - targetIndex;
                 int direction = (indexDelta > 0? 1: -1);
                 if (distanceToCover < (coverList[0].Width + coverSeperation) * Math.Abs(indexDelta))
                     distanceToCover += flowSpeed;
                 else
                 {
                     distanceToCover = 0;
                     activeIndex = targetIndex;
                     return;
                 }

                foreach (CoverFlowImage c in coverList)
                {
                    c.SetPosition(c.Location.X + flowSpeed * direction, center_y - c.Height / 2);
                 
                }
            }
             */
            if ((destAngle != currentAngle) && (coverList.Count > 0))
            {
                double change = (destAngle - currentAngle);
                double absChange = Math.Abs(change);

                this.currentAngle += change * 0.2f;

                if (absChange < 0.001)
                {
                    currentAngle = destAngle;
                    activeIndex = targetIndex;
                }

                for (int f = 0; f < coverList.Count; f++)
                {
                    coverList[f].angle += change * 0.2f;

                    
                    double _x = Math.Sin(coverList[f].angle) * COVER_RADIUS_X + radiusX;
                    double _y = Math.Cos(coverList[f].angle) * COVER_RADIUS_Y + radiusY;
                    
                   // coverList[f].angle += currentAngle;
                    coverList[f].SetPosition((int)_x, (int)_y);
                    if (coverList[f].Location.Y < (radiusY))
                        coverList[f].Visible = false;
                    else
                        coverList[f].Visible = true;

                    float scaleX = 100 - ((float)(Math.Abs(coverList[f].Location.X - zeroPos_x)) / (float)(zeroPos_x)) * 100;
                   // coverList[f].SetScale(scaleX);
                    //int scaleY = 200 - Math.Abs(coverList[f].Location.X - center_x);
                    
                }
            }
        }
    }
}
