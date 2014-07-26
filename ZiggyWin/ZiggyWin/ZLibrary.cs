using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ZeroWin
{
    public partial class ZLibrary : Form
    {
        public ZLibrary()
        {
            InitializeComponent();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                    {
                        Point pos = new Point(button1.Location.X, button1.Location.Y);
                        pos.X = pos.X - 1;
                        button1.Location = pos;
                        break;
                    }

                case Keys.S:
                    {
                        Point pos = new Point(button1.Location.X, button1.Location.Y);
                        pos.X = pos.X + 1;
                        button1.Location = pos;
                        break;
                    }
            }
        }
    }
}
