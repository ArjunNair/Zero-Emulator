using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace ZeroWin
{
    public partial class Infoviewer : Form
    {
        //Encapsulates the capabilities of the machine

        private string wos_select = "http://www.worldofspectrum.org/api/infoseek_select_xml.cgi?";
        private XmlTextReader xmlReader;
        private XmlDocument xmlDoc = new XmlDocument();
        private InfoDetails details;
        private List<String> fileList = new List<String>();
        private int fileDownloadCount = 0;
        private int filesToDownload = 0;
        private string autoLoadFilePath = null;

        private delegate void UpdateLabelInfoCallback(Control lst, String _info);

        private delegate void UpdateCheckBoxCallback(Control lst, String _info);

        public event FileDownloadHandler DownloadCompleteEvent;

        public void OnFileDownloadEvent(Object sender, AutoLoadArgs arg) {
            if (DownloadCompleteEvent != null)
                DownloadCompleteEvent(this, arg);
        }

        private String lastURL = "";

        public class RequestState2
        {
            public int index;
            public String webURL;

            public WebRequest Request;

            // Create Decoder for appropriate enconding type.

            public RequestState2() {
                index = -1;
                webURL = "Dummy URL";
                Request = null;
            }
        }

        private class InfoDetails
        {
            public InfoDetails() {
            }

            public String InfoseekID { get; set; }

            public String Authors { get; set; }

            public String Availability { get; set; }

            public String Publication { get; set; }

            public String Controls { get; set; }

            public String Protection { get; set; }

            public String MachineType { get; set; }

            public String ProgramName { get; set; }

            public String Year { get; set; }

            public String Publisher { get; set; }

            public String ProgramType { get; set; }

            public String Language { get; set; }

            public String Score { get; set; }

            public String PicLoadURL { get; set; }

            public String PicInlayURL { get; set; }

            public String PicIngameURL { get; set; }
        };

        private void UpdateCheckbox(Control lst, String _info) {
            if (lst.InvokeRequired) {
                UpdateCheckBoxCallback d = new UpdateCheckBoxCallback(UpdateCheckbox);
                lst.Invoke(d, new object[] { lst, _info });
            } else {
                CheckedListBox cb = (CheckedListBox)lst;
                cb.Items.Add(_info);
            }
        }

        private void UpdateLabelInfo(Control lst, String _info) {
            if (lst.InvokeRequired) {
                UpdateLabelInfoCallback d = new UpdateLabelInfoCallback(UpdateLabelInfo);
                lst.Invoke(d, new object[] { lst, _info });
            } else {
                if (lst is ScrollableLabel) {
                    ScrollableLabel lbl = (ScrollableLabel)lst;
                    lbl.scrollLabel.Text = _info;
                } else if (lst is Label) {
                    Label lbl = (Label)lst;
                    lbl.Text = _info;
                }
            }
        }

        public Infoviewer() {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            this.loadingScreen.Image = ZeroWin.Properties.Resources.NoImage;
            this.ingameScreen.Image = ZeroWin.Properties.Resources.NoImage;
            fileDownloadCount = 0;
            folderBrowserDialog1.ShowNewFolderButton = true;
            folderBrowserDialog1.SelectedPath = Application.StartupPath;
            autoLoadComboBox.Items.Clear();
            autoLoadComboBox.Items.Add("None");
            autoLoadComboBox.SelectedIndex = 0;
            autoLoadComboBox.Enabled = false;
            autoLoadFilePath = null;
        }

        public void ResetDetails() {
            details = null;
            this.loadingScreen.Image = ZeroWin.Properties.Resources.NoImage;
            this.ingameScreen.Image = ZeroWin.Properties.Resources.NoImage;
            pictureBox1.Image = ZeroWin.Properties.Resources.NoImage;
            fileList.Clear();
            checkedListBox1.Items.Clear();
            filesToDownload = 0;
            fileDownloadCount = 0;
            button1.Enabled = false;
            // this.BackgroundImage = null;
        }

        public void ShowDetails(String infoId, String _imageLoc) {
            toolStripStatusLabel1.Text = "Querying infoseek...";
            details = new InfoDetails();
            string param = "id=" + infoId;
            pictureBox1.ImageLocation = _imageLoc;
            if (checkedListBox1.SelectedItems.Count == 0)
                button1.Enabled = false;
            else
                button1.Enabled = true;
            try {
                lastURL = wos_select + param;
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(lastURL);
                webRequest.Method = "GET";

                // RequestState is a custom class to pass info to the callback
                RequestState2 rs = new RequestState2();
                rs.Request = webRequest;
                rs.webURL = lastURL;

                IAsyncResult result = webRequest.BeginGetResponse(new AsyncCallback(SearchCallback), rs);

                ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
                                        new WaitOrTimerCallback(InfoseekTimeout),
                                        rs,
                                        (30 * 1000), // 30 second timeout
                                        true
                                    );
                this.Show();
            } catch (WebException we) {
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK);
            }
        }

        private void InfoseekTimeout(object state, bool timedOut) {
            if (timedOut) {
                RequestState2 reqState = (RequestState2)state;
                if (reqState != null)
                    reqState.Request.Abort();
                MessageBox.Show("Request timed out.", "Connection Error", MessageBoxButtons.OK);
            }
        }

        private void SearchCallback(IAsyncResult result) {
            try {
                RequestState2 rs = (RequestState2)result.AsyncState;

                //Don't do anything if this isn't the last web request we made
                if (rs.webURL != lastURL) {
                    rs.Request.EndGetResponse(result);
                    //rs.Request.Abort();
                    return;
                }

                // Get the WebRequest from RequestState.
                WebRequest req = rs.Request;

                // Call EndGetResponse, which produces the WebResponse object
                //  that came from the request issued above.
                WebResponse resp = req.EndGetResponse(result);

                //  Start reading data from the response stream.
                Stream responseStream = resp.GetResponseStream();

                // Store the response stream in RequestState to read
                // the stream asynchronously.
                xmlReader = new XmlTextReader(responseStream);
                xmlDoc = new XmlDocument();
                xmlDoc.Load(xmlReader);
                xmlReader.Close();
                resp.Close();
            } catch (WebException we) {
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            XmlNodeList memberNodes = xmlDoc.SelectNodes("//result");
            foreach (XmlNode node in memberNodes) {
                details.ProgramName = GetNodeElement(node, "title");
                details.Year = GetNodeElement(node, "year");
                details.Publisher = GetNodeElement(node, "publisher");
                details.ProgramType = GetNodeElement(node, "type");
                details.Language = GetNodeElement(node, "language");
                details.Score = GetNodeElement(node, "score");
                details.Authors = GetNodeElement(node, "author");
                details.Controls = GetNodeElement(node, "joysticks");
                details.Protection = GetNodeElement(node, "protectionScheme");
                details.Availability = GetNodeElement(node, "availability");
                details.Publication = GetNodeElement(node, "publication");
                details.MachineType = GetNodeElement(node, "machineType");
                details.PicInlayURL = GetNodeElement(node, "picInlay");
                details.PicLoadURL = GetNodeElement(node, "picLoad");
                details.PicIngameURL = GetNodeElement(node, "picIngame");
            }

            if (string.IsNullOrEmpty(details.Protection))
                details.Protection = "Various";
            UpdateLabelInfo(authorLabel, details.Authors);
            UpdateLabelInfo(publicationLabel, details.Publication);
            UpdateLabelInfo(availabilityLabel, details.Availability);
            UpdateLabelInfo(protectionLabel, details.Protection);
            string controls = "";
            foreach (char c in details.Controls.ToCharArray()) {
                if (c == 'K')
                    controls += "Keyboard, ";
                else if (c == '1')
                    controls += "IF 2 Left, ";
                else if (c == '2')
                    controls += "IF 2 Right, ";
                else if (c == 'C')
                    controls += "Cursor, ";
                else if (c == 'R')
                    controls += "Redefinable, ";
            }

            UpdateLabelInfo(controlsLabel, controls);
            UpdateLabelInfo(machineLabel, details.MachineType);

            XmlNodeList fileNodes = xmlDoc.SelectNodes("/result/downloads/file");

            foreach (XmlNode node in fileNodes) {
                String full_link = node.SelectSingleNode("link").InnerText;
                char delimiter = '/';
                string[] splitWords = full_link.Split(delimiter);
                String type = node.SelectSingleNode("type").InnerText;
                fileList.Add(full_link);
                UpdateCheckbox(checkedListBox1, " (" + type + ") " + splitWords[splitWords.Length - 1]);
            }

            XmlNodeList fileNodes2 = xmlDoc.SelectNodes("/result/otherDownloads/file");
            foreach (XmlNode node in fileNodes2) {
                String full_link = node.SelectSingleNode("link").InnerText;
                char delimiter = '/';
                string[] splitWords = full_link.Split(delimiter);
                String type = node.SelectSingleNode("type").InnerText;
                fileList.Add(full_link);
                UpdateCheckbox(checkedListBox1, " (" + type + ") " + splitWords[splitWords.Length - 1]);
            }

            toolStripStatusLabel1.Text = "Ready.";

            if (details.PicInlayURL != "") {
                try {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(details.PicInlayURL);
                    webRequest.Method = WebRequestMethods.Http.Get;
                    webRequest.UserAgent = "Zero Emulator";
                    // RequestState is a custom class to pass info to the callback
                    RequestState2 rs2 = new RequestState2();
                    rs2.Request = webRequest;

                    IAsyncResult result2 = webRequest.BeginGetResponse(new AsyncCallback(PictureLoadCallback), rs2);

                    ThreadPool.RegisterWaitForSingleObject(result2.AsyncWaitHandle,
                                            new WaitOrTimerCallback(InfoseekTimeout),
                                            rs2,
                                            (30 * 1000), // 30 second timeout
                                            true
                                        );
                } catch (WebException we) {
                    MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK);
                }
            }

            if (details.PicLoadURL != "") {
                try {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(details.PicLoadURL);
                    webRequest.Method = WebRequestMethods.Http.Get;
                    webRequest.UserAgent = "Zero Emulator";
                    // RequestState is a custom class to pass info to the callback
                    RequestState2 rs2 = new RequestState2();
                    rs2.Request = webRequest;

                    IAsyncResult result2 = webRequest.BeginGetResponse(new AsyncCallback(PictureLoadCallback), rs2);

                    ThreadPool.RegisterWaitForSingleObject(result2.AsyncWaitHandle,
                                            new WaitOrTimerCallback(InfoseekTimeout),
                                            rs2,
                                            (30 * 1000), // 30 second timeout
                                            true
                                        );
                } catch (WebException we) {
                    MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK);
                }
            }

            if (details.PicIngameURL != "") {
                try {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(details.PicIngameURL);
                    webRequest.Method = WebRequestMethods.Http.Get;
                    webRequest.UserAgent = "Zero Emulator";
                    // RequestState is a custom class to pass info to the callback
                    RequestState2 rs2 = new RequestState2();
                    rs2.Request = webRequest;

                    IAsyncResult result2 = webRequest.BeginGetResponse(new AsyncCallback(PictureLoadCallback), rs2);

                    ThreadPool.RegisterWaitForSingleObject(result2.AsyncWaitHandle,
                                            new WaitOrTimerCallback(InfoseekTimeout),
                                            rs2,
                                            (30 * 1000), // 30 second timeout
                                            true
                                        );
                } catch (WebException we) {
                    MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void PictureLoadCallbackTimeout(object state, bool timedOut) {
            if (timedOut) {
                RequestState2 reqState = (RequestState2)state;

                if (reqState != null)
                    reqState.Request.Abort();
                MessageBox.Show("Request timed out.", reqState.webURL, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FileDownloadCallback(IAsyncResult result) {
            try {
                RequestState2 rs = (RequestState2)result.AsyncState;

                // Get the WebRequest from RequestState.
                WebRequest req = rs.Request;

                // Call EndGetResponse, which produces the WebResponse object
                //  that came from the request issued above.
                WebResponse resp = req.EndGetResponse(result);

                Stream ftpStream = resp.GetResponseStream();
                char delimiter = '/';
                string[] splitWords = fileList[rs.index].Split(delimiter);

                FileStream outputStream = new FileStream(folderBrowserDialog1.SelectedPath + "//" + splitWords[splitWords.Length - 1], FileMode.Create);
                long cl = resp.ContentLength;
                int bufferSize = 2048;
                int readCount;
                byte[] buffer = new byte[bufferSize];

                readCount = ftpStream.Read(buffer, 0, bufferSize);
                while (readCount > 0) {
                    outputStream.Write(buffer, 0, readCount);
                    readCount = ftpStream.Read(buffer, 0, bufferSize);
                }

                ftpStream.Close();
                outputStream.Close();

                fileDownloadCount++;
                if (fileDownloadCount >= filesToDownload) {
                    fileDownloadCount = 0;
                    toolStripStatusLabel1.Text = "Downloading complete.";
                    AutoLoadArgs arg = new AutoLoadArgs(autoLoadFilePath);
                    button1.Enabled = true;
                    OnFileDownloadEvent(this, arg);
                } else
                    toolStripStatusLabel1.Text = "Downloaded " + fileDownloadCount.ToString() + " of " + filesToDownload.ToString() + " files...";
                resp.Close();
            } catch (WebException we) {
                MessageBox.Show(we.Message, "File Download Error", MessageBoxButtons.OK);
            }
        }

        private void PictureLoadCallback(IAsyncResult result) {
            try {
                RequestState2 rs = (RequestState2)result.AsyncState;

                // Get the WebRequest from RequestState.
                WebRequest req = rs.Request;

                // Call EndGetResponse, which produces the WebResponse object
                //  that came from the request issued above.
                WebResponse resp = req.EndGetResponse(result);

                if (req.RequestUri.AbsoluteUri == details.PicInlayURL) {
                    pictureBox1.Image = Bitmap.FromStream(resp.GetResponseStream());
                } else
                    if (req.RequestUri.AbsoluteUri == details.PicLoadURL) {
                        loadingScreen.Image = Bitmap.FromStream(resp.GetResponseStream());
                    } else if (req.RequestUri.AbsoluteUri == details.PicIngameURL)
                        ingameScreen.Image = Bitmap.FromStream(resp.GetResponseStream());
                resp.Close();
            } catch (WebException we) {
                MessageBox.Show(we.Message, "Picture Download Error", MessageBoxButtons.OK);
            }
        }

        private String GetNodeElement(XmlNode node, String value) {
            if (node.SelectSingleNode(value) != null)
                return node.SelectSingleNode(value).InnerText;

            return "";
        }

        private String GetNodeValue(XmlNodeList nodes, String value) {
            String returnString = "";
            foreach (XmlNode node in nodes) {
                if (node.SelectSingleNode("type").InnerText == value) {
                    returnString = node.SelectSingleNode("link").InnerText;
                    break;
                }
            }
            return returnString;
        }

        private void button1_Click(object sender, EventArgs e) {
            CheckedListBox.CheckedIndexCollection selectedItems = checkedListBox1.CheckedIndices;
            if (selectedItems.Count > 0) {
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK) {
                    button1.Enabled = false;
                    filesToDownload = 0;
                    fileDownloadCount = 0;
                    for (int f = 0; f < selectedItems.Count; f++) {
                        char delimiter = '/';
                        string[] splitWords = fileList[selectedItems[f]].Split(delimiter);
                        string localFilePath = folderBrowserDialog1.SelectedPath + "//" + splitWords[splitWords.Length - 1];
                        if (File.Exists(localFilePath)) {
                            if (MessageBox.Show(checkedListBox1.Items[selectedItems[f]] + " already exists at this location.\nOverwrite existing file?", "Confirm File Replace", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                                continue;
                        }
                        //spawn requests for file download
                        filesToDownload++;
                        if (checkedListBox1.Items[selectedItems[f]] == autoLoadComboBox.Items[autoLoadComboBox.SelectedIndex])
                            autoLoadFilePath = localFilePath;

                        try {
                            FtpWebRequest webRequest = (FtpWebRequest)WebRequest.Create(fileList[selectedItems[f]]);
                            webRequest.Method = WebRequestMethods.Ftp.DownloadFile;

                            // RequestState is a custom class to pass info to the callback
                            RequestState2 rs2 = new RequestState2();
                            rs2.index = selectedItems[f];
                            rs2.Request = webRequest;

                            IAsyncResult result2 = webRequest.BeginGetResponse(new AsyncCallback(FileDownloadCallback), rs2);

                            ThreadPool.RegisterWaitForSingleObject(result2.AsyncWaitHandle,
                                                    new WaitOrTimerCallback(InfoseekTimeout),
                                                    rs2,
                                                    (30 * 1000), // 30 second timeout
                                                    true
                                                );
                        } catch (WebException we) {
                            MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK);
                            button1.Enabled = true;
                        }
                    }
                    toolStripStatusLabel1.Text = "Downloading " + filesToDownload.ToString() + " files...";
                }
            }
        }

        private void Infoviewer_FormClosing(object sender, FormClosingEventArgs e) {
            e.Cancel = true;
            this.Hide();
        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e) {
            string file = checkedListBox1.Items[e.Index].ToString();
            string ext = file.Substring(file.Length - 7, 3);
            if (e.NewValue == CheckState.Checked) {
                button1.Enabled = true;
                autoLoadComboBox.Enabled = true;

                if (ext == "szx" || ext == "sna" || ext == "z80" || ext == "tap" || ext == "tzx" ||
                    ext == "pzx" || ext == "dsk" || ext == "trd" || ext == "scl") {
                    autoLoadComboBox.Items.Add(file);
                    //autoLoadComboBox.SelectedIndex = autoLoadComboBox.Items.Count - 1;
                }
            } else {
                if (ext == "szx" || ext == "sna" || ext == "z80" || ext == "tap" || ext == "tzx" ||
                    ext == "pzx" || ext == "dsk" || ext == "trd" || ext == "scl") {
                    if (/*autoLoadComboBox.SelectedIndex >= 0 && */(checkedListBox1.Items[e.Index] == autoLoadComboBox.Items[autoLoadComboBox.SelectedIndex]))
                        autoLoadComboBox.SelectedIndex = 0;
                    autoLoadComboBox.Items.Remove(checkedListBox1.Items[e.Index]);
                }

                if (checkedListBox1.CheckedIndices.Count == 1) {
                    button1.Enabled = false;
                    autoLoadComboBox.Enabled = false;
                }
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e) {
            if (pictureBox1.Image != ZeroWin.Properties.Resources.NoImage) {
                PicturePreview picPreview = new PicturePreview(pictureBox1.Image);
                picPreview.Show();
            }
        }
    }
}