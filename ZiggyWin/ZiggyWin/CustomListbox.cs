//CustomListBox.cs
//(c) Arjun Nair 2011
//The custom list box is a customised view of a list box object to draw items in 2-column mode.
//The first column holds an icon for the item and the 2nd column is the item name.
//The default list box can only draw 1 column, hence the need for the customListBox.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroWin
{
    public delegate void ImageChangedEvent(object sender);
    public class CustomListbox : ListBox
    {
        private Font customBoldFont = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 10, FontStyle.Bold);
        private Font customRegularFont = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 10);

        public CustomListbox() {
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.BorderStyle = BorderStyle.Fixed3D;
            this.DoubleBuffered = true;
            this.HorizontalScrollbar = false;
            this.SelectionMode = SelectionMode.One;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.IntegralHeight = true;
            this.Font = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, 8);
        }

        public void UpdateImageOnChange(Object sender) {
            this.Invalidate(this.GetItemRectangle(((CustomListItem)sender).Index));
        }

        protected override void OnSelectedIndexChanged(EventArgs e) {
            //base.OnSelectedIndexChanged(e);
            this.Invalidate();
        }

        //Flicker Free!
        protected override void OnPaint(PaintEventArgs e) {
            Region iRegion = new Region(e.ClipRectangle);
            e.Graphics.FillRegion(new SolidBrush(this.BackColor), iRegion);
            if (this.Items.Count > 0) {
                for (int i = 0; i < this.Items.Count; ++i) {
                    System.Drawing.Rectangle irect = this.GetItemRectangle(i);
                    if (e.ClipRectangle.IntersectsWith(irect)) {
                        OnDrawItem(new DrawItemEventArgs(e.Graphics, this.Font,
                            irect, i,
                            DrawItemState.Default, this.ForeColor,
                            this.BackColor));
                    }
                    iRegion.Complement(irect);
                }
            }

            base.OnPaint(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e) {
            CustomListItem item = (e.Index < 0 || this.DesignMode ? null : Items[e.Index] as CustomListItem);
            //bool draw = imageList != null && (item != null);
            if (item != null) {
                CheckHorizontalScroll(e.Graphics, customBoldFont);
                e.DrawBackground();
                base.OnDrawItem(e);

                Size imageSize = item.Pic.Size;
                if (this.ItemHeight != imageSize.Height + 2)
                    this.ItemHeight = imageSize.Height + 5;

                Rectangle bounds = e.Bounds;

                Color color = Color.Black;

                SolidBrush brush;
                if (item.Index == this.SelectedIndex) {
                    brush = new SolidBrush(Color.LightSteelBlue);
                } else
                    brush = new SolidBrush(Color.LightBlue);

                Pen pen = new Pen(Color.White);

                e.Graphics.DrawRectangle(pen, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
                e.Graphics.FillRectangle(brush, bounds.Left + 1, bounds.Top + 1, bounds.Width - 1, bounds.Height - 1);

                if (item.Pic.Image != null)
                    e.Graphics.DrawImage(item.Pic.Image, new Rectangle(bounds.Left + 2, bounds.Top + 2, item.Pic.Width, item.Pic.Height + 1));

                int textCount = 0;

                foreach (String s in item.textList) {
                    Rectangle itemBound = new Rectangle(bounds.Left, this.ItemHeight * textCount, bounds.Width, this.ItemHeight * textCount + this.ItemHeight);
                    e.Graphics.DrawString(s, (textCount == 0 ? customBoldFont : customRegularFont), new SolidBrush(color), item.Pic.Width + 5, bounds.Top + 5 + textCount * (e.Font.Size + 6));
                    textCount++;
                }
            }
        }

        protected void CheckHorizontalScroll(Graphics g, Font f) {
            // Determine the size for HorizontalExtent using the MeasureString method using the last item in the list.
            int maxWidth = 0;
            int hzSize = 0;
            int maxImageWidth = 0;
            foreach (CustomListItem item in this.Items) {
                maxImageWidth = item.Pic.Width;
                //first check name
                hzSize = (int)g.MeasureString(item.textList[0], f).Width;
                if (hzSize > maxWidth)
                    maxWidth = hzSize;

                //then check publisher
                hzSize = (int)g.MeasureString(item.textList[1], f).Width;
                if (hzSize > maxWidth)
                    maxWidth = hzSize;
            }
            // Set the HorizontalExtent property.
            this.HorizontalExtent = maxWidth + maxImageWidth;
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // CustomListbox
            // 
            this.Size = new System.Drawing.Size(200, 96);
            this.ResumeLayout(false);
        }
    }

    public class CustomListItem
    {
        public PictureBox Pic = new PictureBox();

        public int Index { get; set; }

        public System.Collections.Generic.List<String> textList = new System.Collections.Generic.List<String>();

        public event ImageChangedEvent ImageChangedEventHandler;

        protected virtual void OnImageChangedEvent() {
            if (ImageChangedEventHandler != null)
                ImageChangedEventHandler(this);
        }

        public CustomListItem()
            : this(null) {
        }

        public void RemoveEventHandlers() {
            // this.SetImageChangedHandler(null);
            Pic.LoadCompleted -= new System.ComponentModel.AsyncCompletedEventHandler(Pic_LoadCompleted);
        }

        public CustomListItem(String _text)
            : this(-1, _text) {
        }

        public CustomListItem(int _index, String _text) {
            Index = _index;
            textList.Add(_text);
            Pic.Image = ZeroWin.Properties.Resources.NoImage;
            // Pic.ImageLocation = null;
            Pic.Width = 200;
            Pic.Height = 150;
            Pic.SizeMode = PictureBoxSizeMode.StretchImage;
            Pic.LoadCompleted += new System.ComponentModel.AsyncCompletedEventHandler(Pic_LoadCompleted);
        }

        private void Pic_LoadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e) {
            OnImageChangedEvent();
        }

        public void AddText(String _text) {
            textList.Add(_text);
        }

        public void SetPicture(String url) {
            Pic.ImageLocation = url;
            Pic.LoadAsync();
        }

        public void SetImageChangedHandler(ImageChangedEvent eventHandler) {
            this.ImageChangedEventHandler += new ImageChangedEvent(eventHandler);
        }
    }

}