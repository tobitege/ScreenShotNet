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
        public const int MaxCapturePixels = 64_000_000;

        public static bool IsValidRegion(Rectangle region)
        {
            return region.Width > 0 && region.Height > 0 &&
                   (long)region.Width * region.Height <= MaxCapturePixels &&
                   (long)region.X + region.Width <= int.MaxValue &&
                   (long)region.Y + region.Height <= int.MaxValue;
        }

        internal static void ValidateRegion(Rectangle region)
        {
            if (!IsValidRegion(region))
            {
                throw new ArgumentOutOfRangeException(nameof(region),
                    "Capture region must have positive dimensions, at most 64 million pixels, and no coordinate overflow.");
            }
        }

        public static Bitmap CaptureRegion(Rectangle region)
        {
            Point ignoredCursorPosition;
            return CaptureRegion(region, out ignoredCursorPosition);
        }

        public static Bitmap CaptureRegion(Rectangle region, out Point cursorScreenPosition)
        {
            ValidateRegion(region);

            if (!TryGetCursorPosition(out cursorScreenPosition))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read the cursor position.");
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

            return Image.FromHbitmap(bitmapHandle.DangerousGetHandle());
        }

        private static bool TryGetCursorPosition(out Point cursorScreenPosition)
        {
            cursorScreenPosition = Point.Empty;
            if (!GetCursorPos(out var point))
            {
                return false;
            }

            cursorScreenPosition = new Point(point.X, point.Y);
            return true;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorPos(out NativePoint lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }
    }
}
