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
    internal static class Program
    {
        private const int SuccessExitCode = 0;
        private const int ValidationExitCode = 2;
        private const int RuntimeExitCode = 3;

        [STAThread]
        private static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            if (!TryParseArguments(args, out var options, out var errorMessage, out var showHelp))
            {
                Console.Error.WriteLine(errorMessage);
                Console.Error.WriteLine();
                WriteUsage();
                return ValidationExitCode;
            }

            if (showHelp)
            {
                WriteUsage();
                return SuccessExitCode;
            }

            Bitmap screenshot = null;
            try
            {
                if (options.DelaySeconds > 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(options.DelaySeconds));
                }

                screenshot = ScreenCaptureService.CaptureRegion(options.Region);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Capture failed: {ex.Message}");
                return RuntimeExitCode;
            }

            try
            {
                using (screenshot)
                {
                    if (options.Watermark != null)
                    {
                        ApplyWatermark(screenshot, options.Watermark);
                    }

                    if (options.CopyToClipboard)
                    {
                        if (!TrySetClipboardImageWithRetry(screenshot, maxAttempts: 8, delayMilliseconds: 75, out var clipboardError))
                        {
                            Console.Error.WriteLine(clipboardError);
                            return RuntimeExitCode;
                        }

                        Console.WriteLine("Screenshot copied to clipboard.");
                    }

                    if (options.SaveToFile)
                    {
                        if (!TrySaveScreenshotToFile(screenshot, options.FilePath, options.OutputFormat, out var targetPath, out var fileError))
                        {
                            Console.Error.WriteLine(fileError);
                            return RuntimeExitCode;
                        }

                        Console.WriteLine($"Screenshot saved to: {targetPath}");
                    }
                }

                return SuccessExitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Runtime error: {ex.Message}");
                return RuntimeExitCode;
            }
        }

        internal static string PrepareOutputPath(string outputPath)
        {
            return PrepareOutputPath(outputPath, ".png", false);
        }

        internal static string PrepareOutputPath(string outputPath, string requiredExtension, bool replaceExistingExtension)
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

        internal static bool TrySaveScreenshotToFile(Bitmap screenshot, string outputPath, out string savedPath, out string errorMessage)
        {
            return TrySaveScreenshotToFile(screenshot, outputPath, null, out savedPath, out errorMessage);
        }

        internal static bool TrySaveScreenshotToFile(Bitmap screenshot, string outputPath, string requestedFormat, out string savedPath, out string errorMessage)
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

        internal static bool TryNormalizeOutputFormat(string value, out string normalizedFormat)
        {
            normalizedFormat = null;
            if (!TryGetImageFormatInfo(value, out var _, out var canonicalFormat, out var _))
            {
                return false;
            }

            normalizedFormat = canonicalFormat;
            return true;
        }

        internal static bool TryResolveOutputFormat(string outputPath, string requestedFormat, out ImageFormat imageFormat, out string requiredExtension, out string errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(requestedFormat))
            {
                if (TryGetImageFormatInfo(requestedFormat, out imageFormat, out var _, out requiredExtension))
                {
                    errorMessage = null;
                    return true;
                }

                imageFormat = null;
                requiredExtension = null;
                errorMessage = "Invalid output format. Supported formats: png, jpg, bmp, gif, tiff.";
                return false;
            }

            string extension;
            try
            {
                extension = Path.GetExtension(outputPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                imageFormat = null;
                requiredExtension = null;
                errorMessage = $"Failed to inspect output path '{outputPath}': {ex.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(extension))
            {
                imageFormat = ImageFormat.Png;
                requiredExtension = ".png";
                errorMessage = null;
                return true;
            }

            if (TryGetImageFormatInfo(extension, out imageFormat, out var _, out requiredExtension))
            {
                errorMessage = null;
                return true;
            }

            imageFormat = null;
            requiredExtension = null;
            errorMessage = $"Unsupported file extension '{extension}'. Supported formats: .png, .jpg, .bmp, .gif, .tiff.";
            return false;
        }

        private static bool TryGetImageFormatInfo(string token, out ImageFormat imageFormat, out string canonicalFormat, out string extension)
        {
            imageFormat = null;
            canonicalFormat = null;
            extension = null;

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
                    return true;
                case "jpg":
                case "jpeg":
                    imageFormat = ImageFormat.Jpeg;
                    canonicalFormat = "jpg";
                    extension = ".jpg";
                    return true;
                case "bmp":
                    imageFormat = ImageFormat.Bmp;
                    canonicalFormat = "bmp";
                    extension = ".bmp";
                    return true;
                case "gif":
                    imageFormat = ImageFormat.Gif;
                    canonicalFormat = "gif";
                    extension = ".gif";
                    return true;
                case "tif":
                case "tiff":
                    imageFormat = ImageFormat.Tiff;
                    canonicalFormat = "tiff";
                    extension = ".tiff";
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TrySetClipboardImageWithRetry(Image screenshot, int maxAttempts, int delayMilliseconds, out string errorMessage)
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

        internal static void ApplyWatermark(Bitmap screenshot, WatermarkOptions watermark)
        {
            using var graphics = Graphics.FromImage(screenshot);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var font = new Font(FontFamily.GenericSansSerif, watermark.Size, FontStyle.Regular, GraphicsUnit.Point);
            using var brush = new SolidBrush(watermark.Color);
            graphics.DrawString(watermark.Text, font, brush, new PointF(watermark.X, watermark.Y));
        }

        internal static bool TryParseArguments(string[] args, out CliOptions options, out string errorMessage, out bool showHelp)
        {
            options = new CliOptions();
            errorMessage = null;
            showHelp = false;

            if (args == null || args.Length == 0)
            {
                errorMessage = "No arguments supplied.";
                return false;
            }

            bool regionProvided = false;
            bool fileProvided = false;
            bool clipboardProvided = false;
            bool formatProvided = false;
            bool watermarkTextProvided = false;
            bool watermarkPositionProvided = false;
            bool watermarkSizeProvided = false;
            bool watermarkColorProvided = false;

            string watermarkText = null;
            int watermarkX = 0;
            int watermarkY = 0;
            float watermarkSize = 24f;
            Color watermarkColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string normalizedArg = arg.ToLowerInvariant();

                switch (normalizedArg)
                {
                    case "--help":
                    case "-h":
                    case "/?":
                        showHelp = true;
                        return true;
                    case "--region":
                    case "-r":
                        if (!TryReadNextValue(args, ref i, arg, out var regionValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (!TryParseRegion(regionValue, out var region))
                        {
                            errorMessage = "Invalid region. Expected format: x,y,width,height with width/height > 0.";
                            return false;
                        }

                        options.Region = region;
                        regionProvided = true;
                        break;
                    case "--delay":
                    case "-d":
                        if (!TryReadNextValue(args, ref i, arg, out var delayValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (!double.TryParse(delayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var delaySeconds) || delaySeconds < 0)
                        {
                            errorMessage = "Invalid delay. Provide a non-negative number in seconds.";
                            return false;
                        }

                        options.DelaySeconds = delaySeconds;
                        break;
                    case "--clipboard":
                    case "-c":
                        clipboardProvided = true;
                        options.CopyToClipboard = true;
                        break;
                    case "--file":
                    case "-f":
                        if (!TryReadNextValue(args, ref i, arg, out var fileValue))
                        {
                            errorMessage = $"Missing value for {arg}.";
                            return false;
                        }

                        if (string.IsNullOrWhiteSpace(fileValue))
                        {
                            errorMessage = "File path cannot be empty.";
                            return false;
                        }

                        fileProvided = true;
                        options.SaveToFile = true;
                        options.FilePath = fileValue;
                        break;
                    case "--format":
                        if (!TryReadNextValue(args, ref i, arg, out var formatValue))
                        {
                            errorMessage = "Missing value for --format.";
                            return false;
                        }

                        if (!TryNormalizeOutputFormat(formatValue, out var normalizedFormat))
                        {
                            errorMessage = "Invalid output format. Supported formats: png, jpg, bmp, gif, tiff.";
                            return false;
                        }

                        formatProvided = true;
                        options.OutputFormat = normalizedFormat;
                        break;
                    case "--watermark-text":
                        if (!TryReadNextValue(args, ref i, arg, out var watermarkTextValue))
                        {
                            errorMessage = "Missing value for --watermark-text.";
                            return false;
                        }

                        if (string.IsNullOrEmpty(watermarkTextValue))
                        {
                            errorMessage = "--watermark-text cannot be empty.";
                            return false;
                        }

                        watermarkTextProvided = true;
                        watermarkText = watermarkTextValue;
                        break;
                    case "--watermark-pos":
                        if (!TryReadNextValue(args, ref i, arg, out var watermarkPosValue))
                        {
                            errorMessage = "Missing value for --watermark-pos.";
                            return false;
                        }

                        if (!TryParsePosition(watermarkPosValue, out watermarkX, out watermarkY))
                        {
                            errorMessage = "Invalid watermark position. Expected format: x,y.";
                            return false;
                        }

                        watermarkPositionProvided = true;
                        break;
                    case "--watermark-size":
                        if (!TryReadNextValue(args, ref i, arg, out var watermarkSizeValue))
                        {
                            errorMessage = "Missing value for --watermark-size.";
                            return false;
                        }

                        if (!float.TryParse(watermarkSizeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out watermarkSize) || watermarkSize <= 0)
                        {
                            errorMessage = "Invalid watermark size. Provide a number greater than 0.";
                            return false;
                        }

                        watermarkSizeProvided = true;
                        break;
                    case "--watermark-color":
                        if (!TryReadNextValue(args, ref i, arg, out var watermarkColorValue))
                        {
                            errorMessage = "Missing value for --watermark-color.";
                            return false;
                        }

                        if (!TryParseColor(watermarkColorValue, out watermarkColor))
                        {
                            errorMessage = "Invalid watermark color. Use #RRGGBB, #AARRGGBB, or a known color name.";
                            return false;
                        }

                        watermarkColorProvided = true;
                        break;
                    default:
                        errorMessage = $"Unknown argument: {arg}";
                        return false;
                }
            }

            if (!regionProvided)
            {
                errorMessage = "Missing required --region option.";
                return false;
            }

            if (!fileProvided && !clipboardProvided)
            {
                errorMessage = "Specify at least one output target: --clipboard and/or --file <path>.";
                return false;
            }

            if (formatProvided && !fileProvided)
            {
                errorMessage = "--format requires --file <path>.";
                return false;
            }

            if (!watermarkTextProvided && (watermarkPositionProvided || watermarkSizeProvided || watermarkColorProvided))
            {
                errorMessage = "Watermark position/size/color requires --watermark-text.";
                return false;
            }

            if (watermarkTextProvided && !watermarkPositionProvided)
            {
                errorMessage = "Missing --watermark-pos for watermark text.";
                return false;
            }

            if (watermarkTextProvided)
            {
                options.Watermark = new WatermarkOptions
                {
                    Text = watermarkText,
                    X = watermarkX,
                    Y = watermarkY,
                    Size = watermarkSize,
                    Color = watermarkColor
                };
            }

            return true;
        }

        private static bool TryReadNextValue(string[] args, ref int currentIndex, string option, out string value)
        {
            int nextIndex = currentIndex + 1;
            if (nextIndex >= args.Length)
            {
                value = null;
                return false;
            }

            string nextValue = args[nextIndex];
            if (IsOptionToken(nextValue))
            {
                value = null;
                return false;
            }

            value = nextValue;
            currentIndex = nextIndex;
            return true;
        }

        private static bool IsOptionToken(string value)
        {
            switch (value)
            {
                case "--help":
                case "-h":
                case "/?":
                case "--region":
                case "-r":
                case "--delay":
                case "-d":
                case "--clipboard":
                case "-c":
                case "--file":
                case "-f":
                case "--format":
                case "--watermark-text":
                case "--watermark-pos":
                case "--watermark-size":
                case "--watermark-color":
                    return true;
                default:
                    return false;
            }
        }

        internal static bool TryParseRegion(string value, out Rectangle region)
        {
            region = Rectangle.Empty;
            var parts = value.Split(',');
            if (parts.Length != 4)
            {
                return false;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
                !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                return false;
            }

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            region = new Rectangle(x, y, width, height);
            return true;
        }

        internal static bool TryParsePosition(string value, out int x, out int y)
        {
            x = 0;
            y = 0;
            var parts = value.Split(',');
            if (parts.Length != 2)
            {
                return false;
            }

            return int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) &&
                   int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
        }

        internal static bool TryParseColor(string value, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                if (trimmed.Length == 7)
                {
                    if (!TryParseHexByte(trimmed.Substring(1, 2), out var r) ||
                        !TryParseHexByte(trimmed.Substring(3, 2), out var g) ||
                        !TryParseHexByte(trimmed.Substring(5, 2), out var b))
                    {
                        return false;
                    }

                    color = Color.FromArgb(255, r, g, b);
                    return true;
                }

                if (trimmed.Length == 9)
                {
                    if (!TryParseHexByte(trimmed.Substring(1, 2), out var a) ||
                        !TryParseHexByte(trimmed.Substring(3, 2), out var r) ||
                        !TryParseHexByte(trimmed.Substring(5, 2), out var g) ||
                        !TryParseHexByte(trimmed.Substring(7, 2), out var b))
                    {
                        return false;
                    }

                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }

                return false;
            }

            if (!Enum.TryParse(trimmed, true, out KnownColor knownColor))
            {
                return false;
            }

            color = Color.FromKnownColor(knownColor);
            return true;
        }

        private static bool TryParseHexByte(string value, out byte byteValue)
        {
            return byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byteValue);
        }

        private static void WriteUsage()
        {
            Console.WriteLine("ScreenShotNet usage:");
            Console.WriteLine();
            Console.WriteLine("  ScreenShotNet --region x,y,width,height [--delay seconds] [--clipboard] [--file fullPath] [--format format]");
            Console.WriteLine("               [--watermark-text text --watermark-pos x,y --watermark-size size --watermark-color color]");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  -r, --region           Required. Capture rectangle as x,y,width,height.");
            Console.WriteLine("  -d, --delay            Optional delay in seconds (default: 0).");
            Console.WriteLine("  -c, --clipboard        Output screenshot to clipboard.");
            Console.WriteLine("  -f, --file             Output screenshot to file path (defaults to .png if missing extension).");
            Console.WriteLine("      --format           File format: png, jpg, bmp, gif, tiff (requires --file).");
            Console.WriteLine("      --watermark-text   Optional watermark text.");
            Console.WriteLine("      --watermark-pos    Required with watermark text, format x,y.");
            Console.WriteLine("      --watermark-size   Optional watermark font size in points (default: 24).");
            Console.WriteLine("      --watermark-color  Optional watermark color (#RRGGBB, #AARRGGBB, or known color name; default: #80FFFFFF).");
            Console.WriteLine("  -h, --help             Show this help.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  ScreenShotNet --region 0,0,400,300 --clipboard");
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --delay 1.5 --file .\\out\\capture.png");
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --file .\\out\\capture --format jpg");
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --clipboard --file .\\out\\capture.png");
            Console.WriteLine("  ScreenShotNet --region 50,50,500,300 --watermark-text \"Draft\" --watermark-pos 12,24 --watermark-size 18 --watermark-color \"#80FF0000\" --file .\\out\\capture-watermark.png");
        }

        internal sealed class CliOptions
        {
            public Rectangle Region { get; set; }
            public double DelaySeconds { get; set; }
            public bool CopyToClipboard { get; set; }
            public bool SaveToFile { get; set; }
            public string FilePath { get; set; }
            public string OutputFormat { get; set; }
            public WatermarkOptions Watermark { get; set; }
        }

        internal sealed class WatermarkOptions
        {
            public string Text { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public float Size { get; set; }
            public Color Color { get; set; }
        }

    }
}
