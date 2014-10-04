using System;
using System.Net;
using System.Windows.Forms;

namespace ZeroWin
{

    //Since the search button cannot be updated from Async web callback as it's on another thread.
    public delegate void UpdateSearchButtonHandler(BoolArgs arg);

    public delegate void FileDownloadHandler(object sender, AutoLoadArgs arg);

    public partial class Infoseeker : Form
    {
        //A neat trick to get around the "call from another thread error" from
        //Praveen Nair's Blog: http://blog.ninethsense.com/
        private delegate void AddItemToListBoxCallback(Control lst, int index, string name,
                                        string inlayURL, string pub, string type,
                                        string year, string language, string score);

        private CustomListbox infoListBox = new CustomListbox();
        private Infoviewer infoView = new Infoviewer();

        public event FileDownloadHandler DownloadCompleteEvent;

        public void OnFileDownloadEvent(Object sender, AutoLoadArgs arg) {
            if (DownloadCompleteEvent != null)
                DownloadCompleteEvent(this, arg);
            //infoView.BeginInvoke(new MethodInvoker(Close));
        }

        public void UpdateSearchButtonEvent(BoolArgs arg) {
            searchButton.Enabled = arg.IsTrue;
        }

        // The RequestState class passes data across async calls.
        public class RequestState
        {
            private const int BufferSize = 1024;
            public System.Text.StringBuilder RequestData;
            public byte[] BufferRead;
            public WebRequest Request;
            public System.IO.Stream ResponseStream;

            // Create Decoder for appropriate enconding type.
            public System.Text.Decoder StreamDecode = System.Text.Encoding.UTF8.GetDecoder();

            public RequestState() {
                BufferRead = new byte[BufferSize];
                RequestData = new System.Text.StringBuilder(String.Empty);
                Request = null;
                ResponseStream = null;
            }
        }

        private class InfoseekResult
        {
            public InfoseekResult() {
                Inlay = null;
            }

            public String InfoseekID { get; set; } //new c# 3.0 way! Yay!

            public String ProgramName { get; set; }

            public String Year { get; set; }

            public String Publisher { get; set; }

            public String ProgramType { get; set; }

            public String Language { get; set; }

            public String Score { get; set; }

            public String PicInlayURL { get; set; }

            public System.Drawing.Bitmap Inlay { get; set; }
        };

        private class ProgramTitle
        {
            public String Title { get; set; }
        }

        private System.ComponentModel.BindingList<InfoseekResult> infoList = new System.ComponentModel.BindingList<InfoseekResult>();
        private System.ComponentModel.BindingList<ProgramTitle> ProgramTitleList = new System.ComponentModel.BindingList<ProgramTitle>();

        private string wos_search = "http://www.worldofspectrum.org/api/infoseek_search_xml.cgi?";
        private string wos_param = "";

        private System.Xml.XmlTextReader xmlReader;
        private System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

        private int currentResultPage = 1;
        private int totalResultPages = 1;

        public Infoseeker() {
            InitializeComponent();
            // Set the default dialog font on each child control
            foreach (Control c in Controls) {
                c.Font = new System.Drawing.Font(System.Drawing.SystemFonts.MessageBoxFont.FontFamily, c.Font.Size);
            }
            infoView.DownloadCompleteEvent += new FileDownloadHandler(OnFileDownloadEvent);
            infoListBox.Location = new System.Drawing.Point(groupBox1.Location.X, groupBox1.Location.Y + groupBox1.Height + 5);
            this.Controls.Add(infoListBox);
            infoListBox.Width = 380;
            infoListBox.Height = 410;
            infoListBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Right | AnchorStyles.Left | AnchorStyles.Top;
            infoListBox.Visible = false;
            detailsButton.Hide();
            moreButton.Hide();
        }

        private void searchButton_Click(object sender, EventArgs e) {
            searchButton.Enabled = false;
            infoList.Clear();
            foreach (CustomListItem ci in infoListBox.Items) {
                ci.RemoveEventHandlers();
            }
            infoListBox.Items.Clear();

            totalResultPages = 1;
            currentResultPage = 1;

            if (titleRadioButton.Checked)
                wos_param = "title=" + titleBox.Text + "&perpage=20";
            else
                wos_param = "pub=" + titleBox.Text + "&perpage=20";

            toolStripStatusLabel1.Text = "Querying Infoseek...";
            toolStripStatusLabel2.Text = "";
            detailsButton.Hide();
            statusStrip1.Refresh();

            try {
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(wos_search + wos_param);
                webRequest.Method = "GET";
                object data = new object();
                // RequestState is a custom class to pass info to the callback
                RequestState rs = new RequestState();
                rs.Request = webRequest;

                IAsyncResult result = webRequest.BeginGetResponse(new AsyncCallback(MySearchCallback), rs);

                System.Threading.ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
                                        new System.Threading.WaitOrTimerCallback(MySearchCallbackTimeout),
                                        rs,
                                        (30 * 1000), // 30 second timeout
                                        true
                                    );
            } catch (WebException we) {
                toolStripStatusLabel1.Text = "Search failed.";
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                searchButton.Enabled = true;
            }
        }

        private void UpdateSearchButton(object sender, BoolArgs e) {
            // Update the user interface.
            searchButton.Enabled = e.IsTrue;
        }

        private void MySearchCallbackTimeout(object state, bool timedOut) {
            // Create an EventHandler delegate.
            UpdateSearchButtonHandler updateSearch = new UpdateSearchButtonHandler(UpdateSearchButtonEvent);

            // Invoke the delegate on the UI thread.
            this.Invoke(updateSearch, new object[] { new BoolArgs(true) });

            if (timedOut) {
                RequestState reqState = (RequestState)state;
                if (reqState != null) {
                    reqState.Request.Abort();
                    reqState.Request = null;
                }
                MessageBox.Show("Request timed out.", "Connection Error", MessageBoxButtons.OK);
                toolStripStatusLabel1.Text = "Search failed.";
                searchButton.Enabled = true;
                if (currentResultPage < totalResultPages)
                    moreButton.Show();
            }
        }

        private void MySearchCallback(IAsyncResult result) {
            // Create an EventHandler delegate.
            UpdateSearchButtonHandler updateSearch = new UpdateSearchButtonHandler(UpdateSearchButtonEvent);

            // Invoke the delegate on the UI thread.
            this.Invoke(updateSearch, new object[] { new BoolArgs(true) });
            RequestState rs = (RequestState)result.AsyncState;
            try {
                // Get the WebRequest from RequestState.
                WebRequest req = rs.Request;

                // Call EndGetResponse, which produces the WebResponse object
                //  that came from the request issued above.
                WebResponse resp = req.EndGetResponse(result);

                //  Start reading data from the response stream.
                System.IO.Stream responseStream = resp.GetResponseStream();

                // Store the response stream in RequestState to read
                // the stream asynchronously.
                rs.ResponseStream = responseStream;
                xmlReader = new System.Xml.XmlTextReader(responseStream);
                xmlDoc = new System.Xml.XmlDocument();
                xmlDoc.Load(xmlReader);
                xmlReader.Close();
                resp.Close();
                req = null;
            } catch (WebException we) {
                rs.Request.Abort();
                rs.Request = null;
                toolStripStatusLabel1.Text = "Search failed.";
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (currentResultPage < totalResultPages)
                    moreButton.Show();

                return;
            }

            int nResults = Convert.ToInt32(xmlDoc.GetElementsByTagName("numResults")[0].InnerText);
            if (nResults > 0) {
                totalResultPages = nResults / 20 + 1;
                toolStripStatusLabel1.Text = "Items found: " + nResults;
                toolStripStatusLabel2.Text = "Showing page " + currentResultPage.ToString() + " of " + totalResultPages.ToString();
            } else {
                MessageBox.Show("Your query didn't return any results from Infoseek.\nTry some other search term or regular Infoseek\nwildcards like '*', '?', '^'", "No match", MessageBoxButtons.OK, MessageBoxIcon.Information);
                toolStripStatusLabel1.Text = "Ready";
                toolStripStatusLabel2.Text = "";
                return;
            }
            System.Xml.XmlNodeList memberNodes = xmlDoc.SelectNodes("//result");
            foreach (System.Xml.XmlNode node in memberNodes) {
                InfoseekResult ir = new InfoseekResult();
                ProgramTitle progTitle = new ProgramTitle();
                ir.InfoseekID = node.SelectSingleNode("id").InnerText;
                ir.ProgramName = node.SelectSingleNode("title").InnerText;
                ir.Year = node.SelectSingleNode("year").InnerText;
                ir.Publisher = node.SelectSingleNode("publisher").InnerText;
                ir.ProgramType = node.SelectSingleNode("type").InnerText;
                ir.Language = node.SelectSingleNode("language").InnerText;
                ir.Score = node.SelectSingleNode("score").InnerText;
                ir.PicInlayURL = node.SelectSingleNode("picInlay").InnerText;
                infoList.Add(ir);
                progTitle.Title = ir.ProgramName;
                // ProgramTitleList.Add(progTitle);
                AddItemToListBox(infoListBox, infoList.Count - 1, ir.ProgramName,
                                ir.PicInlayURL, ir.Publisher, ir.ProgramType,
                                ir.Year, ir.Language, ir.Score);
            }
        }

        private void AddItemToListBox(Control lst, int index, string name,
                                        string inlayURL, string pub, string type,
                                        string year, string language, string score) {
            if (lst.InvokeRequired) {
                AddItemToListBoxCallback d = new AddItemToListBoxCallback(AddItemToListBox);
                lst.Invoke(d, new object[] { lst, index, name, inlayURL, pub, type, year, language, score });
            } else {
                CustomListbox cbox = (CustomListbox)lst;
                CustomListItem citem = new CustomListItem(name);
                citem.Index = index;
                citem.AddText(pub + (year != "" ? ", " + year : ""));
                citem.AddText(type);
                citem.AddText("Language: " + language);
                citem.AddText("Score: " + score);
                if (inlayURL != "")
                    citem.SetPicture(inlayURL);
                citem.SetImageChangedHandler(cbox.UpdateImageOnChange);
                cbox.Items.Add(citem);
                infoListBox.TopIndex = 20 * (infoListBox.Items.Count / 20);
                infoListBox.SelectedIndex = infoListBox.TopIndex;
                if (!infoListBox.Visible) {
                    infoListBox.Visible = true;
                }
                if (!detailsButton.Visible)
                    detailsButton.Show();

                if (!moreButton.Visible && (currentResultPage < totalResultPages))
                    moreButton.Show();
            }
        }

        private void titleBox_TextChanged(object sender, EventArgs e) {
            if (titleBox.TextLength == 0)
                searchButton.Enabled = false;
            else
                searchButton.Enabled = true;
        }

        private void infoListBox_SelectedIndexChanged(object sender, EventArgs e) {
        }

        private void moreButton_Click_1(object sender, EventArgs e) {
            toolStripStatusLabel1.Text = "Querying Infoseek...";
            statusStrip1.Refresh();

            try {
                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(wos_search + wos_param + "&page=" + (currentResultPage + 1).ToString());
                webRequest.Method = "GET";
                object data = new object();
                // RequestState is a custom class to pass info to the callback
                RequestState rs = new RequestState();
                rs.Request = webRequest;

                IAsyncResult result = webRequest.BeginGetResponse(new AsyncCallback(MySearchCallback), rs);

                System.Threading.ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle,
                                        new System.Threading.WaitOrTimerCallback(MySearchCallbackTimeout),
                                        rs,
                                        (30 * 1000), // 30 second timeout
                                        true
                                    );

                currentResultPage++;
                toolStripStatusLabel2.Text = "Showing page " + currentResultPage.ToString() + " of " + totalResultPages.ToString();
                //if (currentResultPage >= totalResultPages)
                moreButton.Hide();
            } catch (WebException we) {
                toolStripStatusLabel1.Text = "Search failed.";
                MessageBox.Show(we.Message, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                toolStripStatusLabel1.Text = "Ready.";
                moreButton.Show();
            }
        }

        private void detailsButton_Click(object sender, EventArgs e) {
            infoView.ResetDetails();
            infoView.ShowDetails(infoList[infoListBox.SelectedIndex].InfoseekID, infoList[infoListBox.SelectedIndex].PicInlayURL);
            //infoView.Owner = this;
        }

        private void Infoseeker_FormClosed(object sender, FormClosedEventArgs e) {
            infoView.Hide();
        }
    }

    public class AutoLoadArgs : EventArgs
    {
        public string filePath;

        public AutoLoadArgs(string path) {
            filePath = path;
        }
    }

    public class BoolArgs : EventArgs
    {
        public bool IsTrue { get; set; }

        public BoolArgs(bool setTrue) {
            IsTrue = setTrue;
        }
    }
}