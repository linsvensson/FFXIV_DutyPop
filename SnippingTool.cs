using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FFXIV_DutyPop
{
    public partial class SnippingTool : Form
    {
        public static SnippingTool Snipper;

        public static Image Snip()
        {
            var rc = Screen.AllScreens[0].Bounds;
           
            using (Bitmap bmp = new Bitmap(rc.Width, rc.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics gr = Graphics.FromImage(bmp))
                    gr.CopyFromScreen(0, 0, 0, 0, bmp.Size);
                using (Snipper = new SnippingTool(bmp))
                {
                    if (Snipper.ShowDialog() == DialogResult.OK)
                    {
                        return Snipper.Image;
                    }
                }

                Main.StartOutPutTimer(500, "Ready");
                return null;
            }
        }

        public SnippingTool(Image screenshot)
        {
            InitializeComponent();

            BackgroundImage = screenshot;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            DoubleBuffered = true;
        }

        public Image Image { get; set; }

        private Rectangle rcSelect = new Rectangle();

        private Point pntStart;

        public Image GetImage()
        {
            var rc = Screen.AllScreens[0].Bounds;
            using (Bitmap bmp = new Bitmap(rc.Width, rc.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            {
                using (Graphics gr = Graphics.FromImage(bmp))
                    gr.CopyFromScreen(0, 0, 0, 0, bmp.Size);

                if (rcSelect.Width <= 0 || rcSelect.Height <= 0) return null;
                Image = new Bitmap(rcSelect.Width, rcSelect.Height);
                using (Graphics gr = Graphics.FromImage(Image))
                {
                    gr.DrawImage(bmp, new Rectangle(0, 0, Image.Width, Image.Height),
                        rcSelect, GraphicsUnit.Pixel);
                }
            }

            return Image;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            // Start the snip on mouse down
            if (e.Button != MouseButtons.Left) return;
            pntStart = e.Location;
            rcSelect = new Rectangle(e.Location, new Size(0, 0));
            this.Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            // Modify the selection on mouse move
            if (e.Button != MouseButtons.Left) return;
            int x1 = Math.Min(e.X, pntStart.X);
            int y1 = Math.Min(e.Y, pntStart.Y);
            int x2 = Math.Max(e.X, pntStart.X);
            int y2 = Math.Max(e.Y, pntStart.Y);
            rcSelect = new Rectangle(x1, y1, x2 - x1, y2 - y1);
            this.Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            // Complete the snip on mouse-up
            if (rcSelect.Width <= 0 || rcSelect.Height <= 0) return;
            Image = new Bitmap(rcSelect.Width, rcSelect.Height);
            using (Graphics gr = Graphics.FromImage(Image))
            {
                gr.DrawImage(this.BackgroundImage, new Rectangle(0, 0, Image.Width, Image.Height),
                    rcSelect, GraphicsUnit.Pixel);
            }
            DialogResult = DialogResult.OK;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            // Draw the current selection
            using (Brush br = new SolidBrush(Color.FromArgb(120, Color.White)))
            {
                int x1 = rcSelect.X; int x2 = rcSelect.X + rcSelect.Width;
                int y1 = rcSelect.Y; int y2 = rcSelect.Y + rcSelect.Height;
                e.Graphics.FillRectangle(br, new Rectangle(0, 0, x1, this.Height));
                e.Graphics.FillRectangle(br, new Rectangle(x2, 0, this.Width - x2, this.Height));
                e.Graphics.FillRectangle(br, new Rectangle(x1, 0, x2 - x1, y1));
                e.Graphics.FillRectangle(br, new Rectangle(x1, y2, x2 - x1, this.Height - y2));
            }
            using (Pen pen = new Pen(Color.Red, 3))
            {
                e.Graphics.DrawRectangle(pen, rcSelect);
            }
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Allow canceling the snip with the Escape key
            if (keyData == Keys.Escape) this.DialogResult = DialogResult.Cancel;
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
