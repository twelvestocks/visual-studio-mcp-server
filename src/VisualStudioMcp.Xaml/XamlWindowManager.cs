using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<int, DateTime> _processLastScan;
    private readonly ConcurrentDictionary<int, XamlDesignerWindow[]> _processWindowCache;
    private readonly SemaphoreSlim _enumerationSemaphore;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromSeconds(30);
    private readonly object _activationLock = new();

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
        _processLastScan = new ConcurrentDictionary<int, DateTime>();
        _processWindowCache = new ConcurrentDictionary<int, XamlDesignerWindow[]>();
        _enumerationSemaphore = new SemaphoreSlim(1, 1); // Only allow one enumeration at a time
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
            // Check cache first to avoid unnecessary enumeration
            if (TryGetCachedWindows(vsProcessId, out var cachedWindows))
            {
                _logger.LogDebug("Returning cached designer windows for process {ProcessId}: {Count} windows", 
                    vsProcessId, cachedWindows.Length);
                return cachedWindows;
            }

            // Use semaphore to prevent concurrent enumerations
            await _enumerationSemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check cache after acquiring semaphore (another thread might have populated it)
                if (TryGetCachedWindows(vsProcessId, out cachedWindows))
                {
                    _logger.LogDebug("Cache populated by another thread for process {ProcessId}", vsProcessId);
                    return cachedWindows;
                }

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
                
                // Find active XAML documents and their designer windows (run in background thread)
                var designerWindows = await Task.Run(() => FindDesignerWindowsForInstanceSafe(vsProcessId)).ConfigureAwait(false);

                // Cache the results
                UpdateCache(vsProcessId, designerWindows);

                _logger.LogInformation("Found and cached {Count} XAML designer windows for process {ProcessId}", 
                    designerWindows.Length, vsProcessId);
                return designerWindows;
            }
            finally
            {
                _enumerationSemaphore.Release();
            }
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
            // Use lock to prevent concurrent activation attempts which can cause issues
            lock (_activationLock)
            {
                try
                {
                    // Validate window still exists before attempting activation
                    if (!ValidateWindowOwnership(designerWindow.Handle, designerWindow.ProcessId))
                    {
                        _logger.LogWarning("Window validation failed during activation: {Handle}", designerWindow.Handle);
                        
                        // Invalidate cache for this process since window may have changed
                        InvalidateProcessCache(designerWindow.ProcessId);
                        return false;
                    }

                    // Use Visual Studio automation to activate the designer
                    return ActivateDesignerViaAutomation(designerWindow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error activating XAML designer for {FilePath}", designerWindow.XamlFilePath);
                    
                    // Invalidate cache on error in case window state has changed
                    InvalidateProcessCache(designerWindow.ProcessId);
                    return false;
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Thread-safe method to find designer windows for a specific Visual Studio instance.
    /// </summary>
    private XamlDesignerWindow[] FindDesignerWindowsForInstanceSafe(int processId)
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

            // Use a HashSet to prevent duplicate windows
            var processedWindows = new HashSet<IntPtr>();

            foreach (var className in designerClassNames)
            {
                try
                {
                    var hwnd = FindWindowEx(IntPtr.Zero, IntPtr.Zero, className, null);
                    while (hwnd != IntPtr.Zero)
                    {
                        // Skip if we've already processed this window handle
                        if (!processedWindows.Add(hwnd))
                        {
                            hwnd = FindWindowEx(IntPtr.Zero, hwnd, className, null);
                            continue;
                        }

                        if (IsWindowVisible(hwnd))
                        {
                            GetWindowThreadProcessId(hwnd, out var windowProcessId);
                            
                            if (windowProcessId == processId)
                            {
                                var windowText = GetWindowTextSafe(hwnd);
                                var designerWindow = CreateDesignerWindowInfoSafe(hwnd, windowText, processId);
                                
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error enumerating windows for class {ClassName}", className);
                    // Continue with next class name
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating designer windows for process {ProcessId}", processId);
        }

        return designerWindows.ToArray();
    }

    /// <summary>
    /// Tries to get cached windows for a process if they haven't expired.
    /// </summary>
    private bool TryGetCachedWindows(int processId, out XamlDesignerWindow[] windows)
    {
        windows = Array.Empty<XamlDesignerWindow>();
        
        if (!_processWindowCache.TryGetValue(processId, out var cachedWindows))
        {
            return false;
        }

        if (!_processLastScan.TryGetValue(processId, out var lastScan))
        {
            return false;
        }

        if (DateTime.UtcNow - lastScan > _cacheExpiration)
        {
            // Cache expired, remove entries
            _processWindowCache.TryRemove(processId, out _);
            _processLastScan.TryRemove(processId, out _);
            return false;
        }

        windows = cachedWindows;
        return true;
    }

    /// <summary>
    /// Updates the cache with new window results.
    /// </summary>
    private void UpdateCache(int processId, XamlDesignerWindow[] windows)
    {
        _processWindowCache.AddOrUpdate(processId, windows, (_, _) => windows);
        _processLastScan.AddOrUpdate(processId, DateTime.UtcNow, (_, _) => DateTime.UtcNow);
    }

    /// <summary>
    /// Invalidates the cache for a specific process.
    /// </summary>
    private void InvalidateProcessCache(int processId)
    {
        _processWindowCache.TryRemove(processId, out _);
        _processLastScan.TryRemove(processId, out _);
        _logger.LogDebug("Invalidated window cache for process {ProcessId}", processId);
    }

    /// <summary>
    /// Thread-safe version of GetWindowText with better error handling.
    /// </summary>
    private string GetWindowTextSafe(IntPtr hwnd)
    {
        try
        {
            var text = new StringBuilder(512); // Increased buffer size
            var length = GetWindowText(hwnd, text, text.Capacity);
            
            if (length == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    _logger.LogDebug("GetWindowText returned no text for handle {Handle}, error: {Error}", hwnd, error);
                }
            }
            
            return text.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting window text for handle {Handle}", hwnd);
            return string.Empty;
        }
    }

    /// <summary>
    /// Thread-safe version of CreateDesignerWindowInfo with enhanced validation.
    /// </summary>
    private XamlDesignerWindow? CreateDesignerWindowInfoSafe(IntPtr hwnd, string title, int processId)
    {
        try
        {
            // Enhanced validation with multiple checks
            if (!ValidateWindowOwnership(hwnd, processId))
            {
                _logger.LogDebug("Window ownership validation failed for handle {Handle}", hwnd);
                return null;
            }

            // Additional validation to ensure window is still valid
            if (!IsWindowVisible(hwnd))
            {
                _logger.LogDebug("Window no longer visible: {Handle}", hwnd);
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
            _logger.LogDebug(ex, "Error creating designer window info for handle {Handle}", hwnd);
            return null;
        }
    }

    /// <summary>
    /// Clears all cached window information. Call when Visual Studio instances change.
    /// </summary>
    public void ClearCache()
    {
        _processWindowCache.Clear();
        _processLastScan.Clear();
        _logger.LogInformation("Cleared all window cache entries");
    }

    /// <summary>
    /// Invalidates cache for a specific process. Call when a VS process exits.
    /// </summary>
    public void InvalidateCache(int processId)
    {
        InvalidateProcessCache(processId);
    }

    /// <summary>
    /// Gets statistics about the current cache state.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return new CacheStatistics
        {
            CachedProcessCount = _processWindowCache.Count,
            TotalCachedWindows = _processWindowCache.Values.Sum(windows => windows.Length),
            CacheExpirationTimespan = _cacheExpiration
        };
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

/// <summary>
/// Statistics about the window manager cache state.
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// Number of processes currently cached.
    /// </summary>
    public int CachedProcessCount { get; set; }

    /// <summary>
    /// Total number of windows across all cached processes.
    /// </summary>
    public int TotalCachedWindows { get; set; }

    /// <summary>
    /// Cache expiration timespan.
    /// </summary>
    public TimeSpan CacheExpirationTimespan { get; set; }
}