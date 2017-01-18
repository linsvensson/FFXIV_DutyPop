using FFXIVAPP.Memory;
using FFXIVAPP.Memory.Core;
using FFXIVAPP.Memory.Models;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static FFXIVAPP.Memory.Reader;

namespace FFXIV_DutyPop
{
    public partial class Main : Form
    {
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(HandleRef hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr WindowHandle);
        public const int SW_MAXIMIZE = 3;
        public const int SW_RESTORE = 9;

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        private System.Windows.Forms.Timer timer;
        private static string outputText;
        private static System.Windows.Forms.Timer outputTimer;
        private Bitmap bitmap;
        private Image image;
        private Color colorToCheck = Color.FromArgb(225, 220, 212);
        private int sec;
        private int secUntilReCheck = 10;
        private double counter;
        private TimeSpan time;
        private bool bCanStart, bDoubleChecking;

        // DX11
        private Process[] processes = Process.GetProcessesByName("ffxiv_dx11");
        // For chatlog you must locally store previous array offsets and indexes in order to pull the correct log from the last time you read it.
        int previousArrayOffset = 0;
        int previousIndex = 0;

        public Main()
        {
            InitializeComponent();

            MouseDown += Main_MouseDown;
            Shown += new EventHandler(Main_Shown);
            outputTimer = new System.Windows.Forms.Timer();
            outputTimer.Tick += outputTimer_Tick;

            // Find the process
            if (processes.Length > 0)
            {
                // supported: English, Chinese, Japanese, French, German, Korean
                string gameLanguage = "English";
                Process process = processes[0];
                ProcessModel processModel = new ProcessModel
                {
                    Process = process,
                    IsWin64 = true
                };
    MemoryHandler.Instance.SetProcess(processModel, gameLanguage);
            }
        }

        #region Initialization
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
            label1.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
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

        private void Snip()
        {
            image = SnippingTool.Snip();
            statusLbl.Text = "Initializing...";

            if (image == null) return;
            bitmap = new Bitmap(image);
            timer.Start();
            StartOutPutTimer(2000, "Running");
        }

        private void PushNotification(string stringToSend)
        {
            try
            {
                // Push a note to all devices.
                String type = "note", title = stringToSend, body = "";
                byte[] data = Encoding.ASCII.GetBytes(String.Format("{{ \"type\": \"{0}\", \"title\": \"{1}\", \"body\": \"{2}\" }}", type, title, body));

                var request = System.Net.WebRequest.Create("https://api.pushbullet.com/v2/pushes") as System.Net.HttpWebRequest;
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Credentials = new System.Net.NetworkCredential(Properties.Settings.Default.AccessToken, "");

                request.ContentLength = data.Length;
                String responseJson = null;

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
                button1_Click(null, null);
                return false;
            }
        }

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

        #region Button Clicks
        private void button1_Click(object sender, EventArgs e)
        {
            //ReadChat();
            //return;

            if (!bCanStart || !IsTokenSetup())
            {
                MessageBox.Show("Cannot start, is ffxiv running and the access token setup?");
                return;
            }

            Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (timer != null)
                StopTimer();

            GC.Collect();
            Application.Exit();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var token = SetupToken();
            if (!string.IsNullOrEmpty(token))
            {
                Properties.Settings.Default.AccessToken = token;
                Properties.Settings.Default.Save();
            }
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

        #region CrapForLater
        private void ReadChat()
        {
            ChatLogReadResult readResult = Reader.GetChatLog(previousArrayOffset, previousIndex);
            List<ChatLogEntry> chatLogEntries = readResult.ChatLogEntries;

            previousArrayOffset = readResult.PreviousOffset;
            previousIndex = readResult.PreviousArrayIndex;

            var info = Reader.GetTargetInfo();
            Console.WriteLine(info.TargetEntity.CurrentTarget);

            return;

            //IntPtr baseAddress = process.MainModule.BaseAddress;
            //Console.WriteLine("Base Address: " + baseAddress);

            //IntPtr firstAddress = IntPtr.Add(baseAddress, 0xF8BEFC);
            //IntPtr firstAddressValue = (IntPtr)BitConverter.ToInt32(MemoryHandler.ReadMemory(process, firstAddress, 4, out bytesRead), 0);
            //IntPtr finalAddr = IntPtr.Add(firstAddressValue, 0x1690);
            //Console.WriteLine("Final Address: " + finalAddr.ToString("X"));

            //byte[] memoryOutput = MemoryHandler.ReadMemory(process, finalAddr, 4, out bytesRead);

            //int value = BitConverter.ToInt32(memoryOutput, 0);
            //Console.WriteLine("Read Value: " + value);
        }
        #endregion
    }
}
