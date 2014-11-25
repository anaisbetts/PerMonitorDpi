using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PerMonitorDPI
{
    static class NativeConstants
    {
        public const int GCLP_HBRBACKGROUND = -0x0A;
        public const int WM_NCCALCSIZE = 0x83;
        public const int WM_NCPAINT = 0x85;
        public const int WM_NCACTIVATE = 0x86;
        public const int WM_GETMINMAXINFO = 0x24;
        public const int WM_SETTINGCHANGE = 0x001A;
        public const int WM_DPICHANGED = 0x02E0;
    }

    enum ChangeWindowMessageFilterFlags : uint 
    {
        Add = 1, Remove = 2
    }

    enum PROCESS_DPI_AWARENESS 
    {
        PROCESS_DPI_UNAWARE = 0,
        PROCESS_SYSTEM_DPI_AWARE = 1,
        PROCESS_PER_MONITOR_DPI_AWARE = 2
    }

    [Flags]
    enum SetWindowPosFlags : uint
    {
        AsynchronousWindowPosition = 0x4000,
        DeferErase = 0x2000,
        DrawFrame = 0x0020,
        FrameChanged = 0x0020,
        HideWindow = 0x0080,
        DoNotActivate = 0x0010,
        DoNotCopyBits = 0x0100,
        IgnoreMove = 0x0002,
        DoNotChangeOwnerZOrder = 0x0200,
        DoNotRedraw = 0x0008,
        DoNotReposition = 0x0200,
        DoNotSendChangingEvent = 0x0400,
        IgnoreResize = 0x0001,
        IgnoreZOrder = 0x0004,
        ShowWindow = 0x0040,
    }

    enum DeviceCaps
    {
        VERTRES = 10,
        DESKTOPVERTRES = 117,
    }  

    enum MonitorOpts : uint
    {
        MONITOR_DEFAULTTONULL = 0x00000000,
        MONITOR_DEFAULTTOPRIMARY = 0x00000001,
        MONITOR_DEFAULTTONEAREST = 0x00000002,
    }

    enum MonitorDpiType
    {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
    }

    /// <summary>
    /// This class suppresses stack walks for unmanaged code permission. 
    /// (System.Security.SuppressUnmanagedCodeSecurityAttribute is applied to this class.) 
    /// This class is for methods that are safe for anyone to call. Callers of these methods 
    /// are not required to do a full security review to ensure that the usage is secure 
    /// because the methods are harmless 
    /// </summary>
    /// <remarks>
    /// Methods that simply query for information or state that isn't sensitive can be moved 
    /// here.
    /// </remarks>
    [SuppressUnmanagedCodeSecurity]
    static class SafeNativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool ChangeWindowMessageFilter(uint msg, ChangeWindowMessageFilterFlags flags);

        [DllImport("shcore.dll")]
        internal static extern uint SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("gdi32.dll")]
        internal static extern int GetDeviceCaps(IntPtr hdc, DeviceCaps nIndex);

        [DllImport("user32.dll")]
        internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, MonitorOpts dwFlags);

        [DllImport("shcore.dll")]
        internal static extern uint GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);


        // Note: this methods are taken from https://raw.githubusercontent.com/Microsoft/referencesource/9da503f9ef21e8d1f2905c78d4e3e5cbb3d6f85a/mscorlib/microsoft/win32/win32native.cs

        // Note - do NOT use this to call methods.  Use P/Invoke, which will
        // do much better things w.r.t. marshaling, pinning memory, security 
        // stuff, better interactions with thread aborts, etc.  This is used
        // solely by DoesWin32MethodExist for avoiding try/catch EntryPointNotFoundException
        // in scenarios where an OS Version check is insufficient
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, String methodName);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        private static extern IntPtr LoadLibrary(string libFilename);

        [DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        private static extern bool FreeLibrary(IntPtr hModule);
        

        [System.Security.SecurityCritical]  // auto-generated
        internal static bool DoesWin32MethodExist(String moduleName, String methodName)
        {
            IntPtr hModule = LoadLibrary(moduleName);
          
            if (hModule == IntPtr.Zero)
            {
                Debug.Assert(hModule != IntPtr.Zero, "LoadLibrary failed. API must not be available");
                return false;
            }
            IntPtr functionPointer = GetProcAddress(hModule, methodName);

            FreeLibrary(hModule);

            return (functionPointer != IntPtr.Zero);
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
        public static readonly RECT Empty;

        public int Width
        {
            get { return Math.Abs(right - left); } // Abs needed for BIDI OS
        }

        public int Height
        {
            get { return bottom - top; }
        }

        public RECT(int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        public RECT(RECT rcSrc)
        {
            left = rcSrc.left;
            top = rcSrc.top;
            right = rcSrc.right;
            bottom = rcSrc.bottom;
        }

        public bool IsEmpty
        {
            get
            {
                // BUGBUG : On Bidi OS (hebrew arabic) left > right
                return left >= right || top >= bottom;
            }
        }

        /// <summary> Return a user friendly representation of this struct </summary>
        public override string ToString()
        {
            if (this == Empty)
            {
                return "RECT {Empty}";
            }
            return "RECT { left : " + left + " / top : " + top + " / right : " + right + " / bottom : " + bottom + " }";
        }

        /// <summary> Determine if 2 RECT are equal (deep compare) </summary>
        public override bool Equals(object obj)
        {
            if (!(obj is RECT))
            {
                return false;
            }
            return (this == (RECT)obj);
        }

        /// <summary>Return the HashCode for this struct (not garanteed to be unique)</summary>
        public override int GetHashCode()
        {
            return left.GetHashCode() + top.GetHashCode() + right.GetHashCode() + bottom.GetHashCode();
        }

        /// <summary> Determine if 2 RECT are equal (deep compare)</summary>
        public static bool operator ==(RECT rect1, RECT rect2)
        {
            return (rect1.left == rect2.left && rect1.top == rect2.top && rect1.right == rect2.right && rect1.bottom == rect2.bottom);
        }

        /// <summary> Determine if 2 RECT are different(deep compare)</summary>
        public static bool operator !=(RECT rect1, RECT rect2)
        {
            return !(rect1 == rect2);
        }
    }
}