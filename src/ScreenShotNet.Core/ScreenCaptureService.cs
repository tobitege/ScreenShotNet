using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using static Vanara.PInvoke.Gdi32;
using static Vanara.PInvoke.User32;

namespace ScreenShotNet
{
    public static class ScreenCaptureService
    {
        public static Bitmap CaptureRegion(Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(region), "Capture region width and height must be greater than zero.");
            }

            var rasterOperation = (RasterOperationMode)((int)RasterOperationMode.SRCCOPY | (int)RasterOperationMode.CAPTUREBLT);

            using var screenDc = GetDC(HWND.NULL);
            using var memoryDc = CreateCompatibleDC(screenDc);
            using var bitmapHandle = CreateCompatibleBitmap(screenDc, region.Width, region.Height);
            using (memoryDc.SelectObject(bitmapHandle))
            {
                if (!BitBlt(memoryDc, 0, 0, region.Width, region.Height, screenDc, region.X, region.Y, rasterOperation))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "BitBlt failed while capturing the screen.");
                }
            }

            using var image = Image.FromHbitmap(bitmapHandle.DangerousGetHandle());
            return new Bitmap(image);
        }
    }
}
