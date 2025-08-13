using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Imaging;

/// <summary>
/// Implementation of Visual Studio imaging and screenshot service.
/// </summary>
public class ImagingService : IImagingService
{
    private readonly ILogger<ImagingService> _logger;

    // Windows API declarations for screenshot capture
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hGDIObj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, uint dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hDC);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr parentHwnd, IntPtr childAfterHwnd, string className, string windowText);

    // Constants for BitBlt operation
    private const uint SRCCOPY = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public ImagingService(ILogger<ImagingService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageCapture> CaptureWindowAsync(string windowTitle)
    {
        _logger.LogInformation("Capturing window: {WindowTitle}", windowTitle);
        
        return await Task.Run(() =>
        {
            try
            {
                var windowHandle = FindWindowByTitle(windowTitle);
                if (windowHandle == IntPtr.Zero)
                {
                    _logger.LogWarning("Window not found: {WindowTitle}", windowTitle);
                    return CreateEmptyCapture();
                }

                return CaptureWindowByHandle(windowHandle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing window: {WindowTitle}", windowTitle);
                return CreateEmptyCapture();
            }
        });
    }

    public async Task<ImageCapture> CaptureFullIdeAsync()
    {
        _logger.LogInformation("Capturing full Visual Studio IDE");
        
        return await Task.Run(() =>
        {
            try
            {
                // Find Visual Studio main window
                var vsWindow = FindVisualStudioMainWindow();
                if (vsWindow == IntPtr.Zero)
                {
                    _logger.LogWarning("Visual Studio main window not found");
                    return CreateEmptyCapture();
                }

                return CaptureWindowByHandle(vsWindow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing full Visual Studio IDE");
                return CreateEmptyCapture();
            }
        });
    }

    public async Task<ImageCapture> CaptureRegionAsync(int x, int y, int width, int height)
    {
        _logger.LogInformation("Capturing region: ({X}, {Y}) {Width}x{Height}", x, y, width, height);
        
        return await Task.Run(() =>
        {
            try
            {
                return CaptureScreenRegion(x, y, width, height);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing screen region: ({X}, {Y}) {Width}x{Height}", x, y, width, height);
                return CreateEmptyCapture();
            }
        });
    }

    public async Task SaveCaptureAsync(ImageCapture capture, string filePath)
    {
        _logger.LogInformation("Saving capture to: {FilePath}", filePath);
        
        await Task.Run(() =>
        {
            try
            {
                SaveCaptureToFile(capture, filePath);
                _logger.LogInformation("Successfully saved capture to: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving capture to: {FilePath}", filePath);
                throw;
            }
        });
    }

    /// <summary>
    /// Finds a window by its title.
    /// </summary>
    private IntPtr FindWindowByTitle(string windowTitle)
    {
        try
        {
            // First try direct window search
            var hwnd = FindWindow(null, windowTitle);
            if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
            {
                return hwnd;
            }

            // If not found, search through all windows for partial matches
            return FindWindowByPartialTitle(windowTitle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding window by title: {WindowTitle}", windowTitle);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Finds a window by partial title match.
    /// </summary>
    private IntPtr FindWindowByPartialTitle(string partialTitle)
    {
        // This is a simplified implementation
        // In a full implementation, we would enumerate all windows
        return IntPtr.Zero;
    }

    /// <summary>
    /// Finds the Visual Studio main window.
    /// </summary>
    private IntPtr FindVisualStudioMainWindow()
    {
        try
        {
            // Try common Visual Studio window class names
            var vsClassNames = new[] { "HwndWrapper[DefaultDomain;;]", "VisualStudioMainWindow" };

            foreach (var className in vsClassNames)
            {
                var hwnd = FindWindow(className, null);
                if (hwnd != IntPtr.Zero && IsWindowVisible(hwnd))
                {
                    return hwnd;
                }
            }

            // Try to find by window title containing "Visual Studio"
            return FindWindowByPartialTitle("Visual Studio");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error finding Visual Studio main window");
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// Captures a window by its handle.
    /// </summary>
    private ImageCapture CaptureWindowByHandle(IntPtr windowHandle)
    {
        try
        {
            if (!GetWindowRect(windowHandle, out var rect))
            {
                _logger.LogWarning("Failed to get window rectangle for handle: {Handle}", windowHandle);
                return CreateEmptyCapture();
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                _logger.LogWarning("Invalid window dimensions: {Width}x{Height}", width, height);
                return CreateEmptyCapture();
            }

            var windowDC = GetWindowDC(windowHandle);
            if (windowDC == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to get window DC for handle: {Handle}", windowHandle);
                return CreateEmptyCapture();
            }

            try
            {
                return CaptureFromDC(windowDC, 0, 0, width, height);
            }
            finally
            {
                ReleaseDC(windowHandle, windowDC);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing window by handle: {Handle}", windowHandle);
            return CreateEmptyCapture();
        }
    }

    /// <summary>
    /// Captures a screen region.
    /// </summary>
    private ImageCapture CaptureScreenRegion(int x, int y, int width, int height)
    {
        try
        {
            var screenDC = GetDC(IntPtr.Zero);
            if (screenDC == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to get screen DC");
                return CreateEmptyCapture();
            }

            try
            {
                return CaptureFromDC(screenDC, x, y, width, height);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing screen region");
            return CreateEmptyCapture();
        }
    }

    /// <summary>
    /// Captures from a device context.
    /// </summary>
    private ImageCapture CaptureFromDC(IntPtr sourceDC, int x, int y, int width, int height)
    {
        var memoryDC = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var oldBitmap = IntPtr.Zero;

        try
        {
            memoryDC = CreateCompatibleDC(sourceDC);
            bitmap = CreateCompatibleBitmap(sourceDC, width, height);
            oldBitmap = SelectObject(memoryDC, bitmap);

            BitBlt(memoryDC, 0, 0, width, height, sourceDC, x, y, SRCCOPY);

            var bmp = Image.FromHbitmap(bitmap);
            var imageData = ConvertImageToByteArray(bmp, ImageFormat.Png);

            var capture = new ImageCapture
            {
                ImageData = imageData,
                ImageFormat = "PNG",
                Width = width,
                Height = height,
                CaptureTime = DateTime.UtcNow
            };

            capture.Metadata["CaptureMethod"] = "GDI+";
            capture.Metadata["SourceDC"] = sourceDC.ToString();
            
            bmp.Dispose();

            return capture;
        }
        finally
        {
            if (oldBitmap != IntPtr.Zero)
                SelectObject(memoryDC, oldBitmap);
            if (bitmap != IntPtr.Zero)
                DeleteObject(bitmap);
            if (memoryDC != IntPtr.Zero)
                DeleteDC(memoryDC);
        }
    }

    /// <summary>
    /// Converts an image to byte array.
    /// </summary>
    private byte[] ConvertImageToByteArray(Image image, ImageFormat format)
    {
        using var stream = new MemoryStream();
        image.Save(stream, format);
        return stream.ToArray();
    }

    /// <summary>
    /// Creates an empty capture result.
    /// </summary>
    private ImageCapture CreateEmptyCapture()
    {
        return new ImageCapture
        {
            ImageData = Array.Empty<byte>(),
            ImageFormat = "PNG",
            Width = 0,
            Height = 0,
            CaptureTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Saves a capture to a file.
    /// </summary>
    private void SaveCaptureToFile(ImageCapture capture, string filePath)
    {
        try
        {
            if (capture.ImageData.Length == 0)
            {
                throw new InvalidOperationException("Cannot save empty image capture");
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(filePath, capture.ImageData);

            _logger.LogDebug("Saved {Bytes} bytes to {FilePath}", capture.ImageData.Length, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving capture to file: {FilePath}", filePath);
            throw;
        }
    }
}