using System.Drawing;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class PicturePreview : Form
    {
        public PicturePreview(Image pix) {
            InitializeComponent();
            pictureBox1.Image = pix;
            pictureBox1.Invalidate();
        }
    }
}