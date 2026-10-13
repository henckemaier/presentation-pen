using System;
using PresentationPen.Interop;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;

namespace PresentationPen.Services
{
    /// <summary>Gera o ícone da bandeja em tempo de execução (sem arquivo .ico).</summary>
    public static class IconFactory
    {
        public static Drawing.Icon CreateTrayIcon()
        {
            using var bmp = new Drawing.Bitmap(32, 32);
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Drawing.Color.Transparent);
                using var bg = new Drawing.SolidBrush(Drawing.Color.FromArgb(255, 229, 57, 53));
                g.FillEllipse(bg, 1, 1, 30, 30);
                using var pen = new Drawing.Pen(Drawing.Color.White, 3.5f)
                {
                    StartCap = Drawing2D.LineCap.Round,
                    EndCap = Drawing2D.LineCap.Round,
                };
                g.DrawBezier(pen, 7, 21, 11, 6, 19, 27, 25, 11);
            }

            IntPtr hIcon = bmp.GetHicon();
            try
            {
                using var temp = Drawing.Icon.FromHandle(hIcon);
                return (Drawing.Icon)temp.Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }
    }
}
