using System;
using System.Windows.Forms;

namespace FFXIV_DutyPop
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Main m = new Main();
            m.StartPosition = FormStartPosition.Manual;
            m.Location = Screen.PrimaryScreen.Bounds.Location;
            Application.Run(m);
        }
    }
}
