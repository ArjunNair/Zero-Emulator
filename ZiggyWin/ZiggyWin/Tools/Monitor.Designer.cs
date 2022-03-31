namespace ZeroWin
{
    partial class Monitor
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridView dataGridView1;
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Monitor));
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.monitorStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.saveFileDialog1 = new System.Windows.Forms.SaveFileDialog();
            this.toolStrip2 = new System.Windows.Forms.ToolStrip();
            this.ResumeEmulationButton = new System.Windows.Forms.ToolStripButton();
            this.RunToCursorButton = new System.Windows.Forms.ToolStripButton();
            this.StopDebuggerButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.StepInButton = new System.Windows.Forms.ToolStripButton();
            this.StepOutButton = new System.Windows.Forms.ToolStripButton();
            this.StepOverButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.ToggleBreakpointButton = new System.Windows.Forms.ToolStripButton();
            this.ClearAllBreakpointsButton = new System.Windows.Forms.ToolStripButton();
            this.BreakpointsButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.MemoryButton = new System.Windows.Forms.ToolStripButton();
            this.machineStateButton = new System.Windows.Forms.ToolStripButton();
            this.RegistersButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.ProfilerButton = new System.Windows.Forms.ToolStripButton();
            this.PokeMemoryButton = new System.Windows.Forms.ToolStripButton();
            this.callStackButton = new System.Windows.Forms.ToolStripButton();
            this.jumpAddrTextBox4 = new System.Windows.Forms.MaskedTextBox();
            this.jumpAddrButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadSymbolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.debugToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resumeEmulationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.runToCursorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stopDebuggingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.stepOverToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stepInToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.stepOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.breakpointsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toggleBreakpointToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.clearAllBreakpointsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.breakpointsEditorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pokeMemoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.watchMemoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.heatMapToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem4 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem2 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem3 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.aSCIICharactersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.numbersInHexToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.systemVariablesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            this.statusStrip1.SuspendLayout();
            this.toolStrip2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (dataGridView1)).BeginInit();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // statusStrip1
            // 
            this.statusStrip1.BackColor = System.Drawing.SystemColors.Control;
            this.statusStrip1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {this.monitorStatusLabel, this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 407);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Padding = new System.Windows.Forms.Padding(1, 0, 16, 0);
            this.statusStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.ManagerRenderMode;
            this.statusStrip1.Size = new System.Drawing.Size(372, 22);
            this.statusStrip1.SizingGrip = false;
            this.statusStrip1.TabIndex = 6;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // monitorStatusLabel
            // 
            this.monitorStatusLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.monitorStatusLabel.Name = "monitorStatusLabel";
            this.monitorStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.BorderStyle = System.Windows.Forms.Border3DStyle.Bump;
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(0, 17);
            // 
            // toolStrip2
            // 
            this.toolStrip2.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {this.ResumeEmulationButton, this.RunToCursorButton, this.StopDebuggerButton, this.toolStripSeparator2, this.StepInButton, this.StepOutButton, this.StepOverButton, this.toolStripSeparator3, this.ToggleBreakpointButton, this.ClearAllBreakpointsButton, this.BreakpointsButton, this.toolStripSeparator4, this.MemoryButton, this.machineStateButton, this.RegistersButton, this.toolStripSeparator5, this.ProfilerButton, this.PokeMemoryButton, this.callStackButton});
            this.toolStrip2.Location = new System.Drawing.Point(0, 24);
            this.toolStrip2.Name = "toolStrip2";
            this.toolStrip2.Size = new System.Drawing.Size(372, 25);
            this.toolStrip2.TabIndex = 7;
            this.toolStrip2.Text = "toolStrip2";
            // 
            // ResumeEmulationButton
            // 
            this.ResumeEmulationButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ResumeEmulationButton.Image = global::ZeroWin.Properties.Resources.PlayHS;
            this.ResumeEmulationButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ResumeEmulationButton.Name = "ResumeEmulationButton";
            this.ResumeEmulationButton.Size = new System.Drawing.Size(23, 22);
            this.ResumeEmulationButton.Text = "Resume Emulation";
            this.ResumeEmulationButton.ToolTipText = "Resume emulation";
            this.ResumeEmulationButton.Click += new System.EventHandler(this.ResumeEmulationButton_Click);
            // 
            // RunToCursorButton
            // 
            this.RunToCursorButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.RunToCursorButton.Image = global::ZeroWin.Properties.Resources.GoToSourceCode_6546;
            this.RunToCursorButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RunToCursorButton.Name = "RunToCursorButton";
            this.RunToCursorButton.Size = new System.Drawing.Size(23, 22);
            this.RunToCursorButton.Text = "Run to Cursor";
            this.RunToCursorButton.ToolTipText = "Run to cursor";
            this.RunToCursorButton.Click += new System.EventHandler(this.RunToCursorButton_Click);
            // 
            // StopDebuggerButton
            // 
            this.StopDebuggerButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StopDebuggerButton.Image = global::ZeroWin.Properties.Resources.StopHS;
            this.StopDebuggerButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StopDebuggerButton.Name = "StopDebuggerButton";
            this.StopDebuggerButton.Size = new System.Drawing.Size(23, 22);
            this.StopDebuggerButton.Text = "Stop Debugging";
            this.StopDebuggerButton.ToolTipText = "Stop Debugging";
            this.StopDebuggerButton.Click += new System.EventHandler(this.StopDebuggerButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // StepInButton
            // 
            this.StepInButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StepInButton.Image = global::ZeroWin.Properties.Resources.StepIn_6326;
            this.StepInButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StepInButton.Name = "StepInButton";
            this.StepInButton.Size = new System.Drawing.Size(23, 22);
            this.StepInButton.Text = "Step Into";
            this.StepInButton.ToolTipText = "Step Into";
            this.StepInButton.Click += new System.EventHandler(this.StepInButton_Click);
            // 
            // StepOutButton
            // 
            this.StepOutButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StepOutButton.Image = global::ZeroWin.Properties.Resources.Stepout_6327;
            this.StepOutButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StepOutButton.Name = "StepOutButton";
            this.StepOutButton.Size = new System.Drawing.Size(23, 22);
            this.StepOutButton.Text = "Step Out";
            this.StepOutButton.ToolTipText = "Step Out";
            this.StepOutButton.Click += new System.EventHandler(this.StepOutButton_Click);
            // 
            // StepOverButton
            // 
            this.StepOverButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.StepOverButton.Image = global::ZeroWin.Properties.Resources.StepOver_6328;
            this.StepOverButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.StepOverButton.Name = "StepOverButton";
            this.StepOverButton.Size = new System.Drawing.Size(23, 22);
            this.StepOverButton.Text = "Step Over";
            this.StepOverButton.ToolTipText = "Step Over";
            this.StepOverButton.Click += new System.EventHandler(this.StepOverButton_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 25);
            // 
            // ToggleBreakpointButton
            // 
            this.ToggleBreakpointButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ToggleBreakpointButton.Image = global::ZeroWin.Properties.Resources.ToggleAllBreakpoints_6554;
            this.ToggleBreakpointButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ToggleBreakpointButton.Name = "ToggleBreakpointButton";
            this.ToggleBreakpointButton.Size = new System.Drawing.Size(23, 22);
            this.ToggleBreakpointButton.Text = "Toggle Breakpoint";
            this.ToggleBreakpointButton.ToolTipText = "Toggle Breakpoint";
            this.ToggleBreakpointButton.Click += new System.EventHandler(this.ToggleBreakpointButton_Click);
            // 
            // ClearAllBreakpointsButton
            // 
            this.ClearAllBreakpointsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ClearAllBreakpointsButton.Image = global::ZeroWin.Properties.Resources.clearallbreakpoints_6551;
            this.ClearAllBreakpointsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ClearAllBreakpointsButton.Name = "ClearAllBreakpointsButton";
            this.ClearAllBreakpointsButton.Size = new System.Drawing.Size(23, 22);
            this.ClearAllBreakpointsButton.Text = "Clear All Breakpoints";
            this.ClearAllBreakpointsButton.ToolTipText = "Clear All Breakpoint";
            this.ClearAllBreakpointsButton.Click += new System.EventHandler(this.ClearAllBreakpointsButton_Click);
            // 
            // BreakpointsButton
            // 
            this.BreakpointsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.BreakpointsButton.Image = global::ZeroWin.Properties.Resources.BreakpointsWindow_6557;
            this.BreakpointsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.BreakpointsButton.Name = "BreakpointsButton";
            this.BreakpointsButton.Size = new System.Drawing.Size(23, 22);
            this.BreakpointsButton.Text = "Breakpoint Editor";
            this.BreakpointsButton.Click += new System.EventHandler(this.BreakpointsButton_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(6, 25);
            // 
            // MemoryButton
            // 
            this.MemoryButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.MemoryButton.Image = global::ZeroWin.Properties.Resources.MemoryWindow_6537;
            this.MemoryButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.MemoryButton.Name = "MemoryButton";
            this.MemoryButton.Size = new System.Drawing.Size(23, 22);
            this.MemoryButton.Text = "Memory Viewer";
            this.MemoryButton.Click += new System.EventHandler(this.MemoryButton_Click);
            // 
            // machineStateButton
            // 
            this.machineStateButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.machineStateButton.Image = global::ZeroWin.Properties.Resources.Processor;
            this.machineStateButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.machineStateButton.Name = "machineStateButton";
            this.machineStateButton.Size = new System.Drawing.Size(23, 22);
            this.machineStateButton.Text = "Machine State";
            this.machineStateButton.Click += new System.EventHandler(this.machineStateButton_Click);
            // 
            // RegistersButton
            // 
            this.RegistersButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.RegistersButton.Image = global::ZeroWin.Properties.Resources.RegistersWindow_6538;
            this.RegistersButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.RegistersButton.Name = "RegistersButton";
            this.RegistersButton.Size = new System.Drawing.Size(23, 22);
            this.RegistersButton.Text = "Registers";
            this.RegistersButton.Click += new System.EventHandler(this.RegistersButton_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(6, 25);
            // 
            // ProfilerButton
            // 
            this.ProfilerButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ProfilerButton.Image = global::ZeroWin.Properties.Resources.IntelliTrace_16x;
            this.ProfilerButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ProfilerButton.Name = "ProfilerButton";
            this.ProfilerButton.Size = new System.Drawing.Size(23, 22);
            this.ProfilerButton.Text = "Profiler";
            this.ProfilerButton.Click += new System.EventHandler(this.ProfilerButton_Click);
            // 
            // PokeMemoryButton
            // 
            this.PokeMemoryButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.PokeMemoryButton.Image = global::ZeroWin.Properties.Resources.PencilTool_206;
            this.PokeMemoryButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.PokeMemoryButton.Name = "PokeMemoryButton";
            this.PokeMemoryButton.Size = new System.Drawing.Size(23, 22);
            this.PokeMemoryButton.Text = "Poke Memory";
            this.PokeMemoryButton.ToolTipText = "Poke Memory";
            this.PokeMemoryButton.Click += new System.EventHandler(this.PokeMemoryButton_Click);
            // 
            // callStackButton
            // 
            this.callStackButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.callStackButton.Enabled = false;
            this.callStackButton.Image = global::ZeroWin.Properties.Resources.CallStackWindow_6561;
            this.callStackButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.callStackButton.Name = "callStackButton";
            this.callStackButton.Size = new System.Drawing.Size(23, 20);
            this.callStackButton.Text = "Stack";
            this.callStackButton.Visible = false;
            this.callStackButton.Click += new System.EventHandler(this.callStackButton_Click);
            // 
            // jumpAddrTextBox4
            // 
            this.jumpAddrTextBox4.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.jumpAddrTextBox4.Location = new System.Drawing.Point(83, 379);
            this.jumpAddrTextBox4.Name = "jumpAddrTextBox4";
            this.jumpAddrTextBox4.Size = new System.Drawing.Size(100, 23);
            this.jumpAddrTextBox4.TabIndex = 2;
            this.jumpAddrTextBox4.TextMaskFormat = System.Windows.Forms.MaskFormat.ExcludePromptAndLiterals;
            // 
            // jumpAddrButton
            // 
            this.jumpAddrButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.jumpAddrButton.Location = new System.Drawing.Point(194, 378);
            this.jumpAddrButton.Name = "jumpAddrButton";
            this.jumpAddrButton.Size = new System.Drawing.Size(75, 28);
            this.jumpAddrButton.TabIndex = 0;
            this.jumpAddrButton.Text = "Go";
            this.jumpAddrButton.UseVisualStyleBackColor = true;
            this.jumpAddrButton.Click += new System.EventHandler(this.jumpAddrButton_Click);
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = System.Drawing.Color.WhiteSmoke;
            dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.BackgroundColor = System.Drawing.SystemColors.Window;
            dataGridView1.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.RaisedVertical;
            dataGridView1.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridView1.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.GridColor = System.Drawing.SystemColors.ControlLightLight;
            dataGridView1.Location = new System.Drawing.Point(3, 54);
            dataGridView1.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle2;
            dataGridView1.RowHeadersWidth = 20;
            dataGridView1.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            dataGridViewCellStyle3.NullValue = null;
            dataGridView1.RowsDefaultCellStyle = dataGridViewCellStyle3;
            dataGridView1.RowTemplate.DefaultCellStyle.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            dataGridView1.RowTemplate.Height = 24;
            dataGridView1.RowTemplate.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            dataGridView1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            dataGridView1.ShowRowErrors = false;
            dataGridView1.Size = new System.Drawing.Size(369, 318);
            dataGridView1.TabIndex = 10;
            dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellDoubleClick);
            dataGridView1.RowPostPaint += new System.Windows.Forms.DataGridViewRowPostPaintEventHandler(this.dataGridView1_RowPostPaint);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 381);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 15);
            this.label1.TabIndex = 11;
            this.label1.Text = "Jump to:";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {this.fileToolStripMenuItem, this.debugToolStripMenuItem, this.breakpointsToolStripMenuItem, this.toolsToolStripMenuItem, this.viewToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(372, 24);
            this.menuStrip1.TabIndex = 13;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {this.saveToolStripMenuItem, this.loadSymbolsToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.disk;
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.saveToolStripMenuItem.Text = "Save...";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // loadSymbolsToolStripMenuItem
            // 
            this.loadSymbolsToolStripMenuItem.Name = "loadSymbolsToolStripMenuItem";
            this.loadSymbolsToolStripMenuItem.Size = new System.Drawing.Size(157, 22);
            this.loadSymbolsToolStripMenuItem.Text = "Load Symbols...";
            this.loadSymbolsToolStripMenuItem.ToolTipText = "Allows you to load custom symbols to represent memory locations.\r\nSymbols should " + "be stored as CSV in key-pairs like so:\r\n23606, CHARS\r\n42000, SPRITE DATA";
            this.loadSymbolsToolStripMenuItem.Click += new System.EventHandler(this.loadSymbolsToolStripMenuItem_Click);
            // 
            // debugToolStripMenuItem
            // 
            this.debugToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {this.resumeEmulationToolStripMenuItem, this.runToCursorToolStripMenuItem, this.stopDebuggingToolStripMenuItem, this.toolStripSeparator1, this.stepOverToolStripMenuItem, this.stepInToolStripMenuItem, this.stepOutToolStripMenuItem});
            this.debugToolStripMenuItem.Name = "debugToolStripMenuItem";
            this.debugToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.debugToolStripMenuItem.Text = "Debug";
            // 
            // resumeEmulationToolStripMenuItem
            // 
            this.resumeEmulationToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.PlayHS;
            this.resumeEmulationToolStripMenuItem.Name = "resumeEmulationToolStripMenuItem";
            this.resumeEmulationToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.resumeEmulationToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.resumeEmulationToolStripMenuItem.Text = "Resume";
            this.resumeEmulationToolStripMenuItem.Click += new System.EventHandler(this.resumeEmulationToolStripMenuItem_Click);
            // 
            // runToCursorToolStripMenuItem
            // 
            this.runToCursorToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.GoToSourceCode_6546;
            this.runToCursorToolStripMenuItem.Name = "runToCursorToolStripMenuItem";
            this.runToCursorToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.runToCursorToolStripMenuItem.Text = "Run To Cursor";
            this.runToCursorToolStripMenuItem.Click += new System.EventHandler(this.runToCursorToolStripMenuItem_Click);
            // 
            // stopDebuggingToolStripMenuItem
            // 
            this.stopDebuggingToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.StopHS;
            this.stopDebuggingToolStripMenuItem.Name = "stopDebuggingToolStripMenuItem";
            this.stopDebuggingToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.stopDebuggingToolStripMenuItem.Text = "Stop";
            this.stopDebuggingToolStripMenuItem.Click += new System.EventHandler(this.stopDebuggingToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(147, 6);
            // 
            // stepOverToolStripMenuItem
            // 
            this.stepOverToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.StepOver_6328;
            this.stepOverToolStripMenuItem.Name = "stepOverToolStripMenuItem";
            this.stepOverToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F10;
            this.stepOverToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.stepOverToolStripMenuItem.Text = "Step Over";
            this.stepOverToolStripMenuItem.Click += new System.EventHandler(this.stepOverToolStripMenuItem_Click);
            // 
            // stepInToolStripMenuItem
            // 
            this.stepInToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.StepIn_6326;
            this.stepInToolStripMenuItem.Name = "stepInToolStripMenuItem";
            this.stepInToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F11;
            this.stepInToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.stepInToolStripMenuItem.Text = "Step In";
            this.stepInToolStripMenuItem.Click += new System.EventHandler(this.stepInToolStripMenuItem_Click);
            // 
            // stepOutToolStripMenuItem
            // 
            this.stepOutToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.Stepout_6327;
            this.stepOutToolStripMenuItem.Name = "stepOutToolStripMenuItem";
            this.stepOutToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F12;
            this.stepOutToolStripMenuItem.Size = new System.Drawing.Size(150, 22);
            this.stepOutToolStripMenuItem.Text = "Step Out";
            this.stepOutToolStripMenuItem.Click += new System.EventHandler(this.stepOutToolStripMenuItem_Click);
            // 
            // breakpointsToolStripMenuItem
            // 
            this.breakpointsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {this.toggleBreakpointToolStripMenuItem, this.clearAllBreakpointsToolStripMenuItem});
            this.breakpointsToolStripMenuItem.Name = "breakpointsToolStripMenuItem";
            this.breakpointsToolStripMenuItem.Size = new System.Drawing.Size(81, 20);
            this.breakpointsToolStripMenuItem.Text = "Breakpoints";
            // 
            // toggleBreakpointToolStripMenuItem
            // 
            this.toggleBreakpointToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.ToggleAllBreakpoints_6554;
            this.toggleBreakpointToolStripMenuItem.Name = "toggleBreakpointToolStripMenuItem";
            this.toggleBreakpointToolStripMenuItem.Size = new System.Drawing.Size(183, 22);
            this.toggleBreakpointToolStripMenuItem.Text = "Toggle Breakpoint";
            this.toggleBreakpointToolStripMenuItem.Click += new System.EventHandler(this.toggleBreakpointToolStripMenuItem_Click);
            // 
            // clearAllBreakpointsToolStripMenuItem
            // 
            this.clearAllBreakpointsToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.clearallbreakpoints_6551;
            this.clearAllBreakpointsToolStripMenuItem.Name = "clearAllBreakpointsToolStripMenuItem";
            this.clearAllBreakpointsToolStripMenuItem.Size = new System.Drawing.Size(183, 22);
            this.clearAllBreakpointsToolStripMenuItem.Text = "Clear All Breakpoints";
            this.clearAllBreakpointsToolStripMenuItem.Click += new System.EventHandler(this.clearAllBreakpointsToolStripMenuItem_Click);
            // 
            // toolsToolStripMenuItem
            // 
            this.toolsToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {this.breakpointsEditorToolStripMenuItem, this.pokeMemoryToolStripMenuItem, this.watchMemoryToolStripMenuItem, this.heatMapToolStripMenuItem});
            this.toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            this.toolsToolStripMenuItem.Size = new System.Drawing.Size(46, 20);
            this.toolsToolStripMenuItem.Text = "Tools";
            this.toolsToolStripMenuItem.Click += new System.EventHandler(this.toolsToolStripMenuItem_Click);
            // 
            // breakpointsEditorToolStripMenuItem
            // 
            this.breakpointsEditorToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.BreakpointsWindow_6557;
            this.breakpointsEditorToolStripMenuItem.Name = "breakpointsEditorToolStripMenuItem";
            this.breakpointsEditorToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.B)));
            this.breakpointsEditorToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.breakpointsEditorToolStripMenuItem.Text = "Breakpoints Editor";
            this.breakpointsEditorToolStripMenuItem.Click += new System.EventHandler(this.breakpointsEditorToolStripMenuItem_Click);
            // 
            // pokeMemoryToolStripMenuItem
            // 
            this.pokeMemoryToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.PencilTool_206;
            this.pokeMemoryToolStripMenuItem.Name = "pokeMemoryToolStripMenuItem";
            this.pokeMemoryToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.P)));
            this.pokeMemoryToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.pokeMemoryToolStripMenuItem.Text = "Poke Memory";
            this.pokeMemoryToolStripMenuItem.Click += new System.EventHandler(this.pokeMemoryToolStripMenuItem_Click);
            // 
            // watchMemoryToolStripMenuItem
            // 
            this.watchMemoryToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.magnifier_16xLG;
            this.watchMemoryToolStripMenuItem.Name = "watchMemoryToolStripMenuItem";
            this.watchMemoryToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.W)));
            this.watchMemoryToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.watchMemoryToolStripMenuItem.Text = "Watch Memory";
            this.watchMemoryToolStripMenuItem.Click += new System.EventHandler(this.watchMemoryToolStripMenuItem_Click);
            // 
            // heatMapToolStripMenuItem
            // 
            this.heatMapToolStripMenuItem.Image = global::ZeroWin.Properties.Resources.library_16xLG;
            this.heatMapToolStripMenuItem.Name = "heatMapToolStripMenuItem";
            this.heatMapToolStripMenuItem.Size = new System.Drawing.Size(207, 22);
            this.heatMapToolStripMenuItem.Text = "Memory Profiler";
            this.heatMapToolStripMenuItem.Click += new System.EventHandler(this.heatMapToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {this.toolStripMenuItem4, this.toolStripMenuItem1, this.toolStripMenuItem2, this.toolStripMenuItem3, this.toolStripSeparator7, this.aSCIICharactersToolStripMenuItem, this.numbersInHexToolStripMenuItem, this.systemVariablesToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // toolStripMenuItem4
            // 
            this.toolStripMenuItem4.Image = global::ZeroWin.Properties.Resources.IntelliTrace_16x;
            this.toolStripMenuItem4.Name = "toolStripMenuItem4";
            this.toolStripMenuItem4.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.L)));
            this.toolStripMenuItem4.Size = new System.Drawing.Size(185, 22);
            this.toolStripMenuItem4.Text = "Execution Log";
            this.toolStripMenuItem4.Click += new System.EventHandler(this.executionLogToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Image = global::ZeroWin.Properties.Resources.Processor;
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.S)));
            this.toolStripMenuItem1.Size = new System.Drawing.Size(185, 22);
            this.toolStripMenuItem1.Text = "Machine State";
            this.toolStripMenuItem1.Click += new System.EventHandler(this.machineStateToolStripMenuItem_Click);
            // 
            // toolStripMenuItem2
            // 
            this.toolStripMenuItem2.Image = global::ZeroWin.Properties.Resources.MemoryWindow_6537;
            this.toolStripMenuItem2.Name = "toolStripMenuItem2";
            this.toolStripMenuItem2.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.V)));
            this.toolStripMenuItem2.Size = new System.Drawing.Size(185, 22);
            this.toolStripMenuItem2.Text = "Memory";
            this.toolStripMenuItem2.Click += new System.EventHandler(this.memoryViewerToolStripMenuItem_Click);
            // 
            // toolStripMenuItem3
            // 
            this.toolStripMenuItem3.Image = global::ZeroWin.Properties.Resources.RegistersWindow_6538;
            this.toolStripMenuItem3.Name = "toolStripMenuItem3";
            this.toolStripMenuItem3.ShortcutKeys = ((System.Windows.Forms.Keys) ((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.R)));
            this.toolStripMenuItem3.Size = new System.Drawing.Size(185, 22);
            this.toolStripMenuItem3.Text = "Registers";
            this.toolStripMenuItem3.Click += new System.EventHandler(this.registersToolStripMenuItem_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(182, 6);
            // 
            // aSCIICharactersToolStripMenuItem
            // 
            this.aSCIICharactersToolStripMenuItem.CheckOnClick = true;
            this.aSCIICharactersToolStripMenuItem.Name = "aSCIICharactersToolStripMenuItem";
            this.aSCIICharactersToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.aSCIICharactersToolStripMenuItem.Text = "ASCII characters";
            this.aSCIICharactersToolStripMenuItem.CheckedChanged += new System.EventHandler(this.aSCIICharactersToolStripMenuItem_CheckedChanged);
            // 
            // numbersInHexToolStripMenuItem
            // 
            this.numbersInHexToolStripMenuItem.CheckOnClick = true;
            this.numbersInHexToolStripMenuItem.Name = "numbersInHexToolStripMenuItem";
            this.numbersInHexToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.numbersInHexToolStripMenuItem.Text = "Hex Numbers";
            this.numbersInHexToolStripMenuItem.CheckedChanged += new System.EventHandler(this.numbersInHexToolStripMenuItem_CheckedChanged);
            // 
            // systemVariablesToolStripMenuItem
            // 
            this.systemVariablesToolStripMenuItem.Checked = true;
            this.systemVariablesToolStripMenuItem.CheckOnClick = true;
            this.systemVariablesToolStripMenuItem.CheckState = System.Windows.Forms.CheckState.Checked;
            this.systemVariablesToolStripMenuItem.Name = "systemVariablesToolStripMenuItem";
            this.systemVariablesToolStripMenuItem.Size = new System.Drawing.Size(185, 22);
            this.systemVariablesToolStripMenuItem.Text = "System Variables";
            this.systemVariablesToolStripMenuItem.CheckedChanged += new System.EventHandler(this.systemVariablesToolStripMenuItem_CheckedChanged);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // Monitor
            // 
            this.AcceptButton = this.jumpAddrButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(372, 429);
            this.Controls.Add(this.label1);
            this.Controls.Add(dataGridView1);
            this.Controls.Add(this.jumpAddrTextBox4);
            this.Controls.Add(this.jumpAddrButton);
            this.Controls.Add(this.toolStrip2);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.Font = new System.Drawing.Font("Consolas", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Icon = ((System.Drawing.Icon) (resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "Monitor";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Monitor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Monitor_FormClosing);
            this.Load += new System.EventHandler(this.Monitor_Load);
            this.Shown += new System.EventHandler(this.Monitor_Shown);
            this.VisibleChanged += new System.EventHandler(this.Monitor_VisibleChanged);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.toolStrip2.ResumeLayout(false);
            this.toolStrip2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize) (dataGridView1)).EndInit();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel monitorStatusLabel;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog1;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton ResumeEmulationButton;
        private System.Windows.Forms.ToolStripButton RunToCursorButton;
        private System.Windows.Forms.ToolStripButton StepInButton;
        private System.Windows.Forms.ToolStripButton StepOutButton;
        private System.Windows.Forms.ToolStripButton StopDebuggerButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton StepOverButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripButton ToggleBreakpointButton;
        private System.Windows.Forms.ToolStripButton ClearAllBreakpointsButton;
        private System.Windows.Forms.ToolStripButton BreakpointsButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripButton MemoryButton;
        private System.Windows.Forms.ToolStripButton RegistersButton;
        private System.Windows.Forms.ToolStripButton ProfilerButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripButton PokeMemoryButton;
        private System.Windows.Forms.Button jumpAddrButton;
        private System.Windows.Forms.MaskedTextBox jumpAddrTextBox4;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem debugToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resumeEmulationToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem runToCursorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stopDebuggingToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem stepInToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stepOutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem stepOverToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem breakpointsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toggleBreakpointToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem clearAllBreakpointsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem breakpointsEditorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pokeMemoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aSCIICharactersToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem numbersInHexToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem systemVariablesToolStripMenuItem;
        private System.Windows.Forms.ToolStripButton machineStateButton;
        private System.Windows.Forms.ToolStripButton callStackButton;
        private System.Windows.Forms.ToolStripMenuItem loadSymbolsToolStripMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem4;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem2;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem3;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripMenuItem watchMemoryToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem heatMapToolStripMenuItem;
    }
}