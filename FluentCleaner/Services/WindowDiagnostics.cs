using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace FluentCleaner.Services;

public static class WindowDiagnostics
{
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int DWMWA_CLOAKED = 14;

    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_MINIMIZE = 0x20000000;
    private const uint WS_DISABLED = 0x08000000;
    private const uint WS_POPUP = 0x80000000;

    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_APPWINDOW = 0x00040000;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        public override string ToString() => $"[{Left}, {Top} to {Right}, {Bottom} ({Width}x{Height})]";
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    public static void EnsureForeground(IntPtr hwnd)
    {
        ShowWindow(hwnd, 5); // SW_SHOW
        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    public static void InspectWindow(Window window, string phase)
    {
        try
        {
            Program.LogDiag($"=== [WINDOW-INSPECT] Phase: {phase} ===");

            var appWindow = window.AppWindow;
            if (appWindow == null)
            {
                Program.LogDiag("[WINDOW-INSPECT] ERROR: AppWindow is NULL!");
                return;
            }

            Program.LogDiag($"[AppWindow] Id: {appWindow.Id.Value}, IsVisible: {appWindow.IsVisible}");
            Program.LogDiag($"[AppWindow] Size: {appWindow.Size.Width}x{appWindow.Size.Height}, Position: ({appWindow.Position.X}, {appWindow.Position.Y})");
            Program.LogDiag($"[AppWindow] Presenter: {appWindow.Presenter?.Kind.ToString() ?? "NULL"}");

            var hwnd = WindowNative.GetWindowHandle(window);
            Program.LogDiag($"[Win32-HWND] 0x{hwnd.ToInt64():X} ({hwnd.ToInt64()})");

            bool isWin = IsWindow(hwnd);
            bool isWinVis = IsWindowVisible(hwnd);
            bool isIconic = IsIconic(hwnd);

            Program.LogDiag($"[Win32] IsWindow: {isWin}, IsWindowVisible: {isWinVis}, IsIconic: {isIconic}");

            if (GetWindowRect(hwnd, out var wRect))
                Program.LogDiag($"[Win32] GetWindowRect: {wRect}");
            else
                Program.LogDiag("[Win32] GetWindowRect: FAILED");

            if (GetClientRect(hwnd, out var cRect))
                Program.LogDiag($"[Win32] GetClientRect: {cRect}");
            else
                Program.LogDiag("[Win32] GetClientRect: FAILED");

            long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

            bool styleVisible = (style & WS_VISIBLE) != 0;
            bool styleMinimized = (style & WS_MINIMIZE) != 0;
            bool stylePopup = (style & WS_POPUP) != 0;
            bool exToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
            bool exAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

            Program.LogDiag($"[Win32] GWL_STYLE: 0x{style:X8} (WS_VISIBLE={styleVisible}, WS_MINIMIZE={styleMinimized}, WS_POPUP={stylePopup})");
            Program.LogDiag($"[Win32] GWL_EXSTYLE: 0x{exStyle:X8} (WS_EX_TOOLWINDOW={exToolWindow}, WS_EX_APPWINDOW={exAppWindow})");

            int dwmRes = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            if (dwmRes == 0)
                Program.LogDiag($"[Win32] DWMWA_CLOAKED: {cloaked} (0=Not Cloaked, 1=App, 2=Shell, 4=Inherited)");
            else
                Program.LogDiag($"[Win32] DwmGetWindowAttribute CLOAKED returned 0x{dwmRes:X8}");

            // Inspect DisplayArea
            var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.None);
            if (display != null)
            {
                Program.LogDiag($"[DisplayArea] Display: {display.DisplayId.Value}, IsPrimary: {display.IsPrimary}");
                Program.LogDiag($"[DisplayArea] WorkArea: {display.WorkArea.X},{display.WorkArea.Y} ({display.WorkArea.Width}x{display.WorkArea.Height})");
                Program.LogDiag($"[DisplayArea] OuterBounds: {display.OuterBounds.X},{display.OuterBounds.Y} ({display.OuterBounds.Width}x{display.OuterBounds.Height})");
            }
            else
            {
                Program.LogDiag("[DisplayArea] WARNING: GetFromWindowId returned NULL (window is NOT on any display!)");
            }

            // Win32 Title & Placement
            var sbTitle = new StringBuilder(256);
            GetWindowText(hwnd, sbTitle, 256);
            Program.LogDiag($"[Win32] GetWindowText: '{sbTitle}'");

            var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
            if (GetWindowPlacement(hwnd, ref wp))
                Program.LogDiag($"[Win32] GetWindowPlacement: showCmd={wp.showCmd}, flags={wp.flags}, normalPos=[{wp.rcNormalPosition.Left},{wp.rcNormalPosition.Top} to {wp.rcNormalPosition.Right},{wp.rcNormalPosition.Bottom}]");

            // Inspect Window Station & Desktop
            try
            {
                IntPtr hDesk = GetThreadDesktop(GetCurrentThreadId());
                var sbDesk = new StringBuilder(256);
                if (GetUserObjectInformation(hDesk, 2, sbDesk, 256, out _))
                    Program.LogDiag($"[Win32-Desktop] ThreadDesktop: {sbDesk}");
            }
            catch (Exception deskEx)
            {
                Program.LogDiag($"[Win32-Desktop] Failed to get desktop: {deskEx.Message}");
            }

            // Inspect Virtual Desktop
            try
            {
                var vdmType = Type.GetTypeFromCLSID(new Guid("aa509085-ecd9-42ba-a65c-34eab80b6195"));
                if (vdmType != null)
                {
                    dynamic? vdm = Activator.CreateInstance(vdmType);
                    if (vdm is IVirtualDesktopManager nativeVdm)
                    {
                        nativeVdm.IsWindowOnCurrentVirtualDesktop(hwnd, out bool onCurrent);
                        nativeVdm.GetWindowDesktopId(hwnd, out Guid deskId);
                        Program.LogDiag($"[VirtualDesktop] OnCurrentDesktop: {onCurrent}, DesktopGuid: {deskId}");
                    }
                }
            }
            catch (Exception vdEx)
            {
                Program.LogDiag($"[VirtualDesktop] Check failed: {vdEx.Message}");
            }

            Program.LogDiag($"=== [WINDOW-INSPECT END: {phase}] ===");
        }
        catch (Exception ex)
        {
            Program.LogDiag($"[WINDOW-INSPECT-ERROR] {ex}");
        }
    }

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDesktop(int dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, StringBuilder pvInfo, int nLength, out int lpnLengthNeeded);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        [PreserveSig] int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);
        [PreserveSig] int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);
        [PreserveSig] int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
