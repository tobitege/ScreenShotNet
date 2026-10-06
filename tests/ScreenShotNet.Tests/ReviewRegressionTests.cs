using System;
using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenShotNet.Tests
{
    [TestClass]
    public class ReviewRegressionTests
    {
        [TestMethod]
        public void OptionValuesRecognizeMixedCaseOptionNames()
        {
            foreach (var option in new[] { "--CLIPBOARD", "-C", "--FoRmAt", "--HeLp" })
            {
                var success = CliArgumentParser.TryParseArguments(
                    new[] { "--region", "0,0,2,2", "--file", option },
                    out _, out var error, out _);
                Assert.IsFalse(success, option);
                StringAssert.Contains(error, "Missing value");
            }

            Assert.IsTrue(CliArgumentParser.TryParseArguments(
                new[] { "--REGION", "-10,-20,2,2", "--FILE", "Capture.PNG", "--CLIPBOARD" },
                out var options, out _, out _));
            Assert.AreEqual("Capture.PNG", options.FilePath);
            Assert.IsTrue(options.CopyToClipboard);
        }

        [TestMethod]
        [DoNotParallelize]
        public void RelativeCaptureUsesWindowPositionAfterDelay()
        {
            RunOnApartment(ApartmentState.STA, () =>
            {
                var area = Screen.PrimaryScreen!.WorkingArea;
                using var window = new Form
                {
                    Text = "ScreenShotNet moving window " + Guid.NewGuid().ToString("N"),
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(area.Left + 20, area.Top + 20),
                    Size = new Size(160, 100),
                    ShowInTaskbar = false
                };
                window.Show();
                var windowTitle = window.Text;
                var match = new WindowMatch { Handle = window.Handle, Title = windowTitle };
                Assert.IsTrue(WindowActivationService.TryGetWindowBounds(match, out var initialBounds, out var error), error);

                var moved = false;
                using var timer = new System.Windows.Forms.Timer { Interval = 500 };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    window.Location = new Point(window.Left + 120, window.Top + 60);
                    moved = true;
                };
                var capture = Task.Run(() =>
                {
                    using var image = ScreenshotOperations.CaptureScreenshot(
                        new Rectangle(10, 10, 2, 2), 1.0, windowTitle, true, out var capturedRegion, out _);
                    return capturedRegion;
                });
                timer.Start();
                var deadline = DateTime.UtcNow.AddSeconds(8);
                while (!capture.IsCompleted && DateTime.UtcNow < deadline)
                {
                    Application.DoEvents();
                    Thread.Sleep(5);
                }

                Assert.IsTrue(capture.IsCompleted, "Delayed capture did not finish.");
                var captured = capture.GetAwaiter().GetResult();
                Assert.IsTrue(moved, "The test window must move during the capture delay.");
                Assert.IsTrue(WindowActivationService.TryGetWindowBounds(match, out var movedBounds, out error), error);
                Assert.AreNotEqual(initialBounds.Location, movedBounds.Location);
                Assert.AreEqual(new Point(movedBounds.Left + 10, movedBounds.Top + 10), captured.Location);
            });
        }

        [TestMethod]
        public void CliRejectsNonFiniteAndUnsupportedDelays()
        {
            foreach (var value in new[] { "NaN", "Infinity", "-Infinity", "2147484" })
            {
                var success = CliArgumentParser.TryParseArguments(
                    new[] { "--region", "0,0,4,4", "--clipboard", "--delay", value },
                    out _, out var error, out _);

                Assert.IsFalse(success, value);
                Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            }
        }

        [TestMethod]
        public void CliRejectsNonFiniteWatermarkSizes()
        {
            foreach (var value in new[] { "NaN", "Infinity", "-Infinity" })
            {
                var success = CliArgumentParser.TryParseArguments(
                    new[] { "--region", "0,0,4,4", "--clipboard", "--watermark-text", "test", "--watermark-pos", "0,0", "--watermark-size", value },
                    out _, out var error, out _);

                Assert.IsFalse(success, value);
                Assert.IsFalse(string.IsNullOrWhiteSpace(error));
            }
        }

        [TestMethod]
        public void CapturesRejectInvalidDelaysBeforeWindowLookup()
        {
            var missingTitle = Guid.NewGuid().ToString("N");
            foreach (var delay in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, 2147484d })
            {
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                    ScreenshotOperations.CaptureScreenshot(new Rectangle(0, 0, 4, 4), delay, missingTitle));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                    ScreenshotOperations.CaptureWindowScreenshot(missingTitle, delay, out _));
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                    ScreenshotOperations.CaptureCenteredWindowScreenshot(missingTitle, 4, 4, delay, out _, out _));
            }
        }

        [TestMethod]
        public void OversizedCapturesAreRejectedBeforeWindowLookup()
        {
            var missingTitle = Guid.NewGuid().ToString("N");
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenshotOperations.CaptureScreenshot(new Rectangle(0, 0, 10000, 10000), 0, missingTitle));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenshotOperations.CaptureCenteredWindowScreenshot(missingTitle, 10000, 10000, 0, out _, out _));
        }

        [TestMethod]
        public void CaptureRejectsCoordinateOverflow()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenCaptureService.CaptureRegion(new Rectangle(int.MaxValue, 0, 2, 2)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenCaptureService.CaptureRegion(new Rectangle(0, int.MaxValue, 2, 2)));
        }

        [TestMethod]
        public void CapturedBitmapRemainsUsableAfterNativeHandlesAreReleased()
        {
            using var image = ScreenCaptureService.CaptureRegion(new Rectangle(0, 0, 2, 2));
            Assert.AreEqual(2, image.Width);
            Assert.AreEqual(2, image.Height);
            Assert.AreEqual(255, image.GetPixel(0, 0).A);
            using var stream = new System.IO.MemoryStream();
            image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            Assert.IsTrue(stream.Length > 0);
            stream.Position = 0;
            using var decoded = new Bitmap(stream);
            Assert.AreEqual(255, decoded.GetPixel(0, 0).A);
        }

        [TestMethod]
        public void RelativeCoordinatesRejectOverflowAndRetainNegativeScreenCoordinates()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenshotOperations.ResolveRelativeRegion(new Rectangle(int.MaxValue - 10, 0, 2, 2), new Rectangle(20, 0, 40, 40)));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
                ScreenshotOperations.ResolveRelativeRegion(new Rectangle(0, int.MinValue, 2, 2), new Rectangle(0, -20, 40, 40)));

            var resolved = ScreenshotOperations.ResolveRelativeRegion(new Rectangle(10, 20, 4, 4), new Rectangle(-100, -200, 40, 40));
            Assert.AreEqual(new Rectangle(-90, -180, 4, 4), resolved);
            Assert.IsTrue(ScreenCaptureService.IsValidRegion(new Rectangle(-100, -100, 8000, 8000)));
            Assert.IsFalse(ScreenCaptureService.IsValidRegion(new Rectangle(0, 0, 8001, 8000)));
        }

        [TestMethod]
        public void WatermarkRejectsNonFiniteSizeBeforeDrawing()
        {
            using var image = new Bitmap(4, 4);
            foreach (var size in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var watermark = new WatermarkOptions { Text = "test", Size = size };
                Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ScreenshotOperations.ApplyWatermark(image, watermark));
            }
        }

        [TestMethod]
        public void ClipboardOperationFromMtaUsesStaAndInitializesOle()
        {
            RunOnApartment(ApartmentState.MTA, () =>
            {
                var oleApartment = ApartmentState.Unknown;
                var success = ScreenshotOperations.TrySetClipboardWithRetry(
                    () => oleApartment = Application.OleRequired(), 1, 0, out var error);

                Assert.IsTrue(success, error);
                Assert.IsNull(error);
                Assert.AreEqual(ApartmentState.STA, oleApartment);
                Assert.AreEqual(ApartmentState.MTA, Thread.CurrentThread.GetApartmentState());
            });
        }

        [TestMethod]
        public void ClipboardRetriesOnTheSameStaThread()
        {
            RunOnApartment(ApartmentState.MTA, () =>
            {
                var attempts = 0;
                var workerId = 0;
                var success = ScreenshotOperations.TrySetClipboardWithRetry(() =>
                {
                    Assert.AreEqual(ApartmentState.STA, Application.OleRequired());
                    if (workerId == 0)
                    {
                        workerId = Thread.CurrentThread.ManagedThreadId;
                    }

                    Assert.AreEqual(workerId, Thread.CurrentThread.ManagedThreadId);
                    if (++attempts < 3)
                    {
                        throw new ExternalException("Clipboard is busy.");
                    }
                }, 3, 0, out var error);

                Assert.IsTrue(success, error);
                Assert.IsNull(error);
                Assert.AreEqual(3, attempts);
            });
        }

        [TestMethod]
        public void ClipboardWorkerPropagatesUnexpectedFailuresToCaller()
        {
            RunOnApartment(ApartmentState.MTA, () =>
            {
                var failure = new NotSupportedException("Unsupported clipboard operation.");
                var caught = Assert.ThrowsExactly<NotSupportedException>(() =>
                    ScreenshotOperations.TrySetClipboardWithRetry(() => throw failure, 1, 0, out _));
                Assert.AreSame(failure, caught);
            });
        }

        [TestMethod]
        [DoNotParallelize]
        public void WindowActivationPreservesTopmostStatus()
        {
            RunOnApartment(ApartmentState.STA, () =>
            {
                using var window = new Form
                {
                    Text = "ScreenShotNet regression test",
                    TopMost = true,
                    ShowInTaskbar = false,
                    Size = new Size(80, 60)
                };
                window.Show();
                window.TopMost = false;
                window.TopMost = true;
                var match = new WindowMatch { Handle = window.Handle, Title = window.Text };
                Assert.AreNotEqual(0, GetWindowLong(window.Handle, -20) & 8, "The test window must start with WS_EX_TOPMOST.");

                WindowActivationService.TryBringWindowToForeground(match, out _);

                Assert.AreNotEqual(0, GetWindowLong(window.Handle, -20) & 8,
                    "Activating a window must preserve WS_EX_TOPMOST.");
            });
        }

        private static void RunOnApartment(ApartmentState apartment, Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }) { IsBackground = true };
            thread.SetApartmentState(apartment);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "Test thread did not complete.");
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);
    }
}
