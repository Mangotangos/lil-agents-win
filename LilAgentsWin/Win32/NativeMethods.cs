using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LilAgentsWin.Win32;

// ─── Taskbar info ────────────────────────────────────────────────────────────

public enum TaskbarEdge { Bottom, Top, Left, Right }

public record TaskbarInfo(TaskbarEdge Edge, Rect Bounds);

// ─── Native structs ───────────────────────────────────────────────────────────

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA
{
    public uint cbSize;
    public IntPtr hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public int lParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int left, top, right, bottom;
}

// ─── TaskbarHelper ────────────────────────────────────────────────────────────

public static class TaskbarHelper
{
    private const uint ABM_GETTASKBARPOS = 5;
    private const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    [DllImport("shell32.dll")]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    public static TaskbarInfo GetTaskbar()
    {
        var data = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
        SHAppBarMessage(ABM_GETTASKBARPOS, ref data);

        var edge = data.uEdge switch
        {
            ABE_TOP   => TaskbarEdge.Top,
            ABE_LEFT  => TaskbarEdge.Left,
            ABE_RIGHT => TaskbarEdge.Right,
            _         => TaskbarEdge.Bottom
        };

        var bounds = new Rect(
            data.rc.left,
            data.rc.top,
            data.rc.right  - data.rc.left,
            data.rc.bottom - data.rc.top);

        return new TaskbarInfo(edge, bounds);
    }
}

// ─── HitTest helper ───────────────────────────────────────────────────────────

public static class HitTestHelper
{
    public const int WM_NCHITTEST  = 0x0084;
    public const int HTTRANSPARENT = -1;
    public const int HTCLIENT      = 1;

    /// <summary>Decode the lParam from WM_NCHITTEST into a screen Point.</summary>
    public static System.Windows.Point DecodeScreenPoint(IntPtr lParam)
    {
        int val = lParam.ToInt32();
        return new System.Windows.Point((short)(val & 0xFFFF), (short)((val >> 16) & 0xFFFF));
    }
}

// ─── WindowHelper ─────────────────────────────────────────────────────────────

public static class WindowHelper
{
    private const int GWL_EXSTYLE  = -20;
    private const int WS_EX_LAYERED    = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW  = 0x00000080;
    private const int WS_EX_NOACTIVATE  = 0x08000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// Set the walker window style: layered + tool window + no-activate.
    /// WM_NCHITTEST handles per-pixel hit testing — no WS_EX_TRANSPARENT needed.
    /// </summary>
    public static void SetWalkerStyle(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int ext  = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            ext | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }
}
