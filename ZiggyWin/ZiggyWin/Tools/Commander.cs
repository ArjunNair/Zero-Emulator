using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ScintillaNET;

namespace ZeroWin.Tools
{
    public partial class Commander : Form
    {
        private Form1 zw;
        private List<Command> commandHandlers = new List<Command>();
        private List<int> outputLines = new List<int>();
        private int prevOutputLine = -1;
        private bool isCommand = false;
        public Commander(Form1 _zw)
        {
            zw = _zw;
            InitializeComponent();
            scintillaOut.ClearAll();
            scintillaIn.ClearAll();
            UpdateOutput("Zero Commander");
            UpdateOutput("-----------------");
            scintillaIn.Select();
            commandHandlers.Add(new TapeCommands(zw.tapeDeck));
            commandHandlers.Add(new MemoryCommands(zw.zx));
        }
        
        private int GetCurrentLine()
        {
            var currentPos = scintillaOut.CurrentPosition;
            var currentLine = scintillaOut.LineFromPosition(currentPos);
            return currentLine;
        }

        private int GetCurrentCol()
        {
            var currentPos = scintillaOut.CurrentPosition;
            var currentCol = scintillaOut.GetColumn(currentPos);
            return currentCol;
        }

        private void UpdateOutput(string text)
        {
            //scintillaOut.ReadOnly = false;
            //prevOutputLine = GetCurrentLine();
            scintillaOut.AppendText(text + "\r\n");
            //scintillaOut.ReadOnly = true;
        }
        private void Commander_UpdateUI(object sender, UpdateUIEventArgs e)
        {
            if((e.Change & UpdateChange.Selection) > 0)
            {
                // The caret/selection changed
                var currentLine = GetCurrentLine();
                /*
                if (outputLines.Contains(currentLine))
                    scintillaOut.ReadOnly = true;
                else
                    scintillaOut.ReadOnly = false;
                    */
                var currentCol = GetCurrentCol();
                var currentText = scintillaOut.Lines[currentLine].Text;
                toolStripStatusLabel1.Text = "Line: " + currentLine + " Col:" + currentCol + " Ch: " + scintillaOut.CurrentPosition + " Sel: " + Math.Abs(scintillaOut.AnchorPosition - scintillaOut.CurrentPosition);
                scintillaOut.ScrollCaret();
            }
        }

        private void scintillaIn_InsertCheck(object sender, InsertCheckEventArgs e)
        {      
            var currentPos = scintillaIn.CurrentPosition;
            var currentLine = scintillaIn.LineFromPosition(currentPos);
            var currentText = scintillaIn.Lines[currentLine].Text;
            if(e.Text == "\r\n")
            {
                if(currentText == "")
                    e.Text = "";
                else
                    if(currentText[currentText.Length - 1] == '\n')    //an existing command then ...
                    {
                        e.Text = "";
                        currentText = currentText.Substring(0, currentText.Length - 2);
                    }

                if(currentText != "")
                {
                    Console.WriteLine("Command: " + currentText);
                    string[] commands = currentText.ToLower().Split(' ');
                    foreach(var comm in commandHandlers)
                    {
                        string ret = comm.Execute(commands);
                        if(ret != null)
                            UpdateOutput(ret); 
                    }
                }
            }
        }

        private void scintillaOut_InsertCheck(object sender, InsertCheckEventArgs e)
        {
            var currentText = scintillaOut.Lines[GetCurrentLine()].Text;
            if(e.Text == "\r\n")
            {
                if(currentText == "")
                    e.Text = "";
            }
        }

        private void scintillaOut_Insert(object sender, ModificationEventArgs e)
        {
            if(e.Text == "\r\n")
            {
                isCommand = true;
            }
        }

        private void scintillaOut_TextChanged(object sender, EventArgs e)
        {
            if(isCommand)
            {
                isCommand = false;
                var currentText = scintillaOut.Lines[GetCurrentLine()].Text;
                Console.WriteLine("Command: " + currentText);
                string[] commands = currentText.ToLower().Split(' ');
                foreach(var comm in commandHandlers)
                {
                    string ret = comm.Execute(commands);
                    if(ret != null)
                        UpdateOutput(ret);
                }                
            }
        }
    }
}
