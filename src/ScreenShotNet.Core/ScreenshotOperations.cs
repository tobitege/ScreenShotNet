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

            return ScreenCaptureService.CaptureRegion(effectiveRegion);
        }

        public static Bitmap CaptureWindowScreenshot(string windowTitle, double delaySeconds, out string matchedWindowTitle)
        {
            matchedWindowTitle = null;

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

            if (!WindowActivationService.TryGetWindowBounds(windowMatch, out var bounds, out windowError))
            {
                throw new InvalidOperationException(windowError);
            }

            matchedWindowTitle = windowMatch.Title;
            return ScreenCaptureService.CaptureRegion(bounds);
        }

        public static Bitmap CaptureCenteredWindowScreenshot(string windowTitle, int width, int height, double delaySeconds, out string matchedWindowTitle, out Rectangle captureRegion)
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
            return ScreenCaptureService.CaptureRegion(captureRegion);
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

        public static string ToDataUri(Bitmap screenshot, string requestedFormat)
        {
            if (!TryResolveOutputFormatInfo("capture", requestedFormat, out var formatInfo, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(requestedFormat));
            }

            using var stream = new MemoryStream();
            screenshot.Save(stream, formatInfo.ImageFormat);
            return string.Format(
                CultureInfo.InvariantCulture,
                "data:{0};base64,{1}",
                formatInfo.MimeType,
                Convert.ToBase64String(stream.ToArray()));
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
    }
}
