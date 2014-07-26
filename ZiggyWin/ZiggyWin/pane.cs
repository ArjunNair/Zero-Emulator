using System.Windows.Forms;

namespace ZeroWin
{
    public partial class pane : UserControl
    {
        public pane() {
            InitializeComponent();
            this.DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e) {
            //Do not paint background
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e) {
            //Do not paint background
            //e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            //base.OnPaintBackground(e);
        }
    }
}