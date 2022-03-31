﻿namespace ZeroWin.Tools
{
    partial class Commander
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
            if(disposing && (components != null))
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
            this.scintillaOut = new ScintillaNET.Scintilla();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.scintillaIn = new ScintillaNET.Scintilla();
            this.panel1 = new System.Windows.Forms.Panel();
            this.statusStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // scintillaOut
            // 
            this.scintillaOut.Location = new System.Drawing.Point(3, 3);
            this.scintillaOut.Name = "scintillaOut";
            this.scintillaOut.Size = new System.Drawing.Size(515, 417);
            this.scintillaOut.TabIndex = 0;
            this.scintillaOut.Insert += new System.EventHandler<ScintillaNET.ModificationEventArgs>(this.scintillaOut_Insert);
            this.scintillaOut.InsertCheck += new System.EventHandler<ScintillaNET.InsertCheckEventArgs>(this.scintillaOut_InsertCheck);
            this.scintillaOut.UpdateUI += new System.EventHandler<ScintillaNET.UpdateUIEventArgs>(this.Commander_UpdateUI);
            this.scintillaOut.TextChanged += new System.EventHandler(this.scintillaOut_TextChanged);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 485);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(521, 22);
            this.statusStrip1.TabIndex = 1;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(118, 17);
            this.toolStripStatusLabel1.Text = "toolStripStatusLabel1";
            // 
            // scintillaIn
            // 
            this.scintillaIn.Location = new System.Drawing.Point(2, 426);
            this.scintillaIn.Name = "scintillaIn";
            this.scintillaIn.Size = new System.Drawing.Size(516, 55);
            this.scintillaIn.TabIndex = 2;
            this.scintillaIn.Text = "scintilla2";
            this.scintillaIn.InsertCheck += new System.EventHandler<ScintillaNET.InsertCheckEventArgs>(this.scintillaIn_InsertCheck);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.scintillaIn);
            this.panel1.Controls.Add(this.scintillaOut);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(521, 507);
            this.panel1.TabIndex = 3;
            // 
            // Commander
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(521, 507);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.panel1);
            this.Name = "Commander";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Commander";
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private ScintillaNET.Scintilla scintillaOut;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private ScintillaNET.Scintilla scintillaIn;
        private System.Windows.Forms.Panel panel1;
    }
}