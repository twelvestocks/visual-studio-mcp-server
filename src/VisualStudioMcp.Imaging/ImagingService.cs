using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Imaging;

/// <summary>
/// Implementation of Visual Studio imaging and screenshot service.
/// </summary>
public class ImagingService : IImagingService
{
    private readonly ILogger<ImagingService> _logger;
    private readonly IWindowClassificationService _windowClassification;

    // Note: Windows API declarations moved to GdiNativeMethods for security and maintainability

    public ImagingService(ILogger<ImagingService> logger, IWindowClassificationService windowClassification)
    {
        _logger = logger;
        _windowClassification = windowClassification;
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
    /// Finds a window by partial title match using comprehensive window enumeration.
    /// </summary>
    private IntPtr FindWindowByPartialTitle(string partialTitle)
    {
        IntPtr foundWindow = IntPtr.Zero;
        
        try
        {
            GdiNativeMethods.EnumWindows((hwnd, lParam) =>
            {
                if (!GdiNativeMethods.IsWindowVisible(hwnd))
                {
                    return true; // Continue enumeration
                }

                try
                {
                    // Get window title
                    var titleLength = GdiNativeMethods.GetWindowTextLength(hwnd);
                    if (titleLength > 0)
                    {
                        var titleBuilder = new StringBuilder(titleLength + 1);
                        GdiNativeMethods.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
                        var title = titleBuilder.ToString();

                        // Check for partial match
                        if (!string.IsNullOrEmpty(title) && 
                            title.Contains(partialTitle, StringComparison.OrdinalIgnoreCase))
                        {
                            // Validate window ownership for security
                            if (ValidateWindowOwnership(hwnd))
                            {
                                foundWindow = hwnd;
                                return false; // Stop enumeration
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking window title for handle: {Handle}", hwnd);
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during window enumeration for partial title: {PartialTitle}", partialTitle);
        }

        return foundWindow;
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

    #region Specialized Capture Methods

    public async Task<SpecializedCapture> CaptureSolutionExplorerAsync()
    {
        _logger.LogInformation("Capturing Solution Explorer with annotations");

        try
        {
            var windows = await _windowClassification.FindWindowsByTypeAsync(VisualStudioWindowType.SolutionExplorer);
            var solutionExplorer = windows.FirstOrDefault();
            
            if (solutionExplorer == null)
            {
                _logger.LogWarning("Solution Explorer window not found");
                return CreateEmptySpecializedCapture(VisualStudioWindowType.SolutionExplorer);
            }

            return await CaptureWindowWithAnnotationAsync(solutionExplorer.Handle, VisualStudioWindowType.SolutionExplorer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Solution Explorer");
            return CreateEmptySpecializedCapture(VisualStudioWindowType.SolutionExplorer);
        }
    }

    public async Task<SpecializedCapture> CapturePropertiesWindowAsync()
    {
        _logger.LogInformation("Capturing Properties Window with highlighting");

        try
        {
            var windows = await _windowClassification.FindWindowsByTypeAsync(VisualStudioWindowType.PropertiesWindow);
            var propertiesWindow = windows.FirstOrDefault();
            
            if (propertiesWindow == null)
            {
                _logger.LogWarning("Properties window not found");
                return CreateEmptySpecializedCapture(VisualStudioWindowType.PropertiesWindow);
            }

            return await CaptureWindowWithAnnotationAsync(propertiesWindow.Handle, VisualStudioWindowType.PropertiesWindow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Properties Window");
            return CreateEmptySpecializedCapture(VisualStudioWindowType.PropertiesWindow);
        }
    }

    public async Task<SpecializedCapture> CaptureErrorListAndOutputAsync()
    {
        _logger.LogInformation("Capturing Error List and Output windows with formatting");

        try
        {
            var errorListWindows = await _windowClassification.FindWindowsByTypeAsync(VisualStudioWindowType.ErrorList);
            var errorListWindow = errorListWindows.FirstOrDefault();
            
            if (errorListWindow == null)
            {
                _logger.LogWarning("Error List window not found");
                return CreateEmptySpecializedCapture(VisualStudioWindowType.ErrorList);
            }

            return await CaptureWindowWithAnnotationAsync(errorListWindow.Handle, VisualStudioWindowType.ErrorList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Error List and Output windows");
            return CreateEmptySpecializedCapture(VisualStudioWindowType.ErrorList);
        }
    }

    public async Task<SpecializedCapture> CaptureCodeEditorAsync(IntPtr? editorWindowHandle = null)
    {
        _logger.LogInformation("Capturing Code Editor with syntax highlighting preservation");

        try
        {
            IntPtr targetWindow;
            
            if (editorWindowHandle.HasValue && editorWindowHandle.Value != IntPtr.Zero)
            {
                targetWindow = editorWindowHandle.Value;
            }
            else
            {
                var codeEditors = await _windowClassification.FindWindowsByTypeAsync(VisualStudioWindowType.CodeEditor);
                var activeEditor = codeEditors.FirstOrDefault(w => w.IsActive) ?? codeEditors.FirstOrDefault();
                
                if (activeEditor == null)
                {
                    _logger.LogWarning("Code Editor window not found");
                    return CreateEmptySpecializedCapture(VisualStudioWindowType.CodeEditor);
                }
                
                targetWindow = activeEditor.Handle;
            }

            return await CaptureWindowWithAnnotationAsync(targetWindow, VisualStudioWindowType.CodeEditor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing Code Editor");
            return CreateEmptySpecializedCapture(VisualStudioWindowType.CodeEditor);
        }
    }

    public async Task<FullIdeCapture> CaptureFullIdeWithLayoutAsync()
    {
        _logger.LogInformation("Capturing complete Visual Studio IDE with layout");

        try
        {
            var layout = await _windowClassification.AnalyzeLayoutAsync();
            var fullCapture = new FullIdeCapture
            {
                Layout = layout,
                CaptureTime = DateTime.UtcNow
            };

            // Capture main window first for the composite image
            if (layout.MainWindow != null)
            {
                var mainCapture = CaptureWindowByHandle(layout.MainWindow.Handle);
                fullCapture.CompositeImage = mainCapture;
            }

            // Capture each significant window
            var captureTasks = new List<Task<SpecializedCapture>>();
            
            foreach (var windowType in layout.WindowsByType.Keys)
            {
                if (windowType != VisualStudioWindowType.Unknown && windowType != VisualStudioWindowType.MainWindow)
                {
                    var windows = layout.WindowsByType[windowType];
                    foreach (var window in windows.Take(1)) // Capture first instance of each type
                    {
                        captureTasks.Add(CaptureWindowWithAnnotationAsync(window.Handle, windowType));
                    }
                }
            }

            // Wait for all captures to complete
            var windowCaptures = await Task.WhenAll(captureTasks);
            fullCapture.WindowCaptures = windowCaptures.Where(c => c.ImageData.Length > 0).ToList();

            // Add IDE metadata
            fullCapture.IdeMetadata["WindowCount"] = layout.AllWindows.Count;
            fullCapture.IdeMetadata["WindowTypes"] = layout.WindowsByType.Keys.ToList();
            fullCapture.IdeMetadata["CapturedWindows"] = fullCapture.WindowCaptures.Count;

            _logger.LogInformation("Full IDE capture completed: {WindowCount} windows captured", 
                fullCapture.WindowCaptures.Count);

            return fullCapture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing full IDE");
            return new FullIdeCapture { CaptureTime = DateTime.UtcNow };
        }
    }

    public async Task<SpecializedCapture> CaptureWindowWithAnnotationAsync(IntPtr windowHandle, VisualStudioWindowType windowType)
    {
        _logger.LogInformation("Capturing window with annotations: {WindowType}", windowType);

        try
        {
            // Capture the basic window image
            var baseCapture = CaptureWindowByHandle(windowHandle);
            
            // Create specialized capture from base capture
            var specializedCapture = new SpecializedCapture
            {
                ImageData = baseCapture.ImageData,
                ImageFormat = baseCapture.ImageFormat,
                Width = baseCapture.Width,
                Height = baseCapture.Height,
                CaptureTime = baseCapture.CaptureTime,
                Metadata = baseCapture.Metadata,
                WindowType = windowType
            };

            // Apply window-type-specific annotations and metadata
            await ApplyWindowSpecificProcessingAsync(specializedCapture, windowHandle, windowType);

            return specializedCapture;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing window with annotations: {WindowType}", windowType);
            return CreateEmptySpecializedCapture(windowType);
        }
    }

    private async Task ApplyWindowSpecificProcessingAsync(SpecializedCapture capture, IntPtr windowHandle, VisualStudioWindowType windowType)
    {
        switch (windowType)
        {
            case VisualStudioWindowType.SolutionExplorer:
                await ProcessSolutionExplorerCapture(capture, windowHandle);
                break;
                
            case VisualStudioWindowType.PropertiesWindow:
                await ProcessPropertiesWindowCapture(capture, windowHandle);
                break;
                
            case VisualStudioWindowType.ErrorList:
                await ProcessErrorListCapture(capture, windowHandle);
                break;
                
            case VisualStudioWindowType.CodeEditor:
                await ProcessCodeEditorCapture(capture, windowHandle);
                break;
                
            default:
                await ProcessGenericWindowCapture(capture, windowHandle);
                break;
        }
    }

    private async Task ProcessSolutionExplorerCapture(SpecializedCapture capture, IntPtr windowHandle)
    {
        await Task.Run(() =>
        {
            try
            {
                // Add Solution Explorer specific metadata
                capture.WindowMetadata.SolutionExplorer = new SolutionExplorerMetadata
                {
                    ProjectCount = EstimateProjectCount(capture),
                    SelectedItem = "Unknown", // Would need UI Automation for accurate detection
                    ExpandedNodes = new List<string>() // Would need UI Automation
                };

                // Add basic annotations for tree structure
                AddTreeStructureAnnotations(capture);
                
                _logger.LogDebug("Solution Explorer processing completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Solution Explorer capture");
            }
        });
    }

    private async Task ProcessPropertiesWindowCapture(SpecializedCapture capture, IntPtr windowHandle)
    {
        await Task.Run(() =>
        {
            try
            {
                // Add Properties Window specific metadata
                capture.WindowMetadata.Properties = new PropertiesWindowMetadata
                {
                    SelectedObjectType = "Unknown", // Would need UI Automation
                    Categories = new List<string>(), // Would need UI Automation
                    ModifiedProperties = new List<string>()
                };

                // Add property grid annotations
                AddPropertyGridAnnotations(capture);
                
                _logger.LogDebug("Properties Window processing completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Properties Window capture");
            }
        });
    }

    private async Task ProcessErrorListCapture(SpecializedCapture capture, IntPtr windowHandle)
    {
        await Task.Run(() =>
        {
            try
            {
                // Add Error List specific metadata
                capture.WindowMetadata.ErrorList = new ErrorListMetadata
                {
                    ErrorCount = 0, // Would need UI Automation for accurate counts
                    WarningCount = 0,
                    MessageCount = 0,
                    ActiveFilter = "All"
                };

                // Add error highlighting annotations
                AddErrorListAnnotations(capture);
                
                _logger.LogDebug("Error List processing completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Error List capture");
            }
        });
    }

    private async Task ProcessCodeEditorCapture(SpecializedCapture capture, IntPtr windowHandle)
    {
        await Task.Run(() =>
        {
            try
            {
                // Add Code Editor specific metadata
                capture.WindowMetadata.CodeEditor = new CodeEditorMetadata
                {
                    FileName = ExtractFileNameFromWindowTitle(windowHandle),
                    Language = "Unknown", // Would need context analysis
                    SyntaxHighlightingActive = true,
                    CurrentLine = null, // Would need UI Automation
                    CurrentColumn = null
                };

                // Add code structure annotations
                AddCodeStructureAnnotations(capture);
                
                _logger.LogDebug("Code Editor processing completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Code Editor capture");
            }
        });
    }

    private async Task ProcessGenericWindowCapture(SpecializedCapture capture, IntPtr windowHandle)
    {
        await Task.Run(() =>
        {
            try
            {
                // Add basic window outline
                AddBasicWindowOutline(capture);
                
                _logger.LogDebug("Generic window processing completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing generic window capture");
            }
        });
    }

    private int EstimateProjectCount(SpecializedCapture capture)
    {
        // Simple heuristic - would be more accurate with UI Automation
        return 1; // Placeholder implementation
    }

    private void AddTreeStructureAnnotations(SpecializedCapture capture)
    {
        // Add basic tree outline annotation
        capture.Annotations.Add(new CaptureAnnotation
        {
            Type = AnnotationType.Outline,
            Bounds = new WindowBounds { X = 10, Y = 50, Width = capture.Width - 20, Height = capture.Height - 100 },
            Color = "#0078D4", // Visual Studio blue
            Label = "Solution Explorer Tree"
        });
    }

    private void AddPropertyGridAnnotations(SpecializedCapture capture)
    {
        // Add property grid outline annotation
        capture.Annotations.Add(new CaptureAnnotation
        {
            Type = AnnotationType.Outline,
            Bounds = new WindowBounds { X = 5, Y = 30, Width = capture.Width - 10, Height = capture.Height - 60 },
            Color = "#0078D4",
            Label = "Properties Grid"
        });
    }

    private void AddErrorListAnnotations(SpecializedCapture capture)
    {
        // Add error list outline annotation
        capture.Annotations.Add(new CaptureAnnotation
        {
            Type = AnnotationType.Outline,
            Bounds = new WindowBounds { X = 5, Y = 30, Width = capture.Width - 10, Height = capture.Height - 60 },
            Color = "#E81123", // Error red
            Label = "Error List"
        });
    }

    private void AddCodeStructureAnnotations(SpecializedCapture capture)
    {
        // Add code editor outline annotation
        capture.Annotations.Add(new CaptureAnnotation
        {
            Type = AnnotationType.Outline,
            Bounds = new WindowBounds { X = 5, Y = 5, Width = capture.Width - 10, Height = capture.Height - 10 },
            Color = "#569CD6", // Code blue
            Label = "Code Editor"
        });
    }

    private void AddBasicWindowOutline(SpecializedCapture capture)
    {
        // Add basic window outline
        capture.Annotations.Add(new CaptureAnnotation
        {
            Type = AnnotationType.Outline,
            Bounds = new WindowBounds { X = 1, Y = 1, Width = capture.Width - 2, Height = capture.Height - 2 },
            Color = "#666666", // Grey outline
            Label = "Window Outline"
        });
    }

    private string? ExtractFileNameFromWindowTitle(IntPtr windowHandle)
    {
        try
        {
            var titleLength = GdiNativeMethods.GetWindowTextLength(windowHandle);
            if (titleLength > 0)
            {
                var titleBuilder = new StringBuilder(titleLength + 1);
                GdiNativeMethods.GetWindowText(windowHandle, titleBuilder, titleBuilder.Capacity);
                var title = titleBuilder.ToString();

                // Extract filename from title (usually contains filename)
                var parts = title.Split(new[] { '-', '—' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    var fileName = parts[0].Trim();
                    if (fileName.Contains('.'))
                    {
                        return fileName;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting filename from window title");
        }

        return null;
    }

    private SpecializedCapture CreateEmptySpecializedCapture(VisualStudioWindowType windowType)
    {
        return new SpecializedCapture
        {
            ImageData = Array.Empty<byte>(),
            ImageFormat = "PNG",
            Width = 0,
            Height = 0,
            CaptureTime = DateTime.UtcNow,
            WindowType = windowType
        };
    }

    #endregion
}