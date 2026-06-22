using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;

namespace LiveCaptionsTranslator.utils
{
    public static class WindowHandler
    {
        public static Rect SaveState(Window? window, Setting? setting)
        {
            if (window == null || setting == null || !window.IsLoaded)
                return Rect.Empty;
            string windowName = window.GetType().Name;
            setting.WindowBounds[windowName] = Regex.Replace(
                window.RestoreBounds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                @"(\d+\.\d{1})\d+", "$1");
            setting.Save();
            return window.RestoreBounds;
        }

        public static Rect LoadState(Window? window, Setting? setting)
        {
            if (window == null || setting == null)
                return Rect.Empty;
            string windowName = window.GetType().Name;
            Rect bound = Rect.Parse(setting.WindowBounds[windowName]);
            return bound;
        }

        public static void RestoreState(Window? window, Rect bound)
        {
            if (window == null || bound.IsEmpty)
                return;
            window.Top = bound.Top;
            window.Left = bound.Left;

            // Restore the size only for a manually sized
            if (window.SizeToContent == SizeToContent.Manual)
            {
                window.Width = bound.Width;
                window.Height = bound.Height;
            }
        }

        public static Rect ValidateAndAdjustBounds(Rect savedBounds, Rect defaultBounds)
        {
            if (savedBounds.IsEmpty || savedBounds.Width <= 0 || savedBounds.Height <= 0)
            {
                return defaultBounds;
            }

            // Get primary monitor scale (in DIPs vs physical pixels)
            double dpiScaleX = 1.0;
            double dpiScaleY = 1.0;
            try
            {
                double primaryWidth = SystemParameters.PrimaryScreenWidth;
                double primaryHeight = SystemParameters.PrimaryScreenHeight;
                if (primaryWidth > 0 && primaryHeight > 0)
                {
                    int screenWidthPhysical = WindowsAPI.GetSystemMetrics(0);
                    int screenHeightPhysical = WindowsAPI.GetSystemMetrics(1);
                    if (screenWidthPhysical > 0 && screenHeightPhysical > 0)
                    {
                        dpiScaleX = screenWidthPhysical / primaryWidth;
                        dpiScaleY = screenHeightPhysical / primaryHeight;
                    }
                }
            }
            catch
            {
                // Fallback to 1.0
            }

            // Convert saved bounds (WPF DIPs) to physical pixels using primary monitor scale
            RECT physicalRect;
            physicalRect.Left = (int)Math.Round(savedBounds.Left * dpiScaleX);
            physicalRect.Top = (int)Math.Round(savedBounds.Top * dpiScaleY);
            physicalRect.Right = (int)Math.Round((savedBounds.Left + savedBounds.Width) * dpiScaleX);
            physicalRect.Bottom = (int)Math.Round((savedBounds.Top + savedBounds.Height) * dpiScaleY);

            // Get nearest monitor
            IntPtr hMonitor = WindowsAPI.MonitorFromRect(ref physicalRect, WindowsAPI.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero)
            {
                return defaultBounds;
            }

            // Get monitor info
            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!WindowsAPI.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return defaultBounds;
            }

            // Get target monitor DPI scale
            double targetDpiScaleX = dpiScaleX;
            double targetDpiScaleY = dpiScaleY;
            try
            {
                int result = WindowsAPI.GetDpiForMonitor(hMonitor, 0, out uint dpiX, out uint dpiY);
                if (result == 0) // S_OK
                {
                    targetDpiScaleX = dpiX / 96.0;
                    targetDpiScaleY = dpiY / 96.0;
                }
            }
            catch
            {
                // Fallback to primary DPI scale
            }

            // Calculate the window size in physical pixels on the target monitor
            double physicalWidth = savedBounds.Width * targetDpiScaleX;
            double physicalHeight = savedBounds.Height * targetDpiScaleY;

            // Target monitor work area in physical pixels
            RECT workArea = monitorInfo.rcWork;

            // Ensure window fits within work area size. If target monitor is smaller than window, clamp size
            double maxWorkWidth = workArea.Right - workArea.Left;
            double maxWorkHeight = workArea.Bottom - workArea.Top;
            if (physicalWidth > maxWorkWidth)
                physicalWidth = maxWorkWidth;
            if (physicalHeight > maxWorkHeight)
                physicalHeight = maxWorkHeight;

            double physicalLeft = savedBounds.Left * dpiScaleX;
            double physicalTop = savedBounds.Top * dpiScaleY;

            // Ensure window is visible on this monitor (clamp inside workArea)
            if (physicalLeft < workArea.Left)
                physicalLeft = workArea.Left;
            else if (physicalLeft + physicalWidth > workArea.Right)
                physicalLeft = workArea.Right - physicalWidth;

            if (physicalTop < workArea.Top)
                physicalTop = workArea.Top;
            else if (physicalTop + physicalHeight > workArea.Bottom)
                physicalTop = workArea.Bottom - physicalHeight;

            // Convert back to WPF DIPs
            double finalLeft = physicalLeft / dpiScaleX;
            double finalTop = physicalTop / dpiScaleY;
            double finalWidth = physicalWidth / targetDpiScaleX;
            double finalHeight = physicalHeight / targetDpiScaleY;

            return new Rect(finalLeft, finalTop, finalWidth, finalHeight);
        }
    }
}
