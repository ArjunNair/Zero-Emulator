using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZeroWin;
using Peripherals;
using Speccy;

namespace ZeroWin
{
    public partial class TapeDeck : Form
    {
        public Form1 ziggyWin;
        private string tapFileName;
        public bool tapePresent = false;
        public bool isPlaying = false;
        private bool tapeIsInserted;
        private bool tapFileOpen = false;
        private bool isPauseBlockPreproccess = false; //To ensure previous edge is finished correctly
        private TapeInfo tapeInfo = new TapeInfo();

        public bool DoTapeAutoStart {
            get {

                return autoPlayStopToolStripMenuItem.Checked;
            }

            set {
                autoPlayStopToolStripMenuItem.Checked = value;
            }
        }

        public bool DoAutoTapeLoad {
            get {
                return autoLoadTapesToolStripMenuItem.Checked;
            }

            set {
                autoLoadTapesToolStripMenuItem.Checked = value;
            }
        }

        public bool DoTapeEdgeLoad {
            get {
                return edgeLoadToolStripMenuItem.Checked;
            }

            set {
                edgeLoadToolStripMenuItem.Checked = value;
            }
        }

        public bool DoTapeAccelerateLoad {
            get {
                return fastLoadToolStripMenuItem.Checked;
            }

            set {
                fastLoadToolStripMenuItem.Checked = value;
            }
        }

        public bool TapeIsInserted {
            get { return tapeIsInserted; }
        }

        private int progressStep = 10;

        public TapeDeck(Form1 zw) {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            ziggyWin = zw;
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            RegisterEventHooks();
            // dataGridView1.DoubleBuffered(true);
        }

        public void RegisterEventHooks() {
            ziggyWin.zx.TapeEvent += new TapeEventHandler(Deck_TapeEvent);
            ziggyWin.zx.edgeLoadTapes = edgeLoadToolStripMenuItem.Checked;
            ziggyWin.zx.tape_AutoPlay = autoPlayStopToolStripMenuItem.Checked;
            ziggyWin.zx.tape_readToPlay = tapeIsInserted;
        }

        public void UnRegisterEventHooks() {
            ziggyWin.zx.TapeEvent -= new TapeEventHandler(Deck_TapeEvent);
        }

        private void dataGridView1_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e) {
            /*  DataGridView gridView = sender as DataGridView;

              if (null != gridView)
              {
                  gridView.Rows[e.RowIndex].HeaderCell.Value = e.RowIndex.ToString();
              }
             */
        }

        private void SaveTAP() {
            if (!tapFileOpen && tapeIsInserted)
                ejectButton_Click(this, null);

            if (!tapFileOpen) {
                ziggyWin.showTapeIndicator = true;
                saveFileDialog1.FileName = "";
                saveFileDialog1.Title = "Save TAP file";
                saveFileDialog1.Filter = "TAP Tape|*.tap";
                saveFileDialog1.OverwritePrompt = false;
                if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                    if (System.IO.File.Exists(saveFileDialog1.FileName)) {
                        DialogResult result = MessageBox.Show("Do you wish to append to this file? Choose Yes to append, No to create a new file or Cancel to erm... cancel this operation.", "File already exists", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                        if (result == System.Windows.Forms.DialogResult.No) {
                            System.IO.FileStream fs = System.IO.File.Create(saveFileDialog1.FileName);
                            fs.Close();
                        } else if (result == System.Windows.Forms.DialogResult.Cancel)
                            return;
                    }
                    tapFileName = saveFileDialog1.FileName;
                    tapFileOpen = true;
                }
            }

            if (tapFileOpen) {
                using (System.IO.FileStream tapFile = new System.IO.FileStream(tapFileName, System.IO.FileMode.Append)) {
                    using (System.IO.BinaryWriter r = new System.IO.BinaryWriter(tapFile)) {
                        int blockID = ziggyWin.zx._AF >> 8; //block ID in A register
                        int startAddr = ziggyWin.zx.IX;     //start address in IX
                        int tapLength = ziggyWin.zx.DE;     //Length of data in DE
                        int checksum = blockID;

                        r.Write((short)(tapLength + 2));
                        r.Write((byte)(blockID));
                        for (int f = startAddr; f < (startAddr + tapLength); f++) {
                            byte data = (byte)(ziggyWin.zx.PeekByteNoContend(f));
                            r.Write(data);
                            checksum = checksum ^ data;
                        }
                        r.Write((byte)(checksum));
                        if (blockID == 0xff) {
                            //tapFileOpen = false;
                            ziggyWin.showTapeIndicator = false;
                        }
                    }
                }
            }
        }

        public void CloseTAP() {
            tapFileOpen = false;
            InsertTape(tapFileName);
        }

        public void Deck_TapeEvent(Object sender, Speccy.TapeEventArgs e) {
            if (!tapeIsInserted && e.EventType != Speccy.TapeEventType.SAVE_TAP && e.EventType != Speccy.TapeEventType.CLOSE_TAP)
                return;
            
            if (e.EventType == Speccy.TapeEventType.STOP_TAPE) //stop
            {
                ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
                ziggyWin.showTapeIndicator = false;
                ziggyWin.zx.MuteSound(!ziggyWin.config.EnableSound);
                progressBar1.Value = 0;
                progressBar1.Maximum = 100;
                progressStep = 0;
                progressBar1.Refresh();
                statusStrip1.Items[0].Text = "Tape stopped.";
                if (ziggyWin.zx.keyBuffer[(int)ZeroWin.Form1.keyCode.ALT])
                    ziggyWin.saveSnapshotMenuItem_Click(this, null);
            } 
            else if (e.EventType == Speccy.TapeEventType.SAVE_TAP) //Save TAP
            {
                SaveTAP();
            } 
            else if (e.EventType == Speccy.TapeEventType.CLOSE_TAP) //Close TAP
            {
                CloseTAP();
            } else if (e.EventType == Speccy.TapeEventType.START_TAPE) {
                if (!tapeIsInserted)
                    return;

                isPlaying = true;
                ziggyWin.showTapeIndicator = true;
                ziggyWin.zx.tapeIsPlaying = true;
                ziggyWin.zx.tapeTStates = 0;
                ziggyWin.zx.tape_readToPlay = true;
                if (ziggyWin.tapeFastLoad) {
                    ziggyWin.zx.SetEmulationSpeed(0);
                    ziggyWin.zx.MuteSound(true);
                    ziggyWin.zx.ResetKeyboard();
                } else {
                    ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
                    if (!ziggyWin.config.EnableSound)
                        ziggyWin.zx.MuteSound(true);
                }
            } else if (e.EventType == TapeEventType.NEXT_BLOCK) {
                progressBar1.Value = 0;
                progressBar1.Maximum = 100;
                progressStep = 10;

                if (ziggyWin.zx.currentBlock is PZXLoader.PULS_Block) {
                    //Prepare progress bar
                    int tcount = 1;
                    for (int i = 0; i < ((PZXLoader.PULS_Block)ziggyWin.zx.currentBlock).pulse.Count; ++i) {
                        for (int g = 0; g < ((PZXLoader.PULS_Block)ziggyWin.zx.currentBlock).pulse[i].count; g++)
                            tcount++;
                    }
                    progressBar1.Maximum = tcount + 1;
                    progressStep = 1;
                } else if (ziggyWin.zx.currentBlock is PZXLoader.DATA_Block) {
                    progressBar1.Maximum = (int)((PZXLoader.DATA_Block)ziggyWin.zx.currentBlock).count + 1;
                    progressStep = 1;

                }
                dataGridView1.Rows[ziggyWin.zx.blockCounter - 1].Selected = true;
                dataGridView1.CurrentCell = dataGridView1.Rows[ziggyWin.zx.blockCounter - 1].Cells[0];
                statusStrip1.Items[0].Text = "Playing block " + ziggyWin.zx.blockCounter;
            }
        }

        public bool NextDataBit() {
            progressBar1.Value += progressStep;
            return false;
        }

        private void ReadTape(String filename) {
            PZXLoader.ReadTapeInfo(filename);
            dataGridView1.DataSource = PZXLoader.tapeBlockInfo;
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridView1.Columns[0].FillWeight = 0.25f;
            dataGridView1.Columns[1].FillWeight = 0.75f;
            statusStrip1.Items[0].Text = "Ready to play";
            for (int i = 0; i < PZXLoader.blocks.Count; i++) {
                if (PZXLoader.blocks[i] is PZXLoader.PZXT_Header) {
                    PZXLoader.PZXT_Header info = (PZXLoader.PZXT_Header)PZXLoader.blocks[i];

                    string tapeText = String.Format("Publisher\t: {0}\r\nAuthor(s)\t: ", info.Publisher);
   
                    if (info.Title != null)
                        this.Text = info.Title;
                    else {
                        //Extract the filename from the path to display as tape name
                        string[] filePath = filename.Split('\\');
                        this.Text = filePath[filePath.Length - 1];
                        this.Text = this.Text.Substring(0, this.Text.Length - 4); //drop extension
                    }

                    for (int f = 0; f < info.Authors.Count; f++) {
                        tapeText += String.Format("{0}, ", info.Authors[f]);
                    }

                    tapeText += String.Format("\r\nYear\t: {0}\r\nLanguage: {1}\r\nType\t: {2}\r\nPrice\t: {3}\r\nProtection: {4}\r\nOrigin\t: {5}\r\nComments: ",
                                                    info.YearOfPublication, info.Language, info.Type, info.Price, info.ProtectionScheme, info.Origin);
                    for (int f = 0; f < info.Comments.Count; f++) {
                        tapeText += String.Format("{0}\r\n\t", info.Comments[f]);
                    }
                    tapeInfo.SetText(tapeText);
              
                    //tapeInfo.Invalidate();
                }
            }
        }

        public void InsertTape(string filename, System.IO.Stream fs) {
            ejectButton_Click(this, null);
            ziggyWin.zx.tapeIsPlaying = false;
            PZXLoader.LoadPZX(fs);
            ReadTape(filename);
            tapeIsInserted = true;
            ziggyWin.zx.tape_readToPlay = true;
            ziggyWin.zx.tapeFilename = filename;
            ziggyWin.zx.blockCounter = 0;
        }

        public void InsertTape(string filename) {
            ejectButton_Click(this, null);
            ziggyWin.zx.tapeIsPlaying = false;
            PZXLoader.LoadPZX(filename);
            ReadTape(filename);
            tapeIsInserted = true;
            ziggyWin.zx.tape_readToPlay = true;
            ziggyWin.zx.tapeFilename = filename;
            ziggyWin.zx.blockCounter = 0;
        }

        private void loadButton_Click(object sender, EventArgs e) {
            ziggyWin.openFileMenuItem1_Click(this, e);
            return;
        }

        private void playButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;

            isPlaying = true;
            ziggyWin.showTapeIndicator = true;
            ziggyWin.zx.tapeIsPlaying = true;
            ziggyWin.zx.tapeTStates = 0;
            ziggyWin.zx.tape_readToPlay = true;
            if (ziggyWin.tapeFastLoad) {
                ziggyWin.zx.SetEmulationSpeed(0);
                ziggyWin.zx.MuteSound(true);
                ziggyWin.zx.ResetKeyboard();
            } else {
                ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
                if (!ziggyWin.config.EnableSound)
                    ziggyWin.zx.MuteSound(true);
            }
            ziggyWin.zx.NextPZXBlock();
            return;
        }

        private void TapeDeck_FormClosing(object sender, FormClosingEventArgs e) {
            e.Cancel = true;
            this.Hide();
        }

        private void ejectButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;
            dataGridView1.DataSource = null;
            PZXLoader.blocks.Clear();
            PZXLoader.tapeBlockInfo.Clear();
            ziggyWin.zx.blockCounter = 0;
            statusStrip1.Items[0].Text = "No tape inserted";
            tapeInfo.SetText("");
            ziggyWin.zx.TapeStopped(true);
            tapeIsInserted = false;
            tapFileOpen = false;
            ziggyWin.zx.tape_readToPlay = false;
            ziggyWin.zx.tapeFilename = "";
        }

        private void stopButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;
            if (ziggyWin.zx.blockCounter > 0)
                ziggyWin.zx.blockCounter = dataGridView1.Rows[ziggyWin.zx.blockCounter - 1].Index;
            ziggyWin.zx.TapeStopped(true);
            ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
            ziggyWin.showTapeIndicator = false;
            if (ziggyWin.tapeFastLoad && ziggyWin.config.EnableSound)
                ziggyWin.zx.MuteSound(false);
            ziggyWin.zx.tape_readToPlay = false;
            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
            progressStep = 0;
            progressBar1.Refresh();
            statusStrip1.Items[0].Text = "Tape stopped.";
        }

        private void prevButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;

            if (ziggyWin.zx.blockCounter > 0)
                ziggyWin.zx.blockCounter--;

            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
            progressStep = 0;
            dataGridView1.Rows[ziggyWin.zx.blockCounter].Selected = true;
            dataGridView1.CurrentCell = dataGridView1.Rows[ziggyWin.zx.blockCounter].Cells[0];
        }

        private void nextButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;

            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
            progressStep = 0;

            if (ziggyWin.zx.blockCounter < PZXLoader.tapeBlockInfo.Count - 1) {
                ziggyWin.zx.blockCounter++;
                dataGridView1.Rows[ziggyWin.zx.blockCounter].Selected = true;
                dataGridView1.CurrentCell = dataGridView1.Rows[ziggyWin.zx.blockCounter].Cells[0];
            }
        }

        private void rewindButton_Click(object sender, EventArgs e) {
            if (!tapeIsInserted)
                return;
            ziggyWin.zx.TapeStopped(true);
            ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
            ziggyWin.showTapeIndicator = false;
            if (ziggyWin.tapeFastLoad && ziggyWin.config.EnableSound)
                ziggyWin.zx.MuteSound(false);
            ziggyWin.zx.blockCounter = 0;
            progressBar1.Value = 0;
            progressBar1.Maximum = 100;
            progressStep = 10;
            dataGridView1.Rows[ziggyWin.zx.blockCounter].Selected = true;
            dataGridView1.CurrentCell = dataGridView1.Rows[ziggyWin.zx.blockCounter].Cells[0];
        }

        private void edgeLoadCheckBox_CheckedChanged(object sender, EventArgs e) {
            if (edgeLoadToolStripMenuItem.Checked) {
                ziggyWin.zx.edgeLoadTapes = true;
            } else {
                ziggyWin.zx.edgeLoadTapes = false;
                autoPlayStopToolStripMenuItem.Checked = false;
            }
        }

        private void autoStartCheckBox_CheckedChanged(object sender, EventArgs e) {
            if (autoPlayStopToolStripMenuItem.Checked) {
                ziggyWin.zx.tape_AutoPlay = true;
            } else {
                ziggyWin.zx.tape_AutoPlay = false;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e) {
            if (fastLoadToolStripMenuItem.Checked) {
                ziggyWin.tapeFastLoad = true;
                if (isPlaying) {
                    ziggyWin.zx.SetEmulationSpeed(0);
                    ziggyWin.zx.MuteSound(true);
                }
            } else {
                if (isPlaying) {
                    ziggyWin.zx.SetEmulationSpeed(ziggyWin.config.EmulationSpeed);
                    ziggyWin.zx.MuteSound(ziggyWin.config.EnableSound);
                }
                ziggyWin.tapeFastLoad = false;
            }
        }

        public bool SavePZX(string filename) {
            bool success = false;
            using (System.IO.FileStream file = new System.IO.FileStream(filename, System.IO.FileMode.Create)) {
                using (System.IO.BinaryWriter r = new System.IO.BinaryWriter(file)) {
                    try {
                        foreach (PZXLoader.Block block in PZXLoader.blocks) {
                            byte[] data = RawSerialize(block);
                            r.Write(data);
                        }
                        success = true;
                    } catch (System.IO.IOException) {
                        System.Windows.Forms.MessageBox.Show("Error while saving", "Error", System.Windows.Forms.MessageBoxButtons.OK);
                    }
                }
            }
            return success;
        }

        private static byte[] RawSerialize(object anything) {
            int rawsize = Marshal.SizeOf(anything);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.StructureToPtr(anything, buffer, false);
            byte[] rawdatas = new byte[rawsize];
            Marshal.Copy(buffer, rawdatas, 0, rawsize);
            Marshal.FreeHGlobal(buffer);
            return rawdatas;
        }

        private void autoStartCheckBox_Click(object sender, EventArgs e) {
            if (!autoPlayStopToolStripMenuItem.Checked && !edgeLoadToolStripMenuItem.Checked) {
                if (MessageBox.Show("Auto Play/Stop relies on Edge Loading, which is currently disabled. Do you wish to turn it on?", "Enable Edge Loading?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == System.Windows.Forms.DialogResult.Yes)
                    edgeLoadToolStripMenuItem.Checked = true;
                else
                    return;
            }

            if (!autoPlayStopToolStripMenuItem.Checked)
                autoPlayStopToolStripMenuItem.Checked = true;
            else
                autoPlayStopToolStripMenuItem.Checked = false;
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e) {
            if (tapeInfo != null)
                tapeInfo.Show();
        }
    }
}