namespace ZeroWin
{
    partial class Infoseeker
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.titleBox = new System.Windows.Forms.TextBox();
            this.searchButton = new System.Windows.Forms.Button();
            this.titleRadioButton = new System.Windows.Forms.RadioButton();
            this.publisherRadioButton = new System.Windows.Forms.RadioButton();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel2 = new System.Windows.Forms.ToolStripStatusLabel();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.moreButton = new System.Windows.Forms.Button();
            this.detailsButton = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // titleBox
            // 
            this.titleBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.titleBox.Location = new System.Drawing.Point(6, 39);
            this.titleBox.Name = "titleBox";
            this.titleBox.Size = new System.Drawing.Size(292, 20);
            this.titleBox.TabIndex = 1;
            this.titleBox.TextChanged += new System.EventHandler(this.titleBox_TextChanged);
            // 
            // searchButton
            // 
            this.searchButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.searchButton.Enabled = false;
            this.searchButton.Location = new System.Drawing.Point(304, 37);
            this.searchButton.Name = "searchButton";
            this.searchButton.Size = new System.Drawing.Size(71, 23);
            this.searchButton.TabIndex = 2;
            this.searchButton.Text = "Search";
            this.searchButton.UseVisualStyleBackColor = true;
            this.searchButton.Click += new System.EventHandler(this.searchButton_Click);
            // 
            // titleRadioButton
            // 
            this.titleRadioButton.AutoSize = true;
            this.titleRadioButton.Checked = true;
            this.titleRadioButton.Location = new System.Drawing.Point(70, 16);
            this.titleRadioButton.Name = "titleRadioButton";
            this.titleRadioButton.Size = new System.Drawing.Size(45, 17);
            this.titleRadioButton.TabIndex = 3;
            this.titleRadioButton.TabStop = true;
            this.titleRadioButton.Text = "Title";
            this.titleRadioButton.UseVisualStyleBackColor = true;
            // 
            // publisherRadioButton
            // 
            this.publisherRadioButton.AutoSize = true;
            this.publisherRadioButton.Location = new System.Drawing.Point(121, 16);
            this.publisherRadioButton.Name = "publisherRadioButton";
            this.publisherRadioButton.Size = new System.Drawing.Size(68, 17);
            this.publisherRadioButton.TabIndex = 4;
            this.publisherRadioButton.TabStop = true;
            this.publisherRadioButton.Text = "Publisher";
            this.publisherRadioButton.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            this.statusStrip1.AutoSize = false;
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1,
            this.toolStripStatusLabel2});
            this.statusStrip1.Location = new System.Drawing.Point(0, 459);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(391, 22);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top)
                        | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right)
                        | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.toolStripStatusLabel1.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.toolStripStatusLabel1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(188, 17);
            this.toolStripStatusLabel1.Spring = true;
            this.toolStripStatusLabel1.Text = "Ready";
            // 
            // toolStripStatusLabel2
            // 
            this.toolStripStatusLabel2.BorderSides = ((System.Windows.Forms.ToolStripStatusLabelBorderSides)((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left | System.Windows.Forms.ToolStripStatusLabelBorderSides.Top)
                        | System.Windows.Forms.ToolStripStatusLabelBorderSides.Right)
                        | System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom)));
            this.toolStripStatusLabel2.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner;
            this.toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            this.toolStripStatusLabel2.Size = new System.Drawing.Size(188, 17);
            this.toolStripStatusLabel2.Spring = true;
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.titleBox);
            this.groupBox1.Controls.Add(this.publisherRadioButton);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.titleRadioButton);
            this.groupBox1.Controls.Add(this.searchButton);
            this.groupBox1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.groupBox1.Location = new System.Drawing.Point(5, -1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(381, 67);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 18);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(58, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Search by:";
            // 
            // moreButton
            // 
            this.moreButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.moreButton.Location = new System.Drawing.Point(11, 433);
            this.moreButton.Name = "moreButton";
            this.moreButton.Size = new System.Drawing.Size(142, 23);
            this.moreButton.TabIndex = 17;
            this.moreButton.Text = "More results...";
            this.moreButton.UseVisualStyleBackColor = true;
            this.moreButton.Click += new System.EventHandler(this.moreButton_Click_1);
            // 
            // detailsButton
            // 
            this.detailsButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.detailsButton.Location = new System.Drawing.Point(238, 433);
            this.detailsButton.Name = "detailsButton";
            this.detailsButton.Size = new System.Drawing.Size(142, 23);
            this.detailsButton.TabIndex = 18;
            this.detailsButton.Text = "Program details...";
            this.detailsButton.UseVisualStyleBackColor = true;
            this.detailsButton.Click += new System.EventHandler(this.detailsButton_Click);
            // 
            // Infoseeker
            // 
            this.AcceptButton = this.searchButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(391, 481);
            this.Controls.Add(this.detailsButton);
            this.Controls.Add(this.moreButton);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.statusStrip1);
            this.Name = "Infoseeker";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Program Finder";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Infoseeker_FormClosed);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TextBox titleBox;
        private System.Windows.Forms.Button searchButton;
        private System.Windows.Forms.RadioButton titleRadioButton;
        private System.Windows.Forms.RadioButton publisherRadioButton;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button moreButton;
        private System.Windows.Forms.Button detailsButton;
    }
}