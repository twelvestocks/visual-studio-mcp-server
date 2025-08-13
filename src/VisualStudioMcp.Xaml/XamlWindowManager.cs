using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Manages XAML designer window discovery, enumeration, and activation.
/// Handles all Windows API interactions for finding and managing designer windows.
/// </summary>
public class XamlWindowManager
{
    private readonly ILogger<XamlWindowManager> _logger;
    private readonly IVisualStudioService _vsService;

    // Windows API declarations with security constraints
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHwnd, IntPtr childAfterHwnd, string? className, string? windowText);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    /// <summary>
    /// Initializes a new instance of the XamlWindowManager class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="vsService">Visual Studio service for instance management.</param>
    public XamlWindowManager(ILogger<XamlWindowManager> logger, IVisualStudioService vsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _vsService = vsService ?? throw new ArgumentNullException(nameof(vsService));
    }

    /// <summary>
    /// Finds XAML designer windows in the specified Visual Studio process.
    /// </summary>
    /// <param name="vsProcessId">Visual Studio process ID.</param>
    /// <returns>Array of designer window information.</returns>
    public async Task<XamlDesignerWindow[]> FindXamlDesignerWindowsAsync(int vsProcessId)
    {
        _logger.LogInformation("Finding XAML designer windows for VS process: {ProcessId}", vsProcessId);

        try
        {
            // Validate that the VS instance exists
            var instances = await _vsService.GetRunningInstancesAsync().ConfigureAwait(false);
            var targetInstance = instances.FirstOrDefault(i => i.ProcessId == vsProcessId);

            if (targetInstance == null)
            {
                _logger.LogWarning("Visual Studio instance with PID {ProcessId} not found", vsProcessId);
                return Array.Empty<XamlDesignerWindow>();
            }

            // Connect to the VS instance to ensure it's accessible
            var vsInstance = await _vsService.ConnectToInstanceAsync(vsProcessId).ConfigureAwait(false);
            
            var designerWindows = new List<XamlDesignerWindow>();
            
            // Find active XAML documents and their designer windows (run in background thread)
            await Task.Run(() =>
            {
                try
                {
                    designerWindows.AddRange(FindDesignerWindowsForInstance(vsProcessId));
                    _logger.LogInformation("Found {Count} XAML designer windows", designerWindows.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finding designer windows for process {ProcessId}", vsProcessId);
                }
            }).ConfigureAwait(false);

            return designerWindows.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding XAML designer windows for process {ProcessId}", vsProcessId);
            return Array.Empty<XamlDesignerWindow>();
        }
    }

    /// <summary>
    /// Gets the active XAML designer window for the currently active document.
    /// </summary>
    /// <returns>Designer window information if found, null otherwise.</returns>
    public async Task<XamlDesignerWindow?> GetActiveDesignerWindowAsync()
    {
        _logger.LogInformation("Getting active XAML designer window");

        try
        {
            var instances = await _vsService.GetRunningInstancesAsync().ConfigureAwait(false);
            
            foreach (var instance in instances)
            {
                var designerWindows = await FindXamlDesignerWindowsAsync(instance.ProcessId);
                var activeDesigner = designerWindows.FirstOrDefault(w => w.IsActive);
                
                if (activeDesigner != null)
                {
                    _logger.LogInformation("Found active designer for file: {FilePath}", activeDesigner.XamlFilePath);
                    return activeDesigner;
                }
            }

            _logger.LogInformation("No active XAML designer window found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active XAML designer window");
            return null;
        }
    }

    /// <summary>
    /// Activates a specific XAML designer window.
    /// </summary>
    /// <param name="designerWindow">The designer window to activate.</param>
    /// <returns>True if successfully activated, false otherwise.</returns>
    public async Task<bool> ActivateDesignerWindowAsync(XamlDesignerWindow designerWindow)
    {
        _logger.LogInformation("Activating XAML designer for: {FilePath}", designerWindow.XamlFilePath);

        return await Task.Run(() =>
        {
            try
            {
                // Use Visual Studio automation to activate the designer
                return ActivateDesignerViaAutomation(designerWindow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating XAML designer for {FilePath}", designerWindow.XamlFilePath);
                return false;
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds designer windows for a specific Visual Studio instance.
    /// </summary>
    private List<XamlDesignerWindow> FindDesignerWindowsForInstance(int processId)
    {
        var designerWindows = new List<XamlDesignerWindow>();

        try
        {
            // Look for XAML designer window class names
            var designerClassNames = new[]
            {
                "Microsoft.XamlDesignerHost.DesignerPane",
                "XamlDesignerPane",
                "DesignerView",
                "Microsoft.VisualStudio.DesignTools.Xaml.DesignerPane"
            };

            foreach (var className in designerClassNames)
            {
                var hwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, null);
                while (hwnd != IntPtr.Zero)
                {
                    if (IsWindowVisible(hwnd))
                    {
                        GetWindowThreadProcessId(hwnd, out var windowProcessId);
                        
                        if (windowProcessId == processId)
                        {
                            var windowText = GetWindowText(hwnd);
                            var designerWindow = CreateDesignerWindowInfo(hwnd, windowText, processId);
                            
                            if (designerWindow != null)
                            {
                                designerWindows.Add(designerWindow);
                                _logger.LogDebug("Found XAML designer window: {Title}", designerWindow.Title);
                            }
                        }
                    }

                    hwnd = FindWindowEx(IntPtr.Zero, hwnd, className, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating designer windows for process {ProcessId}", processId);
        }

        return designerWindows;
    }

    /// <summary>
    /// Gets the text content of a window with proper error handling.
    /// </summary>
    private string GetWindowText(IntPtr hwnd)
    {
        try
        {
            var text = new StringBuilder(256);
            var length = GetWindowText(hwnd, text, text.Capacity);
            
            if (length == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    _logger.LogWarning("GetWindowText failed for handle {Handle}, error: {Error}", hwnd, error);
                }
            }
            
            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting window text for handle {Handle}", hwnd);
            return string.Empty;
        }
    }

    /// <summary>
    /// Creates designer window information from a window handle.
    /// </summary>
    private XamlDesignerWindow? CreateDesignerWindowInfo(IntPtr hwnd, string title, int processId)
    {
        try
        {
            // Validate window ownership for security
            if (!ValidateWindowOwnership(hwnd, processId))
            {
                _logger.LogWarning("Window ownership validation failed for handle {Handle}", hwnd);
                return null;
            }

            // Try to determine the XAML file path from the window title or context
            var xamlFilePath = ExtractXamlFilePathFromContext(title, processId);
            
            return new XamlDesignerWindow
            {
                Handle = hwnd,
                Title = title,
                ProcessId = processId,
                XamlFilePath = xamlFilePath ?? "Unknown",
                IsActive = IsActiveWindow(hwnd),
                IsVisible = IsWindowVisible(hwnd)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating designer window info for handle {Handle}", hwnd);
            return null;
        }
    }

    /// <summary>
    /// Validates window ownership to prevent unauthorized access.
    /// </summary>
    private bool ValidateWindowOwnership(IntPtr hwnd, int expectedProcessId)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out var actualProcessId);
            return actualProcessId == expectedProcessId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating window ownership for handle {Handle}", hwnd);
            return false; // Fail secure
        }
    }

    /// <summary>
    /// Extracts XAML file path from window context using Visual Studio automation.
    /// </summary>
    private string? ExtractXamlFilePathFromContext(string windowTitle, int processId)
    {
        try
        {
            // This would need to use EnvDTE to get the active document
            // For now, return null and implement in subsequent enhancements
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting XAML file path from context");
            return null;
        }
    }

    /// <summary>
    /// Checks if a window is currently active/focused.
    /// </summary>
    private bool IsActiveWindow(IntPtr hwnd)
    {
        // Simple implementation - could be enhanced with GetForegroundWindow
        return IsWindowVisible(hwnd);
    }

    /// <summary>
    /// Activates designer using Visual Studio automation.
    /// </summary>
    private bool ActivateDesignerViaAutomation(XamlDesignerWindow designerWindow)
    {
        try
        {
            // This would use EnvDTE to activate the designer
            // Implementation will be added when EnvDTE integration is complete
            _logger.LogInformation("Designer activation via automation not yet implemented");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in designer activation via automation");
            return false;
        }
    }
}