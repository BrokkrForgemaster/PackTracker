using System.Runtime.InteropServices;

namespace PackTracker.Presentation;

internal static class NativeMethods
{
    internal const uint FLASHW_ALL = 3;
    internal const uint FLASHW_TIMERNOFG = 12;

    [StructLayout(LayoutKind.Sequential)]
    internal struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
