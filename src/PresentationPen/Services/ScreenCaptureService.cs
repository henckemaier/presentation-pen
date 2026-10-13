using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using PresentationPen.Interop;
using Drawing = System.Drawing;

namespace PresentationPen.Services
{
    public static class ScreenCaptureService
    {
        /// <summary>Captura uma área da tela (pixels físicos), incluindo janelas em camadas como o overlay.</summary>
        public static Drawing.Bitmap CaptureBitmap(Drawing.Rectangle bounds)
        {
            // 24bpp: com 32bpp o BitBlt pode deixar o canal alfa zerado e o PNG sair transparente.
            var bmp = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format24bppRgb);
            using var g = Drawing.Graphics.FromImage(bmp);
            // BitBlt direto: Graphics.CopyFromScreen não aceita SRCCOPY|CAPTUREBLT combinados.
            IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
            IntPtr bitmapDc = g.GetHdc();
            try
            {
                NativeMethods.BitBlt(bitmapDc, 0, 0, bounds.Width, bounds.Height, screenDc, bounds.Left, bounds.Top,
                    NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
            }
            finally
            {
                g.ReleaseHdc(bitmapDc);
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
            return bmp;
        }

        public static BitmapSource ToBitmapSource(Drawing.Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }

        public static string SavePng(Drawing.Bitmap bmp, string folder)
        {
            Directory.CreateDirectory(folder);
            string baseName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string path = Path.Combine(folder, baseName + ".png");
            for (int i = 1; File.Exists(path); i++)
                path = Path.Combine(folder, $"{baseName}_{i}.png");

            bmp.Save(path, Drawing.Imaging.ImageFormat.Png);
            return path;
        }

        /// <returns>false se a área de transferência estava ocupada por outro processo.</returns>
        public static bool CopyToClipboard(BitmapSource image)
        {
            try
            {
                Clipboard.SetImage(image);
                return true;
            }
            catch (Exception)
            {
                return false; // o arquivo já foi salvo de qualquer forma
            }
        }
    }
}
