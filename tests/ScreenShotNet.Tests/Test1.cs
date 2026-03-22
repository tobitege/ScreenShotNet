using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenShotNet.Tests
{
    [TestClass]
    public class CoreTests
    {
        [TestMethod]
        public void TryParseArguments_Help_ReturnsShowHelp()
        {
            var success = CliArgumentParser.TryParseArguments(new[] { "--help" }, out var options, out var errorMessage, out var showHelp);

            Assert.IsTrue(success);
            Assert.IsTrue(showHelp);
            Assert.IsNull(errorMessage);
            Assert.IsNotNull(options);
        }

        [TestMethod]
        public void TryParseArguments_ValidClipboardArguments_ParsesSuccessfully()
        {
            var success = CliArgumentParser.TryParseArguments(new[] { "--region", "0,0,400,300", "--clipboard" }, out var options, out var errorMessage, out var showHelp);

            Assert.IsTrue(success);
            Assert.IsFalse(showHelp);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(0, options.Region.X);
            Assert.AreEqual(0, options.Region.Y);
            Assert.AreEqual(400, options.Region.Width);
            Assert.AreEqual(300, options.Region.Height);
            Assert.IsTrue(options.CopyToClipboard);
            Assert.IsFalse(options.SaveToFile);
            Assert.IsNull(options.FilePath);
            Assert.IsNull(options.Watermark);
        }

        [TestMethod]
        public void TryParseArguments_ValidFileAndWatermarkArguments_ParsesSuccessfully()
        {
            var args = new[]
            {
                "--region", "-10,20,640,480",
                "--delay", "1.5",
                "--window-title", "Visual Studio",
                "--file", ".\\out\\capture",
                "--watermark-text", "Draft",
                "--watermark-pos", "12,24",
                "--watermark-size", "18",
                "--watermark-color", "#80FF0000"
            };

            var success = CliArgumentParser.TryParseArguments(args, out var options, out var errorMessage, out var showHelp);

            Assert.IsTrue(success);
            Assert.IsFalse(showHelp);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(-10, options.Region.X);
            Assert.AreEqual(20, options.Region.Y);
            Assert.AreEqual(640, options.Region.Width);
            Assert.AreEqual(480, options.Region.Height);
            Assert.AreEqual(1.5d, options.DelaySeconds, 0.0001d);
            Assert.AreEqual("Visual Studio", options.WindowTitle);
            Assert.IsFalse(options.CopyToClipboard);
            Assert.IsTrue(options.SaveToFile);
            Assert.AreEqual(".\\out\\capture", options.FilePath);
            Assert.IsNull(options.OutputFormat);
            Assert.IsNotNull(options.Watermark);
            Assert.AreEqual("Draft", options.Watermark.Text);
            Assert.AreEqual(12, options.Watermark.X);
            Assert.AreEqual(24, options.Watermark.Y);
            Assert.AreEqual(18f, options.Watermark.Size, 0.001f);
            Assert.AreEqual(Color.FromArgb(0x80, 0xFF, 0x00, 0x00), options.Watermark.Color);
        }

        [TestMethod]
        public void TryParseArguments_InvalidArguments_ReturnsFailure()
        {
            var invalidArgumentSets = new[]
            {
                null,
                new string[0],
                new[] { "--clipboard" },
                new[] { "--region", "10,10,200", "--clipboard" },
                new[] { "--region", "10,10,0,200", "--clipboard" },
                new[] { "--region", "10,10,200,200" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--watermark-size", "10" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--watermark-text", "Draft" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--watermark-text", "Draft", "--watermark-pos", "10" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--watermark-text", "Draft", "--watermark-pos", "10,20", "--watermark-color", "nope" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--format", "jpg" },
                new[] { "--region", "10,10,200,200", "--file", ".\\out\\capture", "--format", "webp" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--window-title" },
                new[] { "--region", "10,10,200,200", "--clipboard", "--window-title", "" },
                new[] { "--region", "10,10,200,200", "--unknown", "value", "--clipboard" }
            };

            foreach (var args in invalidArgumentSets)
            {
                var success = CliArgumentParser.TryParseArguments(args, out var options, out var errorMessage, out var showHelp);

                Assert.IsFalse(success);
                Assert.IsFalse(showHelp);
                Assert.IsNotNull(options);
                Assert.IsFalse(string.IsNullOrEmpty(errorMessage));
            }
        }

        [TestMethod]
        public void TryParseArguments_ValidClipboardAndFileArguments_ParsesSuccessfully()
        {
            var success = CliArgumentParser.TryParseArguments(
                new[] { "--region", "10,20,640,480", "--clipboard", "--file", ".\\out\\capture.png", "--format", "jpeg" },
                out var options,
                out var errorMessage,
                out var showHelp);

            Assert.IsTrue(success);
            Assert.IsFalse(showHelp);
            Assert.IsNull(errorMessage);
            Assert.IsTrue(options.CopyToClipboard);
            Assert.IsTrue(options.SaveToFile);
            Assert.AreEqual(".\\out\\capture.png", options.FilePath);
            Assert.AreEqual("jpg", options.OutputFormat);
        }

        [TestMethod]
        public void TryParseArguments_ValuesWithSpaces_ParsesSuccessfully()
        {
            var success = CliArgumentParser.TryParseArguments(
                new[]
                {
                    "--region", "10,20,640,480",
                    "--window-title", "Visual Studio",
                    "--file", "D:\\temp\\my capture.jpg",
                    "--format", "jpg",
                    "--watermark-text", "Draft Build",
                    "--watermark-pos", "12,24"
                },
                out var options,
                out var errorMessage,
                out var showHelp);

            Assert.IsTrue(success);
            Assert.IsFalse(showHelp);
            Assert.IsNull(errorMessage);
            Assert.AreEqual("Visual Studio", options.WindowTitle);
            Assert.IsTrue(options.SaveToFile);
            Assert.AreEqual("D:\\temp\\my capture.jpg", options.FilePath);
            Assert.AreEqual("jpg", options.OutputFormat);
            Assert.IsNotNull(options.Watermark);
            Assert.AreEqual("Draft Build", options.Watermark.Text);
        }

        [TestMethod]
        public void TryParseColor_ParsesSupportedFormats()
        {
            Assert.IsTrue(CliArgumentParser.TryParseColor("#112233", out var rgbColor));
            Assert.AreEqual(Color.FromArgb(255, 0x11, 0x22, 0x33), rgbColor);

            Assert.IsTrue(CliArgumentParser.TryParseColor("#80112233", out var argbColor));
            Assert.AreEqual(Color.FromArgb(0x80, 0x11, 0x22, 0x33), argbColor);

            Assert.IsTrue(CliArgumentParser.TryParseColor("Red", out var namedColor));
            Assert.AreEqual(Color.Red.ToArgb(), namedColor.ToArgb());
        }

        [TestMethod]
        public void TryParseRegion_AllowsNegativeCoordinates()
        {
            var success = CliArgumentParser.TryParseRegion("-100,-50,320,240", out var region);

            Assert.IsTrue(success);
            Assert.AreEqual(-100, region.X);
            Assert.AreEqual(-50, region.Y);
            Assert.AreEqual(320, region.Width);
            Assert.AreEqual(240, region.Height);
        }

        [TestMethod]
        public void PrepareOutputPath_AddsPngExtensionAndCreatesDirectory()
        {
            var baseDirectory = Path.Combine(Path.GetTempPath(), "ScreenShotNetTests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            var rawPath = Path.Combine(baseDirectory, "nested", "capture");

            try
            {
                var preparedPath = ScreenshotOperations.PrepareOutputPath(rawPath);

                Assert.IsTrue(preparedPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(preparedPath)));
            }
            finally
            {
                if (Directory.Exists(baseDirectory))
                {
                    Directory.Delete(baseDirectory, true);
                }
            }
        }

        [TestMethod]
        public void PrepareOutputPath_ReplacesExtensionWhenFormatRequested()
        {
            var preparedPath = ScreenshotOperations.PrepareOutputPath(".\\out\\capture.png", ".jpg", true);

            Assert.IsTrue(preparedPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
        }

        [TestMethod]
        public void TryResolveOutputFormat_InfersFromExtensionAndHandlesInvalidExtension()
        {
            var jpgResolved = ScreenshotOperations.TryResolveOutputFormat(".\\out\\capture.jpeg", null, out var jpgFormat, out var jpgExtension, out var jpgError);

            Assert.IsTrue(jpgResolved);
            Assert.IsNotNull(jpgFormat);
            Assert.AreEqual(".jpg", jpgExtension);
            Assert.IsNull(jpgError);

            var invalidResolved = ScreenshotOperations.TryResolveOutputFormat(".\\out\\capture.webp", null, out var invalidFormat, out var invalidExtension, out var invalidError);

            Assert.IsFalse(invalidResolved);
            Assert.IsNull(invalidFormat);
            Assert.IsNull(invalidExtension);
            Assert.IsFalse(string.IsNullOrWhiteSpace(invalidError));
            StringAssert.Contains(invalidError, "Unsupported file extension");
        }

        [TestMethod]
        public void TrySaveScreenshotToFile_InvalidPath_ReturnsFailure()
        {
            using var bitmap = new Bitmap(4, 4);
            var invalidPath = "capture" + '\0' + ".png";

            var success = ScreenshotOperations.TrySaveScreenshotToFile(bitmap, invalidPath, out var savedPath, out var errorMessage);

            Assert.IsFalse(success);
            Assert.IsNull(savedPath);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
            StringAssert.Contains(errorMessage, "Failed to inspect output path");
        }

        [TestMethod]
        public void TrySetClipboardImageWithRetry_DisposedImage_ReturnsFailure()
        {
            var bitmap = new Bitmap(4, 4);
            bitmap.Dispose();

            var success = ScreenshotOperations.TrySetClipboardImageWithRetry(bitmap, maxAttempts: 1, delayMilliseconds: 0, out var errorMessage);

            Assert.IsFalse(success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errorMessage));
        }

        [TestMethod]
        public void ApplyWatermark_DrawsTextOntoBitmap()
        {
            using var bitmap = new Bitmap(200, 60);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
            }

            var watermark = new WatermarkOptions
            {
                Text = "Draft",
                X = 0,
                Y = 0,
                Size = 24f,
                Color = Color.White
            };

            ScreenshotOperations.ApplyWatermark(bitmap, watermark);

            var hasNonBlackPixel = false;
            for (var y = 0; y < bitmap.Height && !hasNonBlackPixel; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != Color.Black.ToArgb())
                    {
                        hasNonBlackPixel = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(hasNonBlackPixel);
        }

        [TestMethod]
        public void ApplyCursorReticle_DrawsRedReticleWhenCursorIsInsideCapture()
        {
            using var bitmap = new Bitmap(40, 40);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
            }

            var applied = ScreenshotOperations.ApplyCursorReticle(
                bitmap,
                new Rectangle(100, 200, 40, 40),
                new Point(120, 220));

            var hasRedDominantPixel = false;
            for (var y = 0; y < bitmap.Height && !hasRedDominantPixel; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.ToArgb() != Color.Black.ToArgb() && pixel.R > pixel.G && pixel.R > pixel.B)
                    {
                        hasRedDominantPixel = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(applied);
            Assert.IsTrue(hasRedDominantPixel);
        }

        [TestMethod]
        public void ApplyCursorReticle_ReturnsFalseWhenCursorIsOutsideCapture()
        {
            using var bitmap = new Bitmap(40, 40);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
            }

            var applied = ScreenshotOperations.ApplyCursorReticle(
                bitmap,
                new Rectangle(100, 200, 40, 40),
                new Point(10, 20));

            Assert.IsFalse(applied);
            Assert.AreEqual(Color.Black.ToArgb(), bitmap.GetPixel(20, 20).ToArgb());
        }
    }
}
