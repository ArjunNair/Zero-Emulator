namespace ZeroWin
{
    partial class Infoviewer
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Infoviewer));
            this.panel1 = new System.Windows.Forms.Panel();
            this.label2 = new System.Windows.Forms.Label();
            this.authorsLabel = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.loadingScreen = new System.Windows.Forms.PictureBox();
            this.ingameScreen = new System.Windows.Forms.PictureBox();
            this.checkedListBox1 = new System.Windows.Forms.CheckedListBox();
            this.button1 = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.authorLabel = new System.Windows.Forms.Label();
            this.machineTypeLabel = new System.Windows.Forms.Label();
            this.machineLabel = new System.Windows.Forms.Label();
            this.controlsLabel = new System.Windows.Forms.Label();
            this.controlTypeLabel = new System.Windows.Forms.Label();
            this.availabilityLabel = new System.Windows.Forms.Label();
            this.availabilityTypeLabel = new System.Windows.Forms.Label();
            this.publicationLabel = new System.Windows.Forms.Label();
            this.publicationTypeLabel = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.autoLoadComboBox = new System.Windows.Forms.ComboBox();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.pictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize) (this.loadingScreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize) (this.ingameScreen)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.SystemColors.Control;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.authorsLabel);
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Controls.Add(this.loadingScreen);
            this.panel1.Controls.Add(this.ingameScreen);
            this.panel1.Controls.Add(this.checkedListBox1);
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.groupBox1);
            this.panel1.Controls.Add(this.groupBox2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(681, 617);
            this.panel1.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label2.Location = new System.Drawing.Point(0, 213);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(88, 13);
            this.label2.TabIndex = 23;
            this.label2.Text = "Available files:";
            // 
            // authorsLabel
            // 
            this.authorsLabel.AutoSize = true;
            this.authorsLabel.BackColor = System.Drawing.Color.Transparent;
            this.authorsLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.authorsLabel.ForeColor = System.Drawing.Color.Black;
            this.authorsLabel.Location = new System.Drawing.Point(10, 12);
            this.authorsLabel.Name = "authorsLabel";
            this.authorsLabel.Size = new System.Drawing.Size(52, 13);
            this.authorsLabel.TabIndex = 2;
            this.authorsLabel.Text = "Authors";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBox1.BackColor = System.Drawing.Color.Gray;
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(420, 6);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(256, 192);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 17;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Click += new System.EventHandler(this.pictureBox1_Click);
            // 
            // loadingScreen
            // 
            this.loadingScreen.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.loadingScreen.BackColor = System.Drawing.Color.Gray;
            this.loadingScreen.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.loadingScreen.InitialImage = ((System.Drawing.Image) (resources.GetObject("loadingScreen.InitialImage")));
            this.loadingScreen.Location = new System.Drawing.Point(420, 202);
            this.loadingScreen.Name = "loadingScreen";
            this.loadingScreen.Size = new System.Drawing.Size(256, 192);
            this.loadingScreen.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.loadingScreen.TabIndex = 0;
            this.loadingScreen.TabStop = false;
            this.loadingScreen.Click += new System.EventHandler(this.loadingScreen_Click);
            // 
            // ingameScreen
            // 
            this.ingameScreen.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.ingameScreen.BackColor = System.Drawing.Color.Gray;
            this.ingameScreen.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ingameScreen.InitialImage = ((System.Drawing.Image) (resources.GetObject("ingameScreen.InitialImage")));
            this.ingameScreen.Location = new System.Drawing.Point(420, 399);
            this.ingameScreen.Name = "ingameScreen";
            this.ingameScreen.Size = new System.Drawing.Size(256, 192);
            this.ingameScreen.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.ingameScreen.TabIndex = 1;
            this.ingameScreen.TabStop = false;
            this.ingameScreen.Click += new System.EventHandler(this.ingameScreen_Click);
            // 
            // checkedListBox1
            // 
            this.checkedListBox1.Anchor = ((System.Windows.Forms.AnchorStyles) (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left)));
            this.checkedListBox1.CheckOnClick = true;
            this.checkedListBox1.FormattingEnabled = true;
            this.checkedListBox1.HorizontalScrollbar = true;
            this.checkedListBox1.IntegralHeight = false;
            this.checkedListBox1.Location = new System.Drawing.Point(3, 229);
            this.checkedListBox1.Name = "checkedListBox1";
            this.checkedListBox1.Size = new System.Drawing.Size(411, 226);
            this.checkedListBox1.TabIndex = 18;
            this.checkedListBox1.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.checkedListBox1_ItemCheck);
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.button1.Location = new System.Drawing.Point(44, 461);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(308, 26);
            this.button1.TabIndex = 16;
            this.button1.Text = "Download";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.authorLabel);
            this.groupBox1.Controls.Add(this.machineTypeLabel);
            this.groupBox1.Controls.Add(this.machineLabel);
            this.groupBox1.Controls.Add(this.controlsLabel);
            this.groupBox1.Controls.Add(this.controlTypeLabel);
            this.groupBox1.Controls.Add(this.availabilityLabel);
            this.groupBox1.Controls.Add(this.availabilityTypeLabel);
            this.groupBox1.Controls.Add(this.publicationLabel);
            this.groupBox1.Controls.Add(this.publicationTypeLabel);
            this.groupBox1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.groupBox1.Location = new System.Drawing.Point(3, 1);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(411, 197);
            this.groupBox1.TabIndex = 21;
            this.groupBox1.TabStop = false;
            // 
            // authorLabel
            // 
            this.authorLabel.BackColor = System.Drawing.Color.Transparent;
            this.authorLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.authorLabel.ForeColor = System.Drawing.Color.Black;
            this.authorLabel.Location = new System.Drawing.Point(7, 24);
            this.authorLabel.Name = "authorLabel";
            this.authorLabel.Size = new System.Drawing.Size(243, 13);
            this.authorLabel.TabIndex = 9;
            this.authorLabel.Text = "Unknown";
            // 
            // machineTypeLabel
            // 
            this.machineTypeLabel.AutoSize = true;
            this.machineTypeLabel.BackColor = System.Drawing.Color.Transparent;
            this.machineTypeLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.machineTypeLabel.ForeColor = System.Drawing.Color.Black;
            this.machineTypeLabel.Location = new System.Drawing.Point(7, 122);
            this.machineTypeLabel.Name = "machineTypeLabel";
            this.machineTypeLabel.Size = new System.Drawing.Size(54, 13);
            this.machineTypeLabel.TabIndex = 7;
            this.machineTypeLabel.Text = "Machine";
            // 
            // machineLabel
            // 
            this.machineLabel.BackColor = System.Drawing.Color.Transparent;
            this.machineLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.machineLabel.ForeColor = System.Drawing.Color.Black;
            this.machineLabel.Location = new System.Drawing.Point(7, 135);
            this.machineLabel.Name = "machineLabel";
            this.machineLabel.Size = new System.Drawing.Size(243, 13);
            this.machineLabel.TabIndex = 14;
            this.machineLabel.Text = "Unknown";
            this.machineLabel.Click += new System.EventHandler(this.machineLabel_Click);
            // 
            // controlsLabel
            // 
            this.controlsLabel.BackColor = System.Drawing.Color.Transparent;
            this.controlsLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.controlsLabel.ForeColor = System.Drawing.Color.Black;
            this.controlsLabel.Location = new System.Drawing.Point(7, 172);
            this.controlsLabel.Name = "controlsLabel";
            this.controlsLabel.Size = new System.Drawing.Size(243, 13);
            this.controlsLabel.TabIndex = 12;
            this.controlsLabel.Text = "Unknown";
            // 
            // controlTypeLabel
            // 
            this.controlTypeLabel.AutoSize = true;
            this.controlTypeLabel.BackColor = System.Drawing.Color.Transparent;
            this.controlTypeLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.controlTypeLabel.ForeColor = System.Drawing.Color.Black;
            this.controlTypeLabel.Location = new System.Drawing.Point(7, 159);
            this.controlTypeLabel.Name = "controlTypeLabel";
            this.controlTypeLabel.Size = new System.Drawing.Size(54, 13);
            this.controlTypeLabel.TabIndex = 4;
            this.controlTypeLabel.Text = "Controls";
            // 
            // availabilityLabel
            // 
            this.availabilityLabel.BackColor = System.Drawing.Color.Transparent;
            this.availabilityLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.availabilityLabel.ForeColor = System.Drawing.Color.Black;
            this.availabilityLabel.Location = new System.Drawing.Point(7, 100);
            this.availabilityLabel.Name = "availabilityLabel";
            this.availabilityLabel.Size = new System.Drawing.Size(244, 13);
            this.availabilityLabel.TabIndex = 11;
            this.availabilityLabel.Text = "Unknown";
            // 
            // availabilityTypeLabel
            // 
            this.availabilityTypeLabel.AutoSize = true;
            this.availabilityTypeLabel.BackColor = System.Drawing.Color.Transparent;
            this.availabilityTypeLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.availabilityTypeLabel.ForeColor = System.Drawing.Color.Black;
            this.availabilityTypeLabel.Location = new System.Drawing.Point(7, 87);
            this.availabilityTypeLabel.Name = "availabilityTypeLabel";
            this.availabilityTypeLabel.Size = new System.Drawing.Size(70, 13);
            this.availabilityTypeLabel.TabIndex = 5;
            this.availabilityTypeLabel.Text = "Availability";
            // 
            // publicationLabel
            // 
            this.publicationLabel.BackColor = System.Drawing.Color.Transparent;
            this.publicationLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.publicationLabel.ForeColor = System.Drawing.Color.Black;
            this.publicationLabel.Location = new System.Drawing.Point(7, 62);
            this.publicationLabel.Name = "publicationLabel";
            this.publicationLabel.Size = new System.Drawing.Size(243, 13);
            this.publicationLabel.TabIndex = 10;
            this.publicationLabel.Text = "Unknown";
            // 
            // publicationTypeLabel
            // 
            this.publicationTypeLabel.AutoSize = true;
            this.publicationTypeLabel.BackColor = System.Drawing.Color.Transparent;
            this.publicationTypeLabel.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.publicationTypeLabel.ForeColor = System.Drawing.Color.Black;
            this.publicationTypeLabel.Location = new System.Drawing.Point(7, 49);
            this.publicationTypeLabel.Name = "publicationTypeLabel";
            this.publicationTypeLabel.Size = new System.Drawing.Size(59, 13);
            this.publicationTypeLabel.TabIndex = 3;
            this.publicationTypeLabel.Text = "Publisher";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.autoLoadComboBox);
            this.groupBox2.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.groupBox2.Location = new System.Drawing.Point(3, 508);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(411, 83);
            this.groupBox2.TabIndex = 22;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Auto-Load";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.label1.Location = new System.Drawing.Point(6, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(224, 13);
            this.label1.TabIndex = 21;
            this.label1.Text = "When downloads complete, auto-load this file:";
            // 
            // autoLoadComboBox
            // 
            this.autoLoadComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.autoLoadComboBox.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.autoLoadComboBox.FormattingEnabled = true;
            this.autoLoadComboBox.Location = new System.Drawing.Point(6, 39);
            this.autoLoadComboBox.Name = "autoLoadComboBox";
            this.autoLoadComboBox.Size = new System.Drawing.Size(393, 21);
            this.autoLoadComboBox.TabIndex = 20;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 595);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(681, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(0, 17);
            // 
            // Infoviewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(681, 617);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Infoviewer";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Program Details";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Infoviewer_FormClosing);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) (this.pictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize) (this.loadingScreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize) (this.ingameScreen)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox loadingScreen;
        private System.Windows.Forms.PictureBox ingameScreen;
        private System.Windows.Forms.Label availabilityTypeLabel;
        private System.Windows.Forms.Label controlTypeLabel;
        private System.Windows.Forms.Label publicationTypeLabel;
        private System.Windows.Forms.Label authorsLabel;
        private System.Windows.Forms.Label machineTypeLabel;
        private System.Windows.Forms.Label machineLabel;
        private System.Windows.Forms.Label controlsLabel;
        private System.Windows.Forms.Label availabilityLabel;
        private System.Windows.Forms.Label publicationLabel;
        private System.Windows.Forms.Label authorLabel;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.CheckedListBox checkedListBox1;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ComboBox autoLoadComboBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}