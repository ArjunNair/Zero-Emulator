using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Newtonsoft.Json;
namespace ZeroWin
{
    public partial class Infoviewer : Form
    {
        //Encapsulates the capabilities of the machine

        private string wos_select = "http://www.worldofspectrum.org/api/infoseek_select_xml.cgi?";
        private string url_base = "https://api.zxinfo.dk/v3/";
        private string search_query = "games/";
        private string wos_archive_url = "https://archive.org/download/World_of_Spectrum_June_2017_Mirror/World%20of%20Spectrum%20June%202017%20Mirror.zip/World%20of%20Spectrum%20June%202017%20Mirror/sinclair/";
        private string zxdb_archive_url = "https://spectrumcomputing.co.uk/zxdb/sinclair/";
        private string zxinfo_archive_url = "https://zxinfo.dk/media/"; 
        private XmlTextReader xmlReader;
        private XmlDocument xmlDoc = new XmlDocument();
        private InfoDetails details;
        private List<String> fileList = new List<String>();
        private int fileDownloadCount = 0;
        private int filesToDownload = 0;
        private string autoLoadFilePath = null;

        private delegate void UpdateLabelInfoCallback(Control lst, String _info);
        private delegate void EnableDownloadButtonCallback(bool enable);
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

        private void EnableDownloadButton(bool enable)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.button1.InvokeRequired)
            {
                EnableDownloadButtonCallback d = new EnableDownloadButtonCallback(EnableDownloadButton);
                this.Invoke(d, new object[] { enable });
            }
            else
            {
                this.button1.Enabled = enable;
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
            autoLoadComboBox.Items.Clear();
            autoLoadComboBox.Items.Add("None");
            autoLoadComboBox.SelectedIndex = 0;
            autoLoadComboBox.Enabled = false;
            autoLoadFilePath = null;
            fileList.Clear();
            checkedListBox1.Items.Clear();
            filesToDownload = 0;
            fileDownloadCount = 0;
            button1.Enabled = false;
            // this.BackgroundImage = null;
        }

        public void ShowDetails(String infoId, String _imageLoc) {
            toolStripStatusLabel1.Text = "Searching ...";
            details = new InfoDetails();
            //pictureBox1.ImageLocation = _imageLoc;
            ingameScreen.ImageLocation = _imageLoc;
            if (checkedListBox1.SelectedItems.Count == 0)
                button1.Enabled = false;
            else
                button1.Enabled = true;
            try {
                lastURL = url_base + search_query + infoId + "?mode=compact";
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
        
        // Resolves the archive path from ZXDB result to either legacy WoS archive path,
        // or the newer ZXDB archive path depending on the prefixes in the path.
        // Ref "Additional Details" at: https://github.com/zxdb/ZXDB/
        private string ResolveArchivePath(string path)
        {
            string[] meta_path = path.Split('/');
            string archive_path = "";
            if (meta_path[1] == "pub")
            {
                archive_path = wos_archive_url + String.Join("/", meta_path, 3, meta_path.Length - 3);
            }
            else if (meta_path[1] == "zxdb")
            {
                archive_path = zxdb_archive_url + String.Join("/", meta_path, 3, meta_path.Length - 3);
            }
            else if (meta_path[1] == "zxscreens")
            {
                archive_path = zxinfo_archive_url + String.Join("/", meta_path, 1, meta_path.Length - 1);
            }

            return archive_path;
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
                System.IO.StreamReader reader = new System.IO.StreamReader(responseStream);
                var json_data = JsonConvert.DeserializeObject(reader.ReadToEnd());
                
                xmlDoc = JsonConvert.DeserializeXmlNode(json_data.ToString(), "hits");
                reader.Close();
                resp.Close();
            } catch (WebException we) {
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            XmlNodeList memberNodes = xmlDoc.SelectNodes("//_source");
            foreach (XmlNode node in memberNodes) {
                details.ProgramName = GetNodeElement(node, "title");
                details.Year = GetNodeElement(node, "originalYearOfRelease");
                details.Publisher = GetNodeElement(node, "publishers/name");
                details.ProgramType = GetNodeElement(node, "genreType");
                details.Language = GetNodeElement(node, "language");
                details.Score = GetNodeElement(node, "score/score");
                details.Authors = GetNodeElement(node, "authors/name");
                XmlNodeList control_list = node.SelectNodes("controls");
                foreach (XmlNode control in control_list)
                {
                    details.Controls += control.InnerText + ", ";
                }
                details.Availability = GetNodeElement(node, "availability");
                details.MachineType = GetNodeElement(node, "machineType");
                
                XmlNodeList pics_list = node.SelectNodes("additionalDownloads");
                foreach (XmlNode pic_node in pics_list)
                {
                    string pic_type = pic_node.SelectSingleNode("type").InnerText;
                    if (pic_type == "Running screen" && details.PicIngameURL == null)
                    {
                        string pic_url = pic_node.SelectSingleNode("path").InnerText;
                        details.PicIngameURL = ResolveArchivePath(pic_url);
                    }
                    else if (pic_type == "Inlay - Front" && details.PicInlayURL == null)
                    {
                        string pic_url = pic_node.SelectSingleNode("path").InnerText;
                        details.PicInlayURL = ResolveArchivePath(pic_url);
                    }
                }
                
                // Currently the loading screen URL that's present in "additionalDownloads" is
                // in SCR format, which can't be loaded by WinForms methods.
                // Instead we use the PNG/GIF url that's available in the "screens" section.
                if (details.PicLoadURL == null)
                {
                    XmlNodeList screen_list = node.SelectNodes("screens");
                    foreach (XmlNode screen in screen_list)
                    {
                        if (screen.SelectSingleNode("type").InnerText == "Loading screen")
                        {
                            string pic_url = screen.SelectSingleNode("url").InnerText;
                            details.PicLoadURL = ResolveArchivePath(pic_url);
                            break;
                        }
                    }
                } 
                XmlNodeList fileNodes2 = node.SelectNodes("releases/files");
                foreach (XmlNode node2 in fileNodes2) {
                    String full_link = node2.SelectSingleNode("path").InnerText;
                    char delimiter = '/';
                    string[] splitWords = full_link.Split(delimiter);
                    String type = node2.SelectSingleNode("format").InnerText;
                    fileList.Add(ResolveArchivePath(full_link));
                    UpdateCheckbox(checkedListBox1, " (" + type + ") " + splitWords[splitWords.Length - 1]);
                }
            }

            UpdateLabelInfo(authorLabel, details.Authors);
            UpdateLabelInfo(publicationLabel, details.Publisher);
            UpdateLabelInfo(availabilityLabel, details.Availability);
            UpdateLabelInfo(controlsLabel, details.Controls);
            UpdateLabelInfo(machineLabel, details.MachineType);
            
            toolStripStatusLabel1.Text = "Ready. Click on images to see bigger previews.";

            if (details.PicInlayURL != null) {
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

            if (details.PicLoadURL != null) {
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
            /*
            if (details.PicIngameURL != null) {
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
            }*/
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
                    toolStripStatusLabel1.Text = "Downloads complete.";
                    AutoLoadArgs arg = new AutoLoadArgs(autoLoadFilePath);
                    EnableDownloadButton(true);
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

        private void machineLabel_Click(object sender, EventArgs e)
        {
            throw new System.NotImplementedException();
        }

        private void loadingScreen_Click(object sender, EventArgs e)
        {
            if (loadingScreen.Image != ZeroWin.Properties.Resources.NoImage) {
                PicturePreview picPreview = new PicturePreview(loadingScreen.Image);
                picPreview.Show();
            }
        }

        private void ingameScreen_Click(object sender, EventArgs e)
        {
            if (ingameScreen.Image != ZeroWin.Properties.Resources.NoImage) {
                PicturePreview picPreview = new PicturePreview(ingameScreen.Image);
                picPreview.Show();
            }
        }
    }
}