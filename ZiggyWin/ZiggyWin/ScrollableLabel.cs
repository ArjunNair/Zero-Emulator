using System.Windows.Forms;

namespace ZeroWin
{
    public partial class ScrollableLabel : ScrollableControl
    {
        public Label scrollLabel = new Label();

        public ScrollableLabel() {
            InitializeComponent();
            this.SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            this.BackColor = System.Drawing.Color.Transparent;
            this.ForeColor = System.Drawing.Color.White;
            this.AutoScroll = true;
            scrollLabel.AutoSize = true;
            this.Controls.Add(scrollLabel);
            scrollLabel.Font = new System.Drawing.Font("Tahoma", 8);
            this.HScroll = false;
            this.AutoScrollMargin = new System.Drawing.Size(1, 1);
            // this.AutoScrollMinSize = 20;
        }

        /*  protected override void OnPaint(PaintEventArgs e)
          {
              Font myFont = new Font("Tahoma", 8);
              SolidBrush brush = new SolidBrush(Color.White);

              e.Graphics.DrawString(this.Text, myFont, brush, 0,0);//new RectangleF(this.Bounds.Left, this.Bounds.Top, this.Bounds.Width, this.Bounds.Height)) ;
              base.OnPaint(e);
          }
         */
    }
}