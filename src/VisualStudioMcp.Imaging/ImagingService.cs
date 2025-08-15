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

    // Note: Windows API declarations moved to GdiNativeMethods for security and maintainability

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
    /// Finds a window by its title with security validation.
    /// </summary>
    private IntPtr FindWindowByTitle(string windowTitle)
    {
        try
        {
            // First try direct window search
            var hwnd = GdiNativeMethods.FindWindow(null, windowTitle);
            if (hwnd != IntPtr.Zero && GdiNativeMethods.IsWindowVisible(hwnd))
            {
                // Validate window ownership for security
                if (ValidateWindowOwnership(hwnd))
                {
                    return hwnd;
                }
                _logger.LogWarning("Window found but ownership validation failed: {WindowTitle}", windowTitle);
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
    /// Finds the Visual Studio main window with security validation.
    /// </summary>
    private IntPtr FindVisualStudioMainWindow()
    {
        try
        {
            // Try common Visual Studio window class names
            var vsClassNames = new[] { "HwndWrapper[DefaultDomain;;]", "VisualStudioMainWindow" };

            foreach (var className in vsClassNames)
            {
                var hwnd = GdiNativeMethods.FindWindow(className, null);
                if (hwnd != IntPtr.Zero && GdiNativeMethods.IsWindowVisible(hwnd))
                {
                    // Validate window ownership for security
                    if (ValidateWindowOwnership(hwnd))
                    {
                        return hwnd;
                    }
                    _logger.LogWarning("Visual Studio window found but ownership validation failed for class: {ClassName}", className);
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
    /// Captures a window by its handle using safe GDI resource management.
    /// </summary>
    private ImageCapture CaptureWindowByHandle(IntPtr windowHandle)
    {
        try
        {
            // Validate window handle ownership for security
            if (!ValidateWindowOwnership(windowHandle))
            {
                _logger.LogWarning("Window handle validation failed for security reasons: {Handle}", windowHandle);
                return CreateEmptyCapture();
            }

            if (!GdiNativeMethods.GetWindowRect(windowHandle, out var rect))
            {
                GdiNativeMethods.ThrowIfError("GetWindowRect");
                _logger.LogWarning("Failed to get window rectangle for handle: {Handle}", windowHandle);
                return CreateEmptyCapture();
            }

            var width = rect.Width;
            var height = rect.Height;

            if (width <= 0 || height <= 0)
            {
                _logger.LogWarning("Invalid window dimensions: {Width}x{Height}", width, height);
                return CreateEmptyCapture();
            }

            using var windowDC = SafeDeviceContext.GetWindowDC(windowHandle);
            if (windowDC == null)
            {
                _logger.LogWarning("Failed to get window DC for handle: {Handle}", windowHandle);
                return CreateEmptyCapture();
            }

            return CaptureFromDCSecurely(windowDC.Handle, 0, 0, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing window by handle: {Handle}", windowHandle);
            return CreateEmptyCapture();
        }
    }

    /// <summary>
    /// Captures a screen region using safe GDI resource management.
    /// </summary>
    private ImageCapture CaptureScreenRegion(int x, int y, int width, int height)
    {
        try
        {
            using var screenDC = SafeDeviceContext.GetScreenDC();
            if (screenDC == null)
            {
                _logger.LogWarning("Failed to get screen DC");
                return CreateEmptyCapture();
            }

            return CaptureFromDCSecurely(screenDC.Handle, x, y, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing screen region");
            return CreateEmptyCapture();
        }
    }

    /// <summary>
    /// Captures from a device context using RAII pattern for guaranteed resource cleanup.
    /// </summary>
    private ImageCapture CaptureFromDCSecurely(IntPtr sourceDC, int x, int y, int width, int height)
    {
        try
        {
            using var memoryDC = SafeMemoryDC.CreateCompatibleDC(sourceDC);
            if (memoryDC == null)
            {
                _logger.LogError("Failed to create compatible memory DC");
                return CreateEmptyCapture();
            }

            using var bitmap = SafeBitmap.CreateCompatibleBitmap(sourceDC, width, height);
            if (bitmap == null)
            {
                _logger.LogError("Failed to create compatible bitmap {Width}x{Height}", width, height);
                return CreateEmptyCapture();
            }

            // Select bitmap into memory DC
            memoryDC.SelectBitmap(bitmap.Handle);

            // Perform the bit block transfer
            if (!GdiNativeMethods.BitBlt(memoryDC.Handle, 0, 0, width, height, sourceDC, x, y, GdiNativeMethods.SRCCOPY))
            {
                GdiNativeMethods.ThrowIfError("BitBlt");
                _logger.LogError("BitBlt operation failed");
                return CreateEmptyCapture();
            }

            // Convert to managed image and then to byte array
            using var bmp = Image.FromHbitmap(bitmap.Handle);
            var imageData = ConvertImageToByteArray(bmp, ImageFormat.Png);

            var capture = new ImageCapture
            {
                ImageData = imageData,
                ImageFormat = "PNG",
                Width = width,
                Height = height,
                CaptureTime = DateTime.UtcNow
            };

            capture.Metadata["CaptureMethod"] = "GDI+ (Secure)";
            capture.Metadata["SourceDC"] = sourceDC.ToString();
            capture.Metadata["ResourcesAutoDisposed"] = "True";
            
            return capture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in secure DC capture");
            return CreateEmptyCapture();
        }
        // Note: All GDI resources are automatically disposed by RAII wrappers
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
    /// Saves a capture to a file with path validation.
    /// </summary>
    private void SaveCaptureToFile(ImageCapture capture, string filePath)
    {
        try
        {
            if (capture.ImageData.Length == 0)
            {
                throw new InvalidOperationException("Cannot save empty image capture");
            }

            // Validate file path for security
            var safePath = ValidateFilePath(filePath);

            var directory = Path.GetDirectoryName(safePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(safePath, capture.ImageData);

            _logger.LogDebug("Saved {Bytes} bytes to {FilePath}", capture.ImageData.Length, safePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving capture to file: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Validates window ownership to prevent unauthorized access to other applications' windows.
    /// </summary>
    private bool ValidateWindowOwnership(IntPtr hwnd)
    {
        try
        {
            if (hwnd == IntPtr.Zero)
                return false;

            // Get the process ID that owns this window
            GdiNativeMethods.GetWindowThreadProcessId(hwnd, out var windowProcessId);
            
            // Get current process ID
            var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            
            // For now, allow all Visual Studio processes and the current process
            // In a more restrictive implementation, we would maintain a whitelist
            using var windowProcess = System.Diagnostics.Process.GetProcessById((int)windowProcessId);
            var processName = windowProcess.ProcessName.ToLowerInvariant();
            
            // Allow Visual Studio processes and the current process
            var allowedProcesses = new[] { "devenv", "visualstudio", "code" };
            
            return windowProcessId == currentProcessId || allowedProcesses.Any(p => processName.Contains(p));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating window ownership for handle: {Handle}", hwnd);
            return false; // Fail secure - if we can't validate, deny access
        }
    }

    /// <summary>
    /// Validates and normalizes file path for safe file operations.
    /// </summary>
    private string ValidateFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        try
        {
            // Get the full path to resolve any relative components
            var fullPath = Path.GetFullPath(filePath);
            
            // Basic security checks
            if (fullPath.Contains("..") || fullPath.Contains("~"))
            {
                throw new UnauthorizedAccessException("Path traversal attempts are not allowed");
            }
            
            // Ensure we're not trying to write to system directories
            var systemPaths = new[] 
            { 
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            
            foreach (var systemPath in systemPaths.Where(p => !string.IsNullOrEmpty(p)))
            {
                if (fullPath.StartsWith(systemPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Writing to system directory is not allowed: {systemPath}");
                }
            }
            
            return fullPath;
        }
        catch (Exception ex) when (!(ex is UnauthorizedAccessException || ex is ArgumentException))
        {
            _logger.LogWarning(ex, "Error validating file path: {FilePath}", filePath);
            throw new ArgumentException($"Invalid file path: {filePath}", nameof(filePath), ex);
        }
    }
}