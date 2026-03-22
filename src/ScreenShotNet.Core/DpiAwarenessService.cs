using System;
using System.Runtime.InteropServices;

namespace ScreenShotNet
{
    public static class DpiAwarenessService
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static readonly IntPtr DpiAwarenessContextPerMonitorAwareV2 = new IntPtr(-4);

        public static void EnableBestAvailableDpiAwareness()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                if (!TryEnablePerMonitorAwareV2())
                {
                    if (!TryEnableShcorePerMonitorAware())
                    {
                        TryEnableSystemAware();
                    }
                }

                _initialized = true;
            }
        }

        private static bool TryEnablePerMonitorAwareV2()
        {
            try
            {
                return SetProcessDpiAwarenessContext(DpiAwarenessContextPerMonitorAwareV2);
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static bool TryEnableShcorePerMonitorAware()
        {
            try
            {
                var result = SetProcessDpiAwareness(ProcessDpiAwareness.ProcessPerMonitorDpiAware);
                return result == 0 || result == EAccessDenied;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                return false;
            }
        }

        private static void TryEnableSystemAware()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        private const int EAccessDenied = unchecked((int)0x80070005);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int SetProcessDpiAwareness(ProcessDpiAwareness value);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        private enum ProcessDpiAwareness
        {
            ProcessDpiUnaware = 0,
            ProcessSystemDpiAware = 1,
            ProcessPerMonitorDpiAware = 2
        }
    }
}
