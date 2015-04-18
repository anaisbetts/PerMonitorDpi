using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Drawing;

namespace PerMonitorDPI
{
    public static class MonitorDpi
    {
        static bool? isHighDpiMethodSupported = null;
        public static bool IsHighDpiMethodSupported()
        {
            if (isHighDpiMethodSupported != null) return isHighDpiMethodSupported.Value;

            isHighDpiMethodSupported = SafeNativeMethods.DoesWin32MethodExist("shcore.dll", "SetProcessDpiAwareness");

            return isHighDpiMethodSupported.Value;
        }

        public static double GetScaleRatioForWindow(Window This)
        {
            var wpfDpi = 96.0 * PresentationSource.FromVisual(Application.Current.MainWindow).CompositionTarget.TransformToDevice.M11;
            var hwndSource = PresentationSource.FromVisual(This) as HwndSource;

            if (IsHighDpiMethodSupported() == false)
            {
                return wpfDpi / 96.0;
            }
            else
            {
                var monitor = SafeNativeMethods.MonitorFromWindow(hwndSource.Handle, MonitorOpts.MONITOR_DEFAULTTONEAREST);

                uint dpiX; uint dpiY;
                SafeNativeMethods.GetDpiForMonitor(monitor, MonitorDpiType.MDT_EFFECTIVE_DPI, out dpiX, out dpiY);

                return ((double)dpiX) / wpfDpi;
            }
        }
    }

    public class PerMonitorDpiBehavior
    {    
        HwndSource hwndSource;
        IntPtr hwnd;
        double currentDpiRatio;

        Window AssociatedObject;

        static PerMonitorDpiBehavior()
        {
            if (MonitorDpi.IsHighDpiMethodSupported())
            {
                // NB: We need to call this early before we start doing any 
                // fiddling with window coordinates / geometry
                SafeNativeMethods.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
            }
        }

        public PerMonitorDpiBehavior(Window mainWindow)
        {
            AssociatedObject = mainWindow;
            mainWindow.Loaded += (o, e) => OnAttached();
            mainWindow.Closing += (o, e) => OnDetaching();
        }

        protected void OnAttached()
        {
            if (AssociatedObject.IsInitialized)
            {
                AddHwndHook();
            }
            else
            {
                AssociatedObject.SourceInitialized += AssociatedObject_SourceInitialized;
            }

            // NB: This allows us to drag-drop URLs from IE11, which would 
            // normally fail because we run at Medium integrity and most of
            // IE runs at Low or AppContainer level.
            EnableDragDropFromLowPrivUIPIProcesses();
        }

        protected void OnDetaching()
        {
            RemoveHwndHook();
        }

        void AddHwndHook()
        {
            hwndSource = PresentationSource.FromVisual(AssociatedObject) as HwndSource;
            hwndSource.AddHook(HwndHook);
            hwnd = new WindowInteropHelper(AssociatedObject).Handle;
        }

        void RemoveHwndHook()
        {
            AssociatedObject.SourceInitialized -= AssociatedObject_SourceInitialized;
            hwndSource.RemoveHook(HwndHook);
        }

        void AssociatedObject_SourceInitialized(object sender, EventArgs e)
        {
            AddHwndHook();

            currentDpiRatio = MonitorDpi.GetScaleRatioForWindow(AssociatedObject);
            UpdateDpiScaling(currentDpiRatio);
        }

        static void EnableDragDropFromLowPrivUIPIProcesses()
        {
            // UIPI was introduced on Vista
            if (Environment.OSVersion.Version.Major < 6) return;
            var msgs = new uint[] 
            {
                0x233,      // WM_DROPFILES
                0x48,       // WM_COPYDATA
                0x49,       // NOBODY KNOWS BUT EVERYONE SAYS TO DO IT
            };

            foreach (var msg in msgs) 
            {
                SafeNativeMethods.ChangeWindowMessageFilter(msg, ChangeWindowMessageFilterFlags.Add);
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "GitHub.Extensions.Windows.Native.UnsafeNativeMethods.DwmExtendFrameIntoClientArea(System.IntPtr,GitHub.Extensions.Windows.Native.MARGINS@)")]
        IntPtr HwndHook(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (message)
            {
                case NativeConstants.WM_DPICHANGED:
                    var rect = (RECT)Marshal.PtrToStructure(lParam, typeof(RECT));

                    SafeNativeMethods.SetWindowPos(hWnd, IntPtr.Zero,
                        rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top,
                        SetWindowPosFlags.DoNotChangeOwnerZOrder | SetWindowPosFlags.DoNotActivate | SetWindowPosFlags.IgnoreZOrder);

                    var newDpiRatio = MonitorDpi.GetScaleRatioForWindow(AssociatedObject);
                    if (newDpiRatio != currentDpiRatio) UpdateDpiScaling(newDpiRatio);

                    break;
            }

            return IntPtr.Zero;
        }

        void UpdateDpiScaling(double newDpiRatio)
        {
            currentDpiRatio = newDpiRatio;

            var firstChild = (Visual)VisualTreeHelper.GetChild(AssociatedObject, 0);
            firstChild.SetValue(Window.LayoutTransformProperty, new ScaleTransform(currentDpiRatio, currentDpiRatio));
        }

    }
}
