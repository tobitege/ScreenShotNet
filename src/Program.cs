using System;
using System.Drawing;
using System.Globalization;

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

            if (!CliArgumentParser.TryParseArguments(args, out var options, out var errorMessage, out var showHelp))
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
                screenshot = ScreenshotOperations.CaptureScreenshot(options.Region, options.DelaySeconds, options.WindowTitle);
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
                        ScreenshotOperations.ApplyWatermark(screenshot, options.Watermark);
                    }

                    if (options.CopyToClipboard)
                    {
                        if (!ScreenshotOperations.TrySetClipboardImageWithRetry(screenshot, maxAttempts: 8, delayMilliseconds: 75, out var clipboardError))
                        {
                            Console.Error.WriteLine(clipboardError);
                            return RuntimeExitCode;
                        }

                        Console.WriteLine("Screenshot copied to clipboard.");
                    }

                    if (options.SaveToFile)
                    {
                        if (!ScreenshotOperations.TrySaveScreenshotToFile(screenshot, options.FilePath, options.OutputFormat, out var targetPath, out var fileError))
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

        private static void WriteUsage()
        {
            Console.WriteLine("ScreenShotNet usage:");
            Console.WriteLine();
            Console.WriteLine("  ScreenShotNet --region x,y,width,height [--delay seconds] [--window-title titlePrefix] [--clipboard] [--file fullPath] [--format format]");
            Console.WriteLine("               [--watermark-text text --watermark-pos x,y --watermark-size size --watermark-color color]");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  -r, --region           Required. Capture rectangle as x,y,width,height.");
            Console.WriteLine("  -d, --delay            Optional delay in seconds (default: 0).");
            Console.WriteLine("      --window-title     Optional window title prefix to restore and bring to the foreground before capture.");
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
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --window-title \"Visual Studio\" --file .\\out\\capture.png");
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --file .\\out\\capture --format jpg");
            Console.WriteLine("  ScreenShotNet --region 100,100,640,480 --clipboard --file .\\out\\capture.png");
            Console.WriteLine("  ScreenShotNet --region 50,50,500,300 --watermark-text \"Draft\" --watermark-pos 12,24 --watermark-size 18 --watermark-color \"#80FF0000\" --file .\\out\\capture-watermark.png");
        }
    }
}
