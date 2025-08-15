using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;

namespace VisualStudioMcp.Imaging;

/// <summary>
/// Implementation of Visual Studio window classification and discovery service.
/// </summary>
public class WindowClassificationService : IWindowClassificationService
{
    private readonly ILogger<WindowClassificationService> _logger;
    private readonly List<VisualStudioWindow> _discoveredWindows = new();
    private readonly object _lockObject = new();

    // Known Visual Studio window class names and titles
    private static readonly Dictionary<string, VisualStudioWindowType> ClassNameMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "HwndWrapper[DefaultDomain;;]", VisualStudioWindowType.MainWindow },
        { "VisualStudioMainWindow", VisualStudioWindowType.MainWindow },
        { "VsDebugUIDeadlockDialog", VisualStudioWindowType.Unknown },
        { "tooltips_class32", VisualStudioWindowType.Unknown },
        { "msctls_statusbar32", VisualStudioWindowType.Unknown }
    };

    private static readonly Dictionary<string, VisualStudioWindowType> TitlePatternMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Solution Explorer", VisualStudioWindowType.SolutionExplorer },
        { "Properties", VisualStudioWindowType.PropertiesWindow },
        { "Error List", VisualStudioWindowType.ErrorList },
        { "Output", VisualStudioWindowType.OutputWindow },
        { "Toolbox", VisualStudioWindowType.Toolbox },
        { "Server Explorer", VisualStudioWindowType.ServerExplorer },
        { "Team Explorer", VisualStudioWindowType.TeamExplorer },
        { "Package Manager Console", VisualStudioWindowType.PackageManagerConsole },
        { "Find and Replace", VisualStudioWindowType.FindAndReplace },
        { "Immediate", VisualStudioWindowType.ImmediateWindow },
        { "Watch", VisualStudioWindowType.WatchWindow },
        { "Call Stack", VisualStudioWindowType.CallStackWindow },
        { "Locals", VisualStudioWindowType.LocalsWindow }
    };

    public WindowClassificationService(ILogger<WindowClassificationService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<VisualStudioWindow>> DiscoverVSWindowsAsync()
    {
        _logger.LogInformation("Starting Visual Studio window discovery");
        
        return await Task.Run(() =>
        {
            lock (_lockObject)
            {
                _discoveredWindows.Clear();
                
                try
                {
                    // Use timeout to prevent indefinite blocking during window enumeration
                    var timeout = TimeSpan.FromSeconds(30); // 30 second timeout
                    var startTime = DateTime.UtcNow;
                    
                    // Create cancellation token for timeout
                    using var cts = new CancellationTokenSource(timeout);
                    var cancellationToken = cts.Token;
                    
                    // Enumerate all top-level windows with timeout monitoring
                    var enumerationTask = Task.Run(() =>
                    {
                        GdiNativeMethods.EnumWindows((hwnd, lParam) =>
                        {
                            // Check for cancellation during enumeration
                            if (cancellationToken.IsCancellationRequested)
                            {
                                _logger.LogWarning("Window enumeration cancelled due to timeout");
                                return false; // Stop enumeration
                            }
                            
                            return EnumWindowsCallback(hwnd, lParam);
                        }, IntPtr.Zero);
                    }, cancellationToken);
                    
                    // Wait for enumeration with timeout
                    if (!enumerationTask.Wait(timeout))
                    {
                        _logger.LogWarning("Window enumeration timed out after {Timeout} seconds", timeout.TotalSeconds);
                        return new List<VisualStudioWindow>();
                    }
                    
                    // Post-process to identify child windows and relationships
                    PostProcessWindowRelationships();
                    
                    var elapsed = DateTime.UtcNow - startTime;
                    _logger.LogInformation("Discovered {Count} Visual Studio windows in {Elapsed}ms", 
                        _discoveredWindows.Count, elapsed.TotalMilliseconds);
                    
                    return _discoveredWindows.ToList(); // Return a copy
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Window discovery was cancelled due to timeout");
                    return new List<VisualStudioWindow>();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during window discovery");
                    return new List<VisualStudioWindow>();
                }
            }
        });
    }

    public async Task<VisualStudioWindowType> ClassifyWindowAsync(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return VisualStudioWindowType.Unknown;
        }

        return await Task.Run(() =>
        {
            try
            {
                var windowInfo = ExtractWindowInfo(windowHandle);
                return ClassifyWindowByInfo(windowInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error classifying window handle: {Handle}", windowHandle);
                return VisualStudioWindowType.Unknown;
            }
        });
    }

    public async Task<WindowLayout> AnalyzeLayoutAsync()
    {
        _logger.LogInformation("Starting window layout analysis");
        
        var windows = await DiscoverVSWindowsAsync();
        
        return await Task.Run(() =>
        {
            var layout = new WindowLayout
            {
                AllWindows = windows.ToList(),
                AnalysisTime = DateTime.UtcNow
            };

            // Find the main window
            layout.MainWindow = windows.FirstOrDefault(w => w.WindowType == VisualStudioWindowType.MainWindow);
            
            // Group windows by type
            layout.WindowsByType = windows
                .Where(w => w.WindowType != VisualStudioWindowType.Unknown)
                .GroupBy(w => w.WindowType)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Determine active window
            layout.ActiveWindow = DetermineActiveWindow(windows);

            // Analyze docking layout
            if (layout.MainWindow != null)
            {
                layout.DockingLayout = AnalyzeDockingLayout(layout.MainWindow, windows);
            }

            _logger.LogInformation("Layout analysis complete: {WindowCount} windows, {TypeCount} types", 
                windows.Count(), layout.WindowsByType.Count);

            return layout;
        });
    }

    public async Task<VisualStudioWindow?> GetActiveWindowAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var foregroundWindow = GdiNativeMethods.GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return null;
                }

                var windowInfo = ExtractWindowInfo(foregroundWindow);
                if (IsVisualStudioWindow(windowInfo))
                {
                    windowInfo.IsActive = true;
                    return windowInfo;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting active window");
                return null;
            }
        });
    }

    public async Task<IEnumerable<VisualStudioWindow>> FindWindowsByTypeAsync(VisualStudioWindowType windowType)
    {
        var allWindows = await DiscoverVSWindowsAsync();
        return allWindows.Where(w => w.WindowType == windowType);
    }

    private bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam)
    {
        VisualStudioWindow? windowInfo = null;
        try
        {
            if (!GdiNativeMethods.IsWindow(hwnd) || !GdiNativeMethods.IsWindowVisible(hwnd))
            {
                return true; // Continue enumeration
            }

            windowInfo = ExtractWindowInfo(hwnd);
            
            // Check if this is a Visual Studio window
            if (IsVisualStudioWindow(windowInfo))
            {
                windowInfo.WindowType = ClassifyWindowByInfo(windowInfo);
                
                // Enumerate child windows first (may throw exceptions)
                try
                {
                    EnumerateChildWindows(hwnd, windowInfo);
                }
                catch (Exception childEx)
                {
                    _logger.LogWarning(childEx, "Error enumerating child windows for parent: {Handle}", hwnd);
                    // Continue with parent window even if child enumeration fails
                }
                
                // Only add to collection if everything succeeded
                _discoveredWindows.Add(windowInfo);
            }

            return true; // Continue enumeration
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing window during enumeration: {Handle}", hwnd);
            
            // Ensure partial windowInfo is not left in inconsistent state
            if (windowInfo != null)
            {
                try
                {
                    // Clean up any partially initialized child windows
                    windowInfo.ChildWindows.Clear();
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Error cleaning up window info during exception handling");
                }
            }
            
            return true; // Continue enumeration even after cleanup
        }
    }

    private void EnumerateChildWindows(IntPtr parentHandle, VisualStudioWindow parentWindow)
    {
        try
        {
            var childWindows = new List<VisualStudioWindow>();
            
            GdiNativeMethods.EnumChildWindows(parentHandle, (hwnd, lParam) =>
            {
                if (GdiNativeMethods.IsWindow(hwnd))
                {
                    var childInfo = ExtractWindowInfo(hwnd);
                    childInfo.ParentHandle = parentHandle;
                    childInfo.WindowType = ClassifyWindowByInfo(childInfo);
                    childWindows.Add(childInfo);
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            parentWindow.ChildWindows = childWindows;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating child windows for parent: {Handle}", parentHandle);
        }
    }

    private VisualStudioWindow ExtractWindowInfo(IntPtr windowHandle)
    {
        var window = new VisualStudioWindow
        {
            Handle = windowHandle,
            IsVisible = GdiNativeMethods.IsWindowVisible(windowHandle)
        };

        try
        {
            // Get window title
            var titleLength = GdiNativeMethods.GetWindowTextLength(windowHandle);
            if (titleLength > 0)
            {
                var titleBuilder = new StringBuilder(titleLength + 1);
                GdiNativeMethods.GetWindowText(windowHandle, titleBuilder, titleBuilder.Capacity);
                window.Title = titleBuilder.ToString();
            }

            // Get window class name
            var classBuilder = new StringBuilder(256);
            GdiNativeMethods.GetClassName(windowHandle, classBuilder, classBuilder.Capacity);
            window.ClassName = classBuilder.ToString();

            // Get process ID
            GdiNativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
            window.ProcessId = processId;

            // Get window bounds
            if (GdiNativeMethods.GetWindowRect(windowHandle, out var rect))
            {
                window.Bounds = new WindowBounds
                {
                    X = rect.Left,
                    Y = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                };
            }

            // Get parent window
            var parent = GdiNativeMethods.GetParent(windowHandle);
            if (parent != IntPtr.Zero)
            {
                window.ParentHandle = parent;
            }

            // Check if this is the active window
            window.IsActive = GdiNativeMethods.GetForegroundWindow() == windowHandle;

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting window info for handle: {Handle}", windowHandle);
        }

        return window;
    }

    private bool IsVisualStudioWindow(VisualStudioWindow window)
    {
        try
        {
            // Check process ownership with proper exception handling
            string processName;
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)window.ProcessId);
                processName = process.ProcessName.ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                // Process not found or access denied
                _logger.LogWarning("Process access denied or not found for PID: {ProcessId}", window.ProcessId);
                return false;
            }
            catch (InvalidOperationException)
            {
                // Process has terminated
                _logger.LogWarning("Process has terminated for PID: {ProcessId}", window.ProcessId);
                return false;
            }

            // Known Visual Studio process names - convert to HashSet for performance
            var vsProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { "devenv", "visualstudio", "code" };
            
            if (!vsProcessNames.Any(p => processName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            // Additional validation based on class name or title
            if (ClassNameMappings.ContainsKey(window.ClassName))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(window.Title))
            {
                // Check for Visual Studio in title
                if (window.Title.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check for known window titles
                if (TitlePatternMappings.Keys.Any(pattern => 
                    window.Title.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating Visual Studio window: {Handle}", window.Handle);
            return false;
        }
    }

    private VisualStudioWindowType ClassifyWindowByInfo(VisualStudioWindow window)
    {
        // First try class name mapping
        if (ClassNameMappings.TryGetValue(window.ClassName, out var classType))
        {
            return classType;
        }

        // Then try title pattern matching
        if (!string.IsNullOrEmpty(window.Title))
        {
            foreach (var mapping in TitlePatternMappings)
            {
                if (window.Title.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping.Value;
                }
            }

            // Special handling for code editor windows (usually have file names)
            if (IsCodeEditorWindow(window))
            {
                return VisualStudioWindowType.CodeEditor;
            }

            // Special handling for XAML designer
            if (window.Title.Contains(".xaml", StringComparison.OrdinalIgnoreCase) ||
                window.ClassName.Contains("Xaml", StringComparison.OrdinalIgnoreCase))
            {
                return VisualStudioWindowType.XamlDesigner;
            }
        }

        return VisualStudioWindowType.Unknown;
    }

    private bool IsCodeEditorWindow(VisualStudioWindow window)
    {
        // Code editor windows typically have file extensions in their titles
        var codeExtensions = new[] { ".cs", ".vb", ".cpp", ".h", ".js", ".ts", ".html", ".css", ".json", ".xml" };
        
        return codeExtensions.Any(ext => 
            window.Title.Contains(ext, StringComparison.OrdinalIgnoreCase));
    }

    private void PostProcessWindowRelationships()
    {
        // Build parent-child relationships
        var windowLookup = _discoveredWindows.ToDictionary(w => w.Handle);
        
        foreach (var window in _discoveredWindows.ToList())
        {
            if (window.ParentHandle.HasValue && 
                windowLookup.TryGetValue(window.ParentHandle.Value, out var parent))
            {
                if (!parent.ChildWindows.Any(c => c.Handle == window.Handle))
                {
                    parent.ChildWindows.Add(window);
                }
            }
        }
    }

    private VisualStudioWindow? DetermineActiveWindow(IEnumerable<VisualStudioWindow> windows)
    {
        return windows.FirstOrDefault(w => w.IsActive) ?? 
               windows.FirstOrDefault(w => w.WindowType == VisualStudioWindowType.MainWindow);
    }

    private DockingLayout AnalyzeDockingLayout(VisualStudioWindow mainWindow, IEnumerable<VisualStudioWindow> allWindows)
    {
        var layout = new DockingLayout();
        
        try
        {
            var mainBounds = mainWindow.Bounds;
            var panels = allWindows.Where(w => w.WindowType != VisualStudioWindowType.MainWindow && 
                                              w.WindowType != VisualStudioWindowType.Unknown &&
                                              w.WindowType != VisualStudioWindowType.CodeEditor).ToList();

            foreach (var panel in panels)
            {
                var position = DeterminePanelPosition(panel.Bounds, mainBounds);
                
                switch (position)
                {
                    case PanelPosition.Left:
                        layout.LeftDockedPanels.Add(panel);
                        break;
                    case PanelPosition.Right:
                        layout.RightDockedPanels.Add(panel);
                        break;
                    case PanelPosition.Top:
                        layout.TopDockedPanels.Add(panel);
                        break;
                    case PanelPosition.Bottom:
                        layout.BottomDockedPanels.Add(panel);
                        break;
                    case PanelPosition.Floating:
                        layout.FloatingPanels.Add(panel);
                        break;
                }
            }

            // Find editor area
            layout.EditorArea = allWindows.FirstOrDefault(w => w.WindowType == VisualStudioWindowType.CodeEditor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing docking layout");
        }

        return layout;
    }

    private PanelPosition DeterminePanelPosition(WindowBounds panelBounds, WindowBounds mainBounds)
    {
        // Simple heuristic based on position relative to main window
        if (panelBounds.X < mainBounds.X - 50) // Left of main window
            return PanelPosition.Left;
        
        if (panelBounds.X > mainBounds.Right + 50) // Right of main window
            return PanelPosition.Right;
        
        if (panelBounds.Y < mainBounds.Y - 50) // Above main window
            return PanelPosition.Top;
        
        if (panelBounds.Y > mainBounds.Bottom + 50) // Below main window
            return PanelPosition.Bottom;
        
        // Check if it's within main window bounds (docked)
        if (panelBounds.X >= mainBounds.X && panelBounds.Right <= mainBounds.Right &&
            panelBounds.Y >= mainBounds.Y && panelBounds.Bottom <= mainBounds.Bottom)
        {
            // Further classify based on position within main window
            if (panelBounds.X <= mainBounds.X + 300) // Left side
                return PanelPosition.Left;
            
            if (panelBounds.Right >= mainBounds.Right - 300) // Right side
                return PanelPosition.Right;
            
            if (panelBounds.Bottom >= mainBounds.Bottom - 200) // Bottom
                return PanelPosition.Bottom;
        }

        return PanelPosition.Floating;
    }

    private enum PanelPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Floating
    }
}