using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ScreenShotNet
{
    internal static class WindowActivationService
    {
        private const int DwmwaExtendedFrameBounds = 9;
        private const int DwmwaVisibleFrameBorderThickness = 37;
        private const int SwShow = 5;
        private const int SwRestore = 9;
        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const uint SwpShowwindow = 0x0040;
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotopmost = new IntPtr(-2);

        public static bool TryBringWindowToForeground(string titlePrefix, out string matchedTitle, out string errorMessage)
        {
            matchedTitle = null;
            if (!TryFindWindowByTitlePrefix(titlePrefix, out var windowMatch, out errorMessage))
            {
                return false;
            }

            matchedTitle = windowMatch.Title;
            return TryBringWindowToForeground(windowMatch, out errorMessage);
        }

        public static bool TryFindWindowByTitlePrefix(string titlePrefix, out WindowMatch windowMatch, out string errorMessage)
        {
            windowMatch = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(titlePrefix))
            {
                errorMessage = "Window title prefix cannot be empty.";
                return false;
            }

            IntPtr matchedHandle = IntPtr.Zero;
            string matchedTitle = null;

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
                    matchedTitle = windowTitle;
                    return false;
                },
                IntPtr.Zero);

            if (matchedHandle == IntPtr.Zero)
            {
                errorMessage = string.Format("No visible top-level window title starts with '{0}'.", titlePrefix);
                return false;
            }

            windowMatch = new WindowMatch
            {
                Handle = matchedHandle,
                Title = matchedTitle
            };
            return true;
        }

        public static bool TryBringWindowToForeground(WindowMatch windowMatch, out string errorMessage)
        {
            errorMessage = null;

            if (windowMatch == null || windowMatch.Handle == IntPtr.Zero)
            {
                errorMessage = "Window handle is invalid.";
                return false;
            }

            var targetWindow = windowMatch.Handle;
            var foregroundWindow = GetForegroundWindow();
            var currentThreadId = GetCurrentThreadId();
            uint ignoredProcessId;
            var foregroundThreadId = foregroundWindow != IntPtr.Zero ? GetWindowThreadProcessId(foregroundWindow, out ignoredProcessId) : 0u;
            var targetThreadId = GetWindowThreadProcessId(targetWindow, out ignoredProcessId);
            var attachedToForeground = false;
            var attachedToTarget = false;

            try
            {
                if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
                {
                    attachedToForeground = AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                if (targetThreadId != 0 && targetThreadId != currentThreadId)
                {
                    attachedToTarget = AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                if (IsIconic(targetWindow))
                {
                    ShowWindowAsync(targetWindow, SwRestore);
                }
                else
                {
                    ShowWindowAsync(targetWindow, SwShow);
                }

                SetWindowPos(targetWindow, HwndTopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);
                SetWindowPos(targetWindow, HwndNotopmost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);

                for (var attempt = 0; attempt < 10; attempt++)
                {
                    BringWindowToTop(targetWindow);
                    SetForegroundWindow(targetWindow);

                    if (GetForegroundWindow() == targetWindow)
                    {
                        Thread.Sleep(150);
                        return true;
                    }

                    Thread.Sleep(50);
                }
            }
            finally
            {
                if (attachedToTarget)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                if (attachedToForeground)
                {
                    AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }

            errorMessage = string.Format("Window '{0}' was found, but Windows blocked foreground activation.", windowMatch.Title);
            return false;
        }

        public static bool TryGetWindowBounds(WindowMatch windowMatch, out Rectangle bounds, out string errorMessage)
        {
            bounds = Rectangle.Empty;
            errorMessage = null;

            if (windowMatch == null || windowMatch.Handle == IntPtr.Zero)
            {
                errorMessage = "Window handle is invalid.";
                return false;
            }

            if (TryGetExtendedFrameBounds(windowMatch, out bounds, out errorMessage))
            {
                TrimVisibleFrameBorder(windowMatch.Handle, ref bounds);
                return true;
            }

            return TryGetRawWindowBounds(windowMatch, out bounds, out errorMessage);
        }

        public static bool TryGetExtendedFrameBounds(WindowMatch windowMatch, out Rectangle bounds, out string errorMessage)
        {
            bounds = Rectangle.Empty;
            errorMessage = null;

            if (windowMatch == null || windowMatch.Handle == IntPtr.Zero)
            {
                errorMessage = "Window handle is invalid.";
                return false;
            }

            if (!TryGetExtendedFrameBounds(windowMatch.Handle, out bounds))
            {
                errorMessage = string.Format("Failed to read extended frame bounds for window '{0}'.", windowMatch.Title);
                return false;
            }

            return true;
        }

        public static bool TryGetRawWindowBounds(WindowMatch windowMatch, out Rectangle bounds, out string errorMessage)
        {
            bounds = Rectangle.Empty;
            errorMessage = null;

            if (windowMatch == null || windowMatch.Handle == IntPtr.Zero)
            {
                errorMessage = "Window handle is invalid.";
                return false;
            }

            if (!GetWindowRect(windowMatch.Handle, out var rect))
            {
                errorMessage = string.Format("Failed to read raw bounds for window '{0}'.", windowMatch.Title);
                return false;
            }

            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                errorMessage = string.Format("Window '{0}' returned invalid bounds.", windowMatch.Title);
                return false;
            }

            return true;
        }

        private static bool TryGetExtendedFrameBounds(IntPtr hWnd, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (DwmGetWindowAttributeRect(hWnd, DwmwaExtendedFrameBounds, out var rect, Marshal.SizeOf(typeof(NativeRect))) != 0)
            {
                return false;
            }

            bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            return bounds.Width > 0 && bounds.Height > 0;
        }

        private static void TrimVisibleFrameBorder(IntPtr hWnd, ref Rectangle bounds)
        {
            if (DwmGetWindowAttributeInt(hWnd, DwmwaVisibleFrameBorderThickness, out var thickness, Marshal.SizeOf(typeof(int))) != 0)
            {
                return;
            }

            if (thickness <= 0)
            {
                return;
            }

            var trimmed = Rectangle.FromLTRB(
                bounds.Left + thickness,
                bounds.Top,
                bounds.Right - thickness,
                bounds.Bottom - thickness);

            if (trimmed.Width <= 0 || trimmed.Height <= 0)
            {
                return;
            }

            bounds = trimmed;
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
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmGetWindowAttributeRect(IntPtr hwnd, int dwAttribute, out NativeRect pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
        private static extern int DwmGetWindowAttributeInt(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
