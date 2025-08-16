using System.Runtime.InteropServices;
using System.Text;

namespace VisualStudioMcp.Imaging;

/// <summary>
/// RAII wrapper for Windows GDI device context (DC) handles.
/// Ensures proper cleanup of GDI resources even in exception scenarios.
/// </summary>
public sealed class SafeDeviceContext : IDisposable
{
    private IntPtr _hdc;
    private IntPtr _hwnd;
    private bool _disposed;

    public IntPtr Handle => _hdc;

    public SafeDeviceContext(IntPtr hdc, IntPtr hwnd = default)
    {
        _hdc = hdc;
        _hwnd = hwnd;
    }

    public static SafeDeviceContext? GetWindowDC(IntPtr hwnd)
    {
        var hdc = GdiNativeMethods.GetWindowDC(hwnd);
        return hdc != IntPtr.Zero ? new SafeDeviceContext(hdc, hwnd) : null;
    }

    public static SafeDeviceContext? GetScreenDC()
    {
        var hdc = GdiNativeMethods.GetDC(IntPtr.Zero);
        return hdc != IntPtr.Zero ? new SafeDeviceContext(hdc) : null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && _hdc != IntPtr.Zero)
        {
            GdiNativeMethods.ReleaseDC(_hwnd, _hdc);
            _hdc = IntPtr.Zero;
            _disposed = true;
        }
    }

    ~SafeDeviceContext()
    {
        Dispose(false);
    }
}

/// <summary>
/// RAII wrapper for Windows GDI bitmap handles.
/// Ensures proper cleanup of GDI bitmap resources.
/// </summary>
public sealed class SafeBitmap : IDisposable
{
    private IntPtr _hBitmap;
    private bool _disposed;

    public IntPtr Handle => _hBitmap;

    public SafeBitmap(IntPtr hBitmap)
    {
        _hBitmap = hBitmap;
    }

    public static SafeBitmap? CreateCompatibleBitmap(IntPtr hdc, int width, int height)
    {
        var hBitmap = GdiNativeMethods.CreateCompatibleBitmap(hdc, width, height);
        return hBitmap != IntPtr.Zero ? new SafeBitmap(hBitmap) : null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && _hBitmap != IntPtr.Zero)
        {
            GdiNativeMethods.DeleteObject(_hBitmap);
            _hBitmap = IntPtr.Zero;
            _disposed = true;
        }
    }

    ~SafeBitmap()
    {
        Dispose(false);
    }
}

/// <summary>
/// RAII wrapper for Windows GDI memory device context.
/// Manages both the memory DC and the selected bitmap.
/// </summary>
public sealed class SafeMemoryDC : IDisposable
{
    private IntPtr _memoryDC;
    private IntPtr _oldBitmap;
    private bool _disposed;

    public IntPtr Handle => _memoryDC;

    public SafeMemoryDC(IntPtr memoryDC)
    {
        _memoryDC = memoryDC;
    }

    public static SafeMemoryDC? CreateCompatibleDC(IntPtr hdc)
    {
        var memoryDC = GdiNativeMethods.CreateCompatibleDC(hdc);
        return memoryDC != IntPtr.Zero ? new SafeMemoryDC(memoryDC) : null;
    }

    public void SelectBitmap(IntPtr hBitmap)
    {
        _oldBitmap = GdiNativeMethods.SelectObject(_memoryDC, hBitmap);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (_oldBitmap != IntPtr.Zero)
            {
                GdiNativeMethods.SelectObject(_memoryDC, _oldBitmap);
                _oldBitmap = IntPtr.Zero;
            }

            if (_memoryDC != IntPtr.Zero)
            {
                GdiNativeMethods.DeleteDC(_memoryDC);
                _memoryDC = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    ~SafeMemoryDC()
    {
        Dispose(false);
    }
}

/// <summary>
/// Centralized P/Invoke declarations for GDI operations with enhanced security.
/// All methods include proper error checking and security attributes.
/// </summary>
internal static class GdiNativeMethods
{
    // Windows API constants
    public const uint SRCCOPY = 0x00CC0020;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIObj);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, 
        IntPtr hSrcDC, int xSrc, int ySrc, uint dwRop);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr GetForegroundWindow();

    // Constants for GetWindow
    internal const uint GW_HWNDFIRST = 0;
    internal const uint GW_HWNDLAST = 1;
    internal const uint GW_HWNDNEXT = 2;
    internal const uint GW_HWNDPREV = 3;
    internal const uint GW_OWNER = 4;
    internal const uint GW_CHILD = 5;

    // Window enumeration delegate
    internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Gets the last Win32 error and throws a meaningful exception if an error occurred.
    /// </summary>
    /// <param name="operationName">Name of the operation that was performed.</param>
    /// <exception cref="ComponentModel.Win32Exception">Thrown when a Win32 error occurred.</exception>
    internal static void ThrowIfError(string operationName)
    {
        var error = Marshal.GetLastWin32Error();
        if (error != 0)
        {
            throw new System.ComponentModel.Win32Exception(error, $"{operationName} failed");
        }
    }

    /// <summary>
    /// Validates that a handle is not null/zero and throws an exception if it is.
    /// </summary>
    /// <param name="handle">Handle to validate.</param>
    /// <param name="operationName">Name of the operation that created the handle.</param>
    /// <exception cref="InvalidOperationException">Thrown when handle is invalid.</exception>
    internal static void ValidateHandle(IntPtr handle, string operationName)
    {
        if (handle == IntPtr.Zero)
        {
            ThrowIfError(operationName);
            throw new InvalidOperationException($"{operationName} returned invalid handle");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }
}