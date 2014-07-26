using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class CodeProfiler : Form
    {
        public CodeProfiler(Form1 zw) {
            InitializeComponent();
            Random rand = new Random();
            for (int f = 0; f < 65536; f++)
                temp[f] = zw.zx.PeekByteNoContend(f);
            this.Invalidate();
        }

        private byte[] temp = new byte[256 * 256];
        private Color[] heatColors = new Color[8] {Color.White, Color.Cyan, Color.Blue, Color.LightGreen, Color.Green, Color.Yellow, Color.Red, Color.Crimson };
        protected override void OnPaint(PaintEventArgs e) {
           
        }

        private void panel1_Paint(object sender, PaintEventArgs e) {
            Graphics g = e.Graphics;
            SolidBrush myBrush = new SolidBrush(Color.Red);
            for (int f = 0; f < 256; f++) {
                for (int i = 0; i < 256; i++) {
                    myBrush.Color = heatColors[temp[f * 256 + i] / 35];
                    g.FillRectangle(myBrush, new Rectangle(i * 4, f * 4, 2, 2));
                }
            }
        } 
    }
}
