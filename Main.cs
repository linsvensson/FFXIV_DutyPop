using FFXIVAPP.Memory;
using FFXIVAPP.Memory.Core;
using FFXIVAPP.Memory.Models;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FFXIV_DutyPop
{
    public partial class Main : Form
    {
        #region DllImport
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(HandleRef hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr WindowHandle);
        public const int SW_MAXIMIZE = 3;
        public const int SW_RESTORE = 9;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        #endregion

        #region Variables
        private Timer timer;
        private static string outputText;
        private static Timer outputTimer;
        private Bitmap bitmap;
        private Image image;
        private Color colorToCheck = Color.FromArgb(225, 220, 212);
        private int sec;
        private int secUntilReCheck = 10;
        private double counter;
        private TimeSpan time;
        private bool bCanStart, bDoubleChecking;
        #endregion

        #region Initialization
        public Main()
        {
            InitializeComponent();

            MouseDown += Main_MouseDown;
            Shown += new EventHandler(Main_Shown);
            outputTimer = new Timer();
            outputTimer.Tick += outputTimer_Tick;
        }

        private bool FocusProcess(string procName, int action)
        {
            Process[] objProcesses = Process.GetProcessesByName(procName); if (objProcesses.Length > 0)
            {
                IntPtr hWnd = IntPtr.Zero;
                hWnd = objProcesses[0].MainWindowHandle;
                ShowWindowAsync(new HandleRef(null, hWnd), action);
                SetForegroundWindow(objProcesses[0].MainWindowHandle);

                return true;
            }

            return false;
        }

        private void Start()
        {
            counter = 0;
            bDoubleChecking = false;
            sec = 0;

            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 1000;
                timer.Tick += timer_Tick;
                Snip();

                return;
            }

            StopTimer();
        }

        public static void StartOutPutTimer(int interval, string text)
        {
            outputText = text;
            outputTimer.Interval = interval;
            outputTimer.Start();
        }
        #endregion

        #region Timers
        private void outputTimer_Tick(object sender, EventArgs e)
        {
            outputTimer.Stop();
            statusLbl.Text = outputText;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            // Show time expired
            sec += 1;
            time = TimeSpan.FromSeconds(sec);
            timeLbl.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                time.Hours,
                time.Minutes,
                time.Seconds);

            if (bDoubleChecking)
            {
                counter += 1;

                if (counter < secUntilReCheck)
                { statusLbl.Text = "Possible pop, checking in " + counter + " sec" ; return; }
                else
                {
                    counter = 0;
                    bDoubleChecking = false;
                    if (TakeScreenshot())
                    {
                        if (CheckPixels()) { Notify(); return; }
                        else statusLbl.Text = "Running";
                    }
                }
            }

            if (TakeScreenshot())
                if (CheckPixels() && !bDoubleChecking)
                    bDoubleChecking = true;
        }

        private void StopTimer()
        {
            statusLbl.Text = "Timer Stopped";
            timer.Stop();
            timer.Dispose();
            timer = null;
            GC.Collect();
        }

        private void Notify()
        {
            StopTimer();
            statusLbl.Text = "Duty Popped!";
            PushNotification("Duty Popped!");
        }
        #endregion

        #region Custom Functions
        private void Snip()
        {
            image = SnippingTool.Snip();
            statusLbl.Text = "Initializing...";

            // Make sure the image isn't empty and start the timer
            if (image == null) return;
            bitmap = new Bitmap(image);

            timer.Start();
            StartOutPutTimer(2000, "Running");
        }

        /// <summary>
        /// Send a push notification to devices
        /// </summary>
        /// <param name="stringToSend">Text to send to the devices</param>
        private void PushNotification(string stringToSend)
        {
            try
            {
                // Push a note to all devices.
                string type = "note", title = stringToSend, body = string.Empty;
                byte[] data = Encoding.ASCII.GetBytes(string.Format("{{ \"type\": \"{0}\", \"title\": \"{1}\", \"body\": \"{2}\" }}", type, title, body));

                var request = System.Net.WebRequest.Create("https://api.pushbullet.com/v2/pushes") as System.Net.HttpWebRequest;
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Credentials = new System.Net.NetworkCredential(Properties.Settings.Default.AccessToken, "");

                request.ContentLength = data.Length;
                string responseJson = null;

                using (var requestStream = request.GetRequestStream())
                {
                    requestStream.Write(data, 0, data.Length);
                    requestStream.Close();
                }

                using (var response = request.GetResponse() as System.Net.HttpWebResponse)
                {
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        responseJson = reader.ReadToEnd();
                    }
                }
            }

            catch { MessageBox.Show(this, "Unable to send information to devices", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        /// <summary>
        /// Take a screenshot using the SnippingTool Window
        /// </summary>
        /// <returns>Was the snip successful</returns>
        private bool TakeScreenshot()
        {
            try
            {
                // Take a new screenshot to check every tick
                image = SnippingTool.Snipper.GetImage();
                bitmap = new Bitmap(image);
                return true;
            }

            catch
            {
                statusLbl.Text = "Unable to get image information";
                startStopBtn_Click(null, null);
                return false;
            }
        }

        /// <summary>
        /// Check the pixels from the snip and see if the right colour is found
        /// </summary>
        /// <returns>Did we find the right colour</returns>
        private bool CheckPixels()
        {
            // Check every pixel to find the colour we're looking for
            for (int x = 0; x < bitmap.Width; x++)
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Color pxl = bitmap.GetPixel(x, y);

                    // When the right colour is found, stop everything and break out of this function
                    if (pxl.A == colorToCheck.A && pxl.R == colorToCheck.R && pxl.G == colorToCheck.G && pxl.B == colorToCheck.B)
                        return true;
                }
            }

            return false;
        }
        #endregion

        #region Control Events
        private void Main_Shown(object sender, EventArgs e)
        {
            // Give focus to FFXIV and this app
            statusLbl.Text = "Making FFXIV window active";
            if (FocusProcess("ffxiv_dx11", SW_MAXIMIZE))
                bCanStart = true;
            FocusProcess("FFXIV_DutyPop", SW_RESTORE);

            if (!bCanStart)
            {
                StartOutPutTimer(2000, "FFXIV process isn't running!");
                return;
            }

            StartOutPutTimer(2000, "Ready");
        }

        /// <summary>
        /// Handles dragging the window
        /// </summary>
        void Main_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void keyBtn_Click(object sender, EventArgs e)
        {
            var token = SetupToken();
            if (!string.IsNullOrEmpty(token))
            {
                Properties.Settings.Default.AccessToken = token;
                Properties.Settings.Default.Save();
            }
        }

        private void closeBtn_Click(object sender, EventArgs e)
        {
            if (timer != null)
                StopTimer();

            Application.Exit();
        }

        private void startStopBtn_Click(object sender, EventArgs e)
        {
            if (!bCanStart || !IsTokenSetup())
            {
                MessageBox.Show("Unable to start, is ffxiv running and the access token setup?");
                return;
            }

            Start();
        }
        #endregion

        #region Token
        private bool IsTokenSetup()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.AccessToken))
                return false;
            return true;
        }

        private string SetupToken()
        {
            var result = Interaction.InputBox("Enter PushBullet Access Token", "Access Token", Properties.Settings.Default.AccessToken, -1, -1);
            return result;
        }
        #endregion
    }
}
