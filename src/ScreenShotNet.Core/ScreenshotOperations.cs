using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ScreenShotNet
{
    public static class ScreenshotOperations
    {
        private const int CursorReticleGreenshotOffset = 8;
        private const int CursorReticleRadius = 10;
        private const int CursorReticleGap = 4;
        private const int CursorReticleLineLength = 8;

        public static Bitmap CaptureScreenshot(Rectangle region, double delaySeconds)
        {
            return CaptureScreenshot(region, delaySeconds, null);
        }

        public static Bitmap CaptureScreenshot(Rectangle region, double delaySeconds, string windowTitle)
        {
            return CaptureScreenshot(region, delaySeconds, windowTitle, false);
        }

        public static Bitmap CaptureScreenshot(Rectangle region, double delaySeconds, string windowTitle, bool useRelativeToWindow)
        {
            Rectangle ignoredCaptureRegion;
            Point ignoredCursorScreenPosition;
            return CaptureScreenshot(region, delaySeconds, windowTitle, useRelativeToWindow, out ignoredCaptureRegion, out ignoredCursorScreenPosition);
        }

        public static Bitmap CaptureScreenshot(Rectangle region, double delaySeconds, string windowTitle, bool useRelativeToWindow, out Rectangle captureRegion, out Point cursorScreenPosition)
        {
            var effectiveRegion = region;

            if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                if (!WindowActivationService.TryFindWindowByTitlePrefix(windowTitle, out var windowMatch, out var windowError))
                {
                    throw new InvalidOperationException(windowError);
                }

                if (!WindowActivationService.TryBringWindowToForeground(windowMatch, out windowError))
                {
                    throw new InvalidOperationException(windowError);
                }

                if (useRelativeToWindow)
                {
                    if (!WindowActivationService.TryGetWindowBounds(windowMatch, out var windowBounds, out windowError))
                    {
                        throw new InvalidOperationException(windowError);
                    }

                    effectiveRegion = new Rectangle(
                        windowBounds.Left + region.X,
                        windowBounds.Top + region.Y,
                        region.Width,
                        region.Height);
                }
            }
            else if (useRelativeToWindow)
            {
                throw new ArgumentException("Relative capture offsets require a window title.", nameof(useRelativeToWindow));
            }

            if (delaySeconds > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }

            captureRegion = effectiveRegion;
            return ScreenCaptureService.CaptureRegion(effectiveRegion, out cursorScreenPosition);
        }

        public static Bitmap CaptureWindowScreenshot(string windowTitle, double delaySeconds, out string matchedWindowTitle)
        {
            Rectangle ignoredCaptureRegion;
            Point ignoredCursorScreenPosition;
            return CaptureWindowScreenshot(windowTitle, delaySeconds, out matchedWindowTitle, out ignoredCaptureRegion, out ignoredCursorScreenPosition);
        }

        public static Bitmap CaptureWindowScreenshot(string windowTitle, double delaySeconds, out string matchedWindowTitle, out Rectangle captureRegion, out Point cursorScreenPosition)
        {
            return CaptureWindowScreenshot(windowTitle, delaySeconds, false, out matchedWindowTitle, out captureRegion, out cursorScreenPosition);
        }

        public static Bitmap CaptureWindowScreenshot(string windowTitle, double delaySeconds, bool useRawWindowBounds, out string matchedWindowTitle, out Rectangle captureRegion, out Point cursorScreenPosition)
        {
            matchedWindowTitle = null;
            captureRegion = Rectangle.Empty;

            if (delaySeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay must be zero or greater.");
            }

            if (!WindowActivationService.TryFindWindowByTitlePrefix(windowTitle, out var windowMatch, out var windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            if (!WindowActivationService.TryBringWindowToForeground(windowMatch, out windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            if (delaySeconds > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }

            Rectangle bounds;
            var boundsResolved = useRawWindowBounds
                ? WindowActivationService.TryGetRawWindowBounds(windowMatch, out bounds, out windowError)
                : WindowActivationService.TryGetWindowBounds(windowMatch, out bounds, out windowError);
            if (!boundsResolved)
            {
                throw new InvalidOperationException(windowError);
            }

            matchedWindowTitle = windowMatch.Title;
            captureRegion = bounds;
            return ScreenCaptureService.CaptureRegion(bounds, out cursorScreenPosition);
        }

        public static Bitmap CaptureCenteredWindowScreenshot(string windowTitle, int width, int height, double delaySeconds, out string matchedWindowTitle, out Rectangle captureRegion)
        {
            Point ignoredCursorScreenPosition;
            return CaptureCenteredWindowScreenshot(windowTitle, width, height, delaySeconds, out matchedWindowTitle, out captureRegion, out ignoredCursorScreenPosition);
        }

        public static Bitmap CaptureCenteredWindowScreenshot(string windowTitle, int width, int height, double delaySeconds, out string matchedWindowTitle, out Rectangle captureRegion, out Point cursorScreenPosition)
        {
            matchedWindowTitle = null;
            captureRegion = Rectangle.Empty;

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
            }

            if (delaySeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay must be zero or greater.");
            }

            if (!WindowActivationService.TryFindWindowByTitlePrefix(windowTitle, out var windowMatch, out var windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            if (!WindowActivationService.TryBringWindowToForeground(windowMatch, out windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            if (delaySeconds > 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
            }

            if (!WindowActivationService.TryGetWindowBounds(windowMatch, out var windowBounds, out windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            if (width > windowBounds.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(width), string.Format("Requested width {0} exceeds matched window '{1}' width {2}.", width, windowMatch.Title, windowBounds.Width));
            }

            if (height > windowBounds.Height)
            {
                throw new ArgumentOutOfRangeException(nameof(height), string.Format("Requested height {0} exceeds matched window '{1}' height {2}.", height, windowMatch.Title, windowBounds.Height));
            }

            var centeredX = windowBounds.Left + ((windowBounds.Width - width) / 2);
            var centeredY = windowBounds.Top + ((windowBounds.Height - height) / 2);

            captureRegion = new Rectangle(centeredX, centeredY, width, height);
            matchedWindowTitle = windowMatch.Title;
            return ScreenCaptureService.CaptureRegion(captureRegion, out cursorScreenPosition);
        }

        public static string PrepareOutputPath(string outputPath)
        {
            return PrepareOutputPath(outputPath, ".png", false);
        }

        public static string PrepareOutputPath(string outputPath, string requiredExtension, bool replaceExistingExtension)
        {
            var path = outputPath;
            if (Path.HasExtension(path))
            {
                if (replaceExistingExtension)
                {
                    path = Path.ChangeExtension(path, requiredExtension);
                }
            }
            else
            {
                path += requiredExtension;
            }

            path = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return path;
        }

        public static bool TrySaveScreenshotToFile(Bitmap screenshot, string outputPath, out string savedPath, out string errorMessage)
        {
            return TrySaveScreenshotToFile(screenshot, outputPath, null, out savedPath, out errorMessage);
        }

        public static bool TrySaveScreenshotToFile(Bitmap screenshot, string outputPath, string requestedFormat, out string savedPath, out string errorMessage)
        {
            savedPath = null;
            errorMessage = null;

            if (!TryResolveOutputFormat(outputPath, requestedFormat, out var imageFormat, out var requiredExtension, out var formatErrorMessage))
            {
                errorMessage = formatErrorMessage;
                return false;
            }

            string targetPath;
            try
            {
                var shouldReplaceExistingExtension = !string.IsNullOrWhiteSpace(requestedFormat);
                targetPath = PrepareOutputPath(outputPath, requiredExtension, shouldReplaceExistingExtension);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException || ex is IOException || ex is UnauthorizedAccessException)
            {
                errorMessage = $"Failed to prepare output path '{outputPath}': {ex.Message}";
                return false;
            }

            try
            {
                screenshot.Save(targetPath, imageFormat);
                savedPath = targetPath;
                return true;
            }
            catch (Exception ex) when (ex is ExternalException || ex is UnauthorizedAccessException || ex is IOException || ex is ArgumentException)
            {
                errorMessage = $"Failed to save screenshot to '{targetPath}': {ex.Message}";
                return false;
            }
        }

        public static bool TryNormalizeOutputFormat(string value, out string normalizedFormat)
        {
            normalizedFormat = null;
            if (!TryGetImageFormatInfo(value, out var _, out var canonicalFormat, out var _, out var _))
            {
                return false;
            }

            normalizedFormat = canonicalFormat;
            return true;
        }

        public static bool TryResolveOutputFormat(string outputPath, string requestedFormat, out ImageFormat imageFormat, out string requiredExtension, out string errorMessage)
        {
            if (!TryResolveOutputFormatInfo(outputPath, requestedFormat, out var formatInfo, out errorMessage))
            {
                imageFormat = null;
                requiredExtension = null;
                return false;
            }

            imageFormat = formatInfo.ImageFormat;
            requiredExtension = formatInfo.Extension;
            return true;
        }

        public static bool TrySetClipboardImageWithRetry(Image screenshot, int maxAttempts, int delayMilliseconds, out string errorMessage)
        {
            errorMessage = null;
            var attempts = Math.Max(1, maxAttempts);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    Clipboard.SetImage(screenshot);
                    return true;
                }
                catch (ExternalException ex)
                {
                    if (attempt == attempts)
                    {
                        errorMessage = $"Clipboard is busy after {attempts} attempts: {ex.Message}";
                        return false;
                    }

                    if (delayMilliseconds > 0)
                    {
                        Thread.Sleep(delayMilliseconds);
                    }
                }
                catch (Exception ex) when (ex is ThreadStateException || ex is InvalidOperationException || ex is ArgumentException)
                {
                    errorMessage = $"Failed to write to clipboard: {ex.Message}";
                    return false;
                }
            }

            errorMessage = "Failed to write to clipboard.";
            return false;
        }

        public static void ApplyWatermark(Bitmap screenshot, WatermarkOptions watermark)
        {
            using var graphics = Graphics.FromImage(screenshot);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var font = new Font(FontFamily.GenericSansSerif, watermark.Size, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(watermark.Color);
            graphics.DrawString(watermark.Text, font, brush, new PointF(watermark.X, watermark.Y));
        }

        public static bool ApplyCursorReticle(Bitmap screenshot, Rectangle captureRegion, Point cursorScreenPosition)
        {
            if (!captureRegion.Contains(cursorScreenPosition))
            {
                return false;
            }

            var relativeCursorPosition = new Point(cursorScreenPosition.X - captureRegion.Left, cursorScreenPosition.Y - captureRegion.Top);
            return ApplyCursorReticleAtRelativePosition(screenshot, relativeCursorPosition);
        }

        public static bool ApplyCursorReticleForCapture(Bitmap screenshot, Rectangle captureRegion, Point cursorScreenPosition)
        {
            if (!captureRegion.Contains(cursorScreenPosition))
            {
                return false;
            }

            var relativeCursorPosition = new Point(cursorScreenPosition.X - captureRegion.Left, cursorScreenPosition.Y - captureRegion.Top);
            if (TryGetCurrentCursorLayout(out var cursorSize, out var cursorHotspot))
            {
                relativeCursorPosition = new Point(
                    relativeCursorPosition.X - cursorHotspot.X + (cursorSize.Width / 2) - CursorReticleRadius - CursorReticleGreenshotOffset,
                    relativeCursorPosition.Y - cursorHotspot.Y + (cursorSize.Height / 2) - CursorReticleRadius - CursorReticleGreenshotOffset);
            }

            return ApplyCursorReticleAtRelativePosition(screenshot, relativeCursorPosition);
        }

        private static bool ApplyCursorReticleAtRelativePosition(Bitmap screenshot, Point relativeCursorPosition)
        {
            using var graphics = Graphics.FromImage(screenshot);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var pen = new Pen(Color.Red, 2f);

            graphics.DrawEllipse(
                pen,
                relativeCursorPosition.X - CursorReticleRadius,
                relativeCursorPosition.Y - CursorReticleRadius,
                CursorReticleRadius * 2,
                CursorReticleRadius * 2);

            graphics.DrawLine(pen, relativeCursorPosition.X - CursorReticleRadius - CursorReticleLineLength, relativeCursorPosition.Y, relativeCursorPosition.X - CursorReticleGap, relativeCursorPosition.Y);
            graphics.DrawLine(pen, relativeCursorPosition.X + CursorReticleGap, relativeCursorPosition.Y, relativeCursorPosition.X + CursorReticleRadius + CursorReticleLineLength, relativeCursorPosition.Y);
            graphics.DrawLine(pen, relativeCursorPosition.X, relativeCursorPosition.Y - CursorReticleRadius - CursorReticleLineLength, relativeCursorPosition.X, relativeCursorPosition.Y - CursorReticleGap);
            graphics.DrawLine(pen, relativeCursorPosition.X, relativeCursorPosition.Y + CursorReticleGap, relativeCursorPosition.X, relativeCursorPosition.Y + CursorReticleRadius + CursorReticleLineLength);

            return true;
        }

        private static bool TryGetCurrentCursorLayout(out Size cursorSize, out Point cursorHotspot)
        {
            cursorSize = Size.Empty;
            cursorHotspot = Point.Empty;

            var cursorInfo = new CursorInfo
            {
                cbSize = Marshal.SizeOf(typeof(CursorInfo))
            };

            if (!GetCursorInfo(ref cursorInfo))
            {
                return false;
            }

            if ((cursorInfo.flags & CursorShowing) == 0 || cursorInfo.hCursor == IntPtr.Zero)
            {
                return false;
            }

            if (!GetIconInfo(cursorInfo.hCursor, out var iconInfo))
            {
                return false;
            }

            try
            {
                if (!TryGetCursorSize(iconInfo, out cursorSize))
                {
                    return false;
                }

                cursorHotspot = new Point(iconInfo.xHotspot, iconInfo.yHotspot);
                return true;
            }
            finally
            {
                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmMask);
                }

                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    DeleteObject(iconInfo.hbmColor);
                }
            }
        }

        private static bool TryGetCursorSize(IconInfo iconInfo, out Size cursorSize)
        {
            cursorSize = Size.Empty;

            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                if (GetObject(iconInfo.hbmColor, Marshal.SizeOf(typeof(BitmapInfoHeader)), out var colorBitmap) == 0)
                {
                    return false;
                }

                cursorSize = new Size(colorBitmap.bmWidth, Math.Abs(colorBitmap.bmHeight));
                return cursorSize.Width > 0 && cursorSize.Height > 0;
            }

            if (iconInfo.hbmMask == IntPtr.Zero)
            {
                return false;
            }

            if (GetObject(iconInfo.hbmMask, Marshal.SizeOf(typeof(BitmapInfoHeader)), out var maskBitmap) == 0)
            {
                return false;
            }

            var height = Math.Abs(maskBitmap.bmHeight);
            if (!iconInfo.fIcon && height > 1)
            {
                height /= 2;
            }

            cursorSize = new Size(maskBitmap.bmWidth, height);
            return cursorSize.Width > 0 && cursorSize.Height > 0;
        }


        public static bool TryCreateInlineImageDataUri(Bitmap screenshot, string requestedFormat, int maxEncodedBytes, out string dataUri, out long encodedByteCount, out string errorMessage)
        {
            if (!TryResolveOutputFormatInfo("capture", requestedFormat, out var formatInfo, out var formatErrorMessage))
            {
                dataUri = null;
                encodedByteCount = 0;
                errorMessage = formatErrorMessage;
                return false;
            }

            using var stream = new MemoryStream();
            screenshot.Save(stream, formatInfo.ImageFormat);
            encodedByteCount = stream.Length;
            if (maxEncodedBytes > 0 && encodedByteCount > maxEncodedBytes)
            {
                dataUri = null;
                errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    "Inline screenshot exceeds {0:N0} bytes ({1:N0} bytes encoded). Use savePath to write the image to a file instead.",
                    maxEncodedBytes,
                    encodedByteCount);
                return false;
            }

            dataUri = string.Format(
                CultureInfo.InvariantCulture,
                "data:{0};base64,{1}",
                formatInfo.MimeType,
                Convert.ToBase64String(stream.ToArray()));
            errorMessage = null;
            return true;
        }

        public static string ToDataUri(Bitmap screenshot, string requestedFormat)
        {
            if (!TryCreateInlineImageDataUri(screenshot, requestedFormat, maxEncodedBytes: 0, out var dataUri, out _, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(requestedFormat));
            }

            return dataUri;
        }

        private static bool TryResolveOutputFormatInfo(string outputPath, string requestedFormat, out OutputFormatInfo formatInfo, out string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(requestedFormat))
            {
                if (TryGetImageFormatInfo(requestedFormat, out var imageFormat, out var canonicalFormat, out var extension, out var mimeType))
                {
                    formatInfo = new OutputFormatInfo(imageFormat, canonicalFormat, extension, mimeType);
                    errorMessage = null;
                    return true;
                }

                formatInfo = null;
                errorMessage = "Invalid output format. Supported formats: png, jpg, bmp, gif, tiff.";
                return false;
            }

            string extensionFromPath;
            try
            {
                extensionFromPath = Path.GetExtension(outputPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                formatInfo = null;
                errorMessage = $"Failed to inspect output path '{outputPath}': {ex.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(extensionFromPath))
            {
                formatInfo = new OutputFormatInfo(ImageFormat.Png, "png", ".png", "image/png");
                errorMessage = null;
                return true;
            }

            if (TryGetImageFormatInfo(extensionFromPath, out var pathImageFormat, out var pathCanonicalFormat, out var pathExtension, out var pathMimeType))
            {
                formatInfo = new OutputFormatInfo(pathImageFormat, pathCanonicalFormat, pathExtension, pathMimeType);
                errorMessage = null;
                return true;
            }

            formatInfo = null;
            errorMessage = $"Unsupported file extension '{extensionFromPath}'. Supported formats: .png, .jpg, .bmp, .gif, .tiff.";
            return false;
        }

        private static bool TryGetImageFormatInfo(string token, out ImageFormat imageFormat, out string canonicalFormat, out string extension, out string mimeType)
        {
            imageFormat = null;
            canonicalFormat = null;
            extension = null;
            mimeType = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = token.Trim().TrimStart('.').ToLowerInvariant();
            switch (normalized)
            {
                case "png":
                    imageFormat = ImageFormat.Png;
                    canonicalFormat = "png";
                    extension = ".png";
                    mimeType = "image/png";
                    return true;
                case "jpg":
                case "jpeg":
                    imageFormat = ImageFormat.Jpeg;
                    canonicalFormat = "jpg";
                    extension = ".jpg";
                    mimeType = "image/jpeg";
                    return true;
                case "bmp":
                    imageFormat = ImageFormat.Bmp;
                    canonicalFormat = "bmp";
                    extension = ".bmp";
                    mimeType = "image/bmp";
                    return true;
                case "gif":
                    imageFormat = ImageFormat.Gif;
                    canonicalFormat = "gif";
                    extension = ".gif";
                    mimeType = "image/gif";
                    return true;
                case "tif":
                case "tiff":
                    imageFormat = ImageFormat.Tiff;
                    canonicalFormat = "tiff";
                    extension = ".tiff";
                    mimeType = "image/tiff";
                    return true;
                default:
                    return false;
            }
        }

        private sealed class OutputFormatInfo
        {
            public OutputFormatInfo(ImageFormat imageFormat, string canonicalFormat, string extension, string mimeType)
            {
                ImageFormat = imageFormat;
                CanonicalFormat = canonicalFormat;
                Extension = extension;
                MimeType = mimeType;
            }

            public ImageFormat ImageFormat { get; }

            public string CanonicalFormat { get; }

            public string Extension { get; }

            public string MimeType { get; }
        }

        private const int CursorShowing = 0x00000001;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorInfo(ref CursorInfo pci);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BitmapInfoHeader lpvObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct CursorInfo
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public Point ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public short bmPlanes;
            public short bmBitsPixel;
            public IntPtr bmBits;
        }

    }
}
