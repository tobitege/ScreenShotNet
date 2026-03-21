using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ScreenShotNet
{
    internal static class WindowActivationService
    {
        private const int SwShow = 5;
        private const int SwRestore = 9;

        public static bool TryBringWindowToForeground(string titlePrefix, out string matchedTitle, out string errorMessage)
        {
            matchedTitle = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(titlePrefix))
            {
                errorMessage = "Window title prefix cannot be empty.";
                return false;
            }

            var targetWindow = FindWindowByTitlePrefix(titlePrefix, out matchedTitle);
            if (targetWindow == IntPtr.Zero)
            {
                errorMessage = string.Format("No visible top-level window title starts with '{0}'.", titlePrefix);
                return false;
            }

            if (IsIconic(targetWindow))
            {
                ShowWindow(targetWindow, SwRestore);
            }
            else
            {
                ShowWindow(targetWindow, SwShow);
            }

            BringWindowToTop(targetWindow);
            if (!SetForegroundWindow(targetWindow) && GetForegroundWindow() != targetWindow)
            {
                errorMessage = string.Format("Window '{0}' was found, but could not be brought to the foreground.", matchedTitle);
                return false;
            }

            Thread.Sleep(150);
            return true;
        }

        private static IntPtr FindWindowByTitlePrefix(string titlePrefix, out string matchedTitle)
        {
            IntPtr matchedHandle = IntPtr.Zero;
            string matchedWindowTitle = null;

            EnumWindows(
                delegate (IntPtr hWnd, IntPtr lParam)
                {
                    if (!IsWindowVisible(hWnd))
                    {
                        return true;
                    }

                    var windowTitle = GetWindowTitle(hWnd);
                    if (string.IsNullOrEmpty(windowTitle))
                    {
                        return true;
                    }

                    if (!windowTitle.StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    matchedHandle = hWnd;
                    matchedWindowTitle = windowTitle;
                    return false;
                },
                IntPtr.Zero);

            matchedTitle = matchedWindowTitle;
            return matchedHandle;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            var length = GetWindowTextLengthW(hWnd);
            if (length <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(length + 1);
            if (GetWindowTextW(hWnd, builder, builder.Capacity) <= 0)
            {
                return string.Empty;
            }

            return builder.ToString();
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLengthW(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
