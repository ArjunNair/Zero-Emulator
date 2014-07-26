using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class TextOnImageControl : UserControl
    {
        public String header = null;
        public String text = null;

        public Point textAnchor = new Point(5, 35);

        public TextOnImageControl() {
            InitializeComponent();
        }

        public void SetText(String _text, Point anchor) {
            text = _text;
            textAnchor = new Point(anchor.X, anchor.Y);
        }

        protected override void OnPaint(PaintEventArgs e) {
            if (this.BackgroundImage != null && this.text != null) {
                //Graphics g = Graphics.FromImage(this.BackgroundImage);
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                StringFormat strFormat = new StringFormat();
                strFormat.Alignment = StringAlignment.Near;

                strFormat.LineAlignment = StringAlignment.Near;
                e.Graphics.DrawString(header, new Font("Comic Sans MS", 14, FontStyle.Bold), Brushes.RosyBrown, new Point(5, 5));
                e.Graphics.DrawString(text, new Font("Comic Sans MS", 9), Brushes.DarkBlue, new RectangleF(textAnchor.X, textAnchor.Y, this.Width - textAnchor.X, this.Height - textAnchor.Y), strFormat);
            }
        }
    }
}