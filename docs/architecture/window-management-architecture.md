# Window Management Architecture

This document provides a comprehensive overview of the Phase 5 Advanced Visual Capture window management system architecture, covering P/Invoke integration, security validation, classification algorithms, and performance optimization strategies.

## 📋 Executive Summary

Phase 5 implements a sophisticated window management system that transforms raw Windows API data into intelligent Visual Studio window classification and spatial analysis. The architecture emphasizes security, performance, and maintainability while providing comprehensive visual context for Claude Code integration.

### 🎯 Key Architectural Goals
- **Security-First Design** - Process access validation with graceful failure handling
- **Performance Optimization** - Sub-500ms window enumeration for standard layouts
- **Scalability** - Support for complex multi-monitor Visual Studio configurations  
- **Maintainability** - Clear separation of concerns with dependency injection
- **Extensibility** - Plugin-ready architecture for future window types

---

## 🏗️ System Architecture Overview

### High-Level Component Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                    MCP Server Layer                         │
├─────────────────────────────────────────────────────────────┤
│  VisualStudioMcpServer                                      │
│  ├─ vs_capture_window        ┌─────────────────────────────┐│
│  ├─ vs_capture_full_ide      │     Tool Routing Layer      ││
│  └─ vs_analyse_visual_state  └─────────────────────────────┘│
└─────────────────┬───────────────────────────────────────────┘
                  │
┌─────────────────┴───────────────────────────────────────────┐
│                Service Layer                                │
├─────────────────────────────────────────────────────────────┤
│  IWindowClassificationService │  IImagingService             │
│  ├─ DiscoverVSWindowsAsync    │  ├─ CaptureWindowAsync       │
│  ├─ ClassifyWindowAsync       │  ├─ CaptureFullIdeAsync      │
│  ├─ AnalyzeLayoutAsync        │  └─ SpecializedCapture       │
│  └─ FindWindowsByTypeAsync    │                              │
└─────────────────┬───────────────────────────────────────────┘
                  │
┌─────────────────┴───────────────────────────────────────────┐
│            Core Window Management Layer                     │
├─────────────────────────────────────────────────────────────┤
│  WindowClassificationService                               │
│  ├─ Window Enumeration Engine                              │
│  ├─ Security Validation Layer                              │
│  ├─ Classification Algorithm Engine                        │
│  └─ Layout Analysis Engine                                 │
└─────────────────┬───────────────────────────────────────────┘
                  │
┌─────────────────┴───────────────────────────────────────────┐
│              P/Invoke Integration Layer                     │
├─────────────────────────────────────────────────────────────┤
│  GdiResourceManager                                         │
│  ├─ EnumWindows / EnumChildWindows                         │
│  ├─ GetWindowText / GetClassName                           │
│  ├─ GetWindowRect / IsWindowVisible                        │
│  └─ Process Security Validation                            │
└─────────────────┬───────────────────────────────────────────┘
                  │
┌─────────────────┴───────────────────────────────────────────┐
│                Windows API Layer                            │
├─────────────────────────────────────────────────────────────┤
│  user32.dll │ kernel32.dll │ gdi32.dll │ ntdll.dll         │
│  ├─ Window Management APIs                                  │
│  ├─ Process Access APIs                                     │
│  └─ Graphics and Device Context APIs                       │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 P/Invoke Integration Architecture

### Windows API Integration Strategy

The system leverages carefully selected Windows APIs through P/Invoke for maximum performance and security:

#### Core P/Invoke Declarations
**File:** `src/VisualStudioMcp.Imaging/GdiResourceManager.cs`

```csharp
// Window Enumeration APIs
[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

// Window Property Retrieval
[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

// Window State and Positioning
[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool IsWindowVisible(IntPtr hWnd);

// Process Information
[DllImport("user32.dll", SetLastError = true)]
internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
```

### P/Invoke Security Patterns

#### 1. Error Handling Strategy
```csharp
private string GetWindowTextSafely(IntPtr windowHandle, int maxLength = 256)
{
    try
    {
        var buffer = new StringBuilder(maxLength);
        int length = GetWindowText(windowHandle, buffer, maxLength);
        
        if (length == 0)
        {
            int error = Marshal.GetLastWin32Error();
            if (error != 0)
            {
                _logger.LogWarning("GetWindowText failed for handle {Handle}: Error {Error}", 
                    windowHandle, error);
            }
            return string.Empty;
        }
        
        return buffer.ToString();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Exception in GetWindowTextSafely for handle {Handle}", windowHandle);
        return string.Empty;
    }
}
```

#### 2. Resource Management Patterns
```csharp
// RAII pattern for Windows resources
public sealed class SafeWindowEnumerator : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly List<IntPtr> _discoveredHandles;
    private bool _disposed;

    public SafeWindowEnumerator(TimeSpan timeout)
    {
        _cancellationTokenSource = new CancellationTokenSource(timeout);
        _discoveredHandles = new List<IntPtr>();
    }

    public async Task<IEnumerable<IntPtr>> EnumerateWindowsAsync()
    {
        return await Task.Run(() =>
        {
            EnumWindows(WindowEnumerationCallback, IntPtr.Zero);
            return _discoveredHandles.AsEnumerable();
        }, _cancellationTokenSource.Token);
    }

    private bool WindowEnumerationCallback(IntPtr handle, IntPtr lParam)
    {
        if (_cancellationTokenSource.Token.IsCancellationRequested)
            return false;

        _discoveredHandles.Add(handle);
        return true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Dispose();
            _discoveredHandles?.Clear();
            _disposed = true;
        }
    }
}
```

### API Selection Rationale

| API Function | Purpose | Alternative Considered | Selection Reason |
|-------------|---------|----------------------|------------------|
| `EnumWindows` | Top-level window enumeration | `FindWindow` | Complete enumeration needed |
| `EnumChildWindows` | Child window discovery | Manual traversal | Efficient recursive enumeration |
| `GetWindowText` | Window title retrieval | `WM_GETTEXT` message | Direct API, better performance |
| `GetClassName` | Window class identification | Registry lookup | Real-time class resolution |
| `GetWindowRect` | Window positioning | `GetWindowPlacement` | Simple bounds sufficient |
| `IsWindowVisible` | Visibility checking | Manual style bit checking | Reliable system implementation |

---

## 🔍 Window Classification System

### Classification Algorithm Architecture

The window classification system employs a multi-stage approach for accurate Visual Studio window type detection:

#### 1. Classification Pipeline

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Raw Window    │    │   Security      │    │  Classification │
│   Handle        │───▶│   Validation    │───▶│   Algorithm     │
│                 │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                 │                       │
                                 ▼                       ▼
                  ┌─────────────────┐    ┌─────────────────┐
                  │   Process       │    │   Classified    │
                  │   Access        │    │   Window        │
                  │   Validation    │    │   Object        │
                  └─────────────────┘    └─────────────────┘
```

#### 2. Multi-Strategy Classification
**File:** `src/VisualStudioMcp.Imaging/WindowClassificationService.cs`

```csharp
private VisualStudioWindowType ClassifyWindowByStrategies(VisualStudioWindow window)
{
    // Strategy 1: Title Pattern Matching (Primary)
    var titleType = ClassifyByTitle(window.Title);
    if (titleType != VisualStudioWindowType.Unknown)
    {
        _logger.LogDebug("Window classified by title: {Title} -> {Type}", window.Title, titleType);
        return titleType;
    }

    // Strategy 2: Class Name Analysis (Secondary)
    var classType = ClassifyByClassName(window.ClassName);
    if (classType != VisualStudioWindowType.Unknown)
    {
        _logger.LogDebug("Window classified by class: {ClassName} -> {Type}", window.ClassName, classType);
        return classType;
    }

    // Strategy 3: Parent-Child Relationship Analysis (Tertiary)
    var relationshipType = ClassifyByRelationship(window);
    if (relationshipType != VisualStudioWindowType.Unknown)
    {
        _logger.LogDebug("Window classified by relationship: {Handle} -> {Type}", window.Handle, relationshipType);
        return relationshipType;
    }

    // Strategy 4: Content Analysis (When Available)
    var contentType = ClassifyByContent(window);
    if (contentType != VisualStudioWindowType.Unknown)
    {
        _logger.LogDebug("Window classified by content analysis: {Handle} -> {Type}", window.Handle, contentType);
        return contentType;
    }

    return VisualStudioWindowType.Unknown;
}
```

### Title Pattern Classification

#### Pattern Database Architecture
```csharp
private static readonly Dictionary<VisualStudioWindowType, List<Regex>> TitlePatterns = new()
{
    [VisualStudioWindowType.SolutionExplorer] = new List<Regex>
    {
        new(@"^Solution Explorer.*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@".*Solution.*Explorer.*", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    },
    [VisualStudioWindowType.CodeEditor] = new List<Regex>
    {
        new(@".*\.(cs|vb|cpp|h|js|ts|html|css|xml|json)(\s|\s*\*)?(\s-.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^.*\.(cs|vb|cpp|h|js|ts|html|css|xml|json)\s+\[.*\]$", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    },
    [VisualStudioWindowType.XamlDesigner] = new List<Regex>
    {
        new(@".*\.xaml.*\[Design\].*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@".*\.xaml.*Designer.*", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    },
    [VisualStudioWindowType.ErrorList] = new List<Regex>
    {
        new(@"^Error List.*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@".*Error List.*\(\d+.*\).*", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    }
    // ... additional patterns for all 20+ window types
};
```

### Class Name Classification

#### Windows Class Hierarchy
```csharp
private static readonly Dictionary<VisualStudioWindowType, HashSet<string>> ClassNamePatterns = new()
{
    [VisualStudioWindowType.MainWindow] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "HwndWrapper[DefaultDomain;;]",
        "Window",
        "VsDebugWnd"
    },
    [VisualStudioWindowType.CodeEditor] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "VsTextEditPane",
        "VsCodeWindow",
        "AnyCode-TextEditorTextPanelControl"
    },
    [VisualStudioWindowType.SolutionExplorer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "VsSolutionExplorerPane",
        "GenericPane"
    }
    // ... comprehensive class mappings
};
```

### Performance Optimization

#### Caching Strategy
```csharp
private readonly Dictionary<IntPtr, (VisualStudioWindowType Type, DateTime CachedAt)> _classificationCache 
    = new Dictionary<IntPtr, (VisualStudioWindowType, DateTime)>();

private const int CacheExpiryMinutes = 5;

private VisualStudioWindowType GetCachedClassification(IntPtr windowHandle)
{
    if (_classificationCache.TryGetValue(windowHandle, out var cached))
    {
        if (DateTime.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(CacheExpiryMinutes))
        {
            return cached.Type;
        }
        
        // Remove expired entry
        _classificationCache.Remove(windowHandle);
    }
    
    return VisualStudioWindowType.Unknown;
}
```

---

## 🔐 Security Validation Layer

### Process Access Security Architecture

The security validation layer implements defense-in-depth strategies for safe process access during window enumeration:

#### 1. Process Security Validation Pipeline

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Window        │    │   Process ID    │    │   Security      │
│   Handle        │───▶│   Extraction    │───▶│   Validation    │
│                 │    │                 │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                 │                       │
                                 ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Exception     │    │   Process       │    │   Classification│
│   Handling      │◄───│   Access        │    │   Decision      │
│                 │    │   Attempt       │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

#### 2. Security Validation Implementation
```csharp
private bool ValidateProcessAccess(VisualStudioWindow window)
{
    try
    {
        // Attempt secure process access with timeout
        using var process = Process.GetProcessById((int)window.ProcessId);
        
        // Validate process is still running
        if (process.HasExited)
        {
            _logger.LogWarning("Process {ProcessId} has exited during validation", window.ProcessId);
            return false;
        }
        
        // Security validation: Check if process is accessible
        var processName = process.ProcessName.ToLowerInvariant();
        
        // Validate against known Visual Studio process names
        var isValidVSProcess = _validProcessNames.Contains(processName);
        
        if (isValidVSProcess)
        {
            _logger.LogDebug("Validated VS process: {ProcessName} (PID: {ProcessId})", 
                processName, window.ProcessId);
        }
        
        return isValidVSProcess;
    }
    catch (ArgumentException)
    {
        // Process not found - this is normal for terminated processes
        _logger.LogWarning("Process access denied or not found for PID: {ProcessId}", window.ProcessId);
        return false;
    }
    catch (InvalidOperationException)
    {
        // Process has terminated - handle gracefully
        _logger.LogWarning("Process has terminated for PID: {ProcessId}", window.ProcessId);
        return false;
    }
    catch (Win32Exception ex)
    {
        // Access denied - system security boundary
        _logger.LogWarning("Access denied to process {ProcessId}: {Message}", window.ProcessId, ex.Message);
        return false;
    }
    catch (Exception ex)
    {
        // Unexpected exception - log and continue
        _logger.LogError(ex, "Unexpected error validating process {ProcessId}", window.ProcessId);
        return false;
    }
}
```

### Security Boundaries and Policies

#### Process Access Security Matrix

| Process Type | Access Level | Security Action | Logging Level |
|-------------|--------------|----------------|---------------|
| **Visual Studio** | Full Access | Allow classification | Debug |
| **System Process** | Restricted | Deny with warning | Warning |
| **Terminated Process** | No Access | Graceful failure | Warning |
| **Protected Process** | No Access | Security boundary respect | Warning |
| **Unknown Process** | Validation | Investigate and log | Information |

#### Security Event Monitoring
```csharp
private void MonitorSecurityEvents()
{
    var securityMetrics = new
    {
        ProcessAccessDenials = _securityViolations.Count(v => v.Type == "ACCESS_DENIED"),
        ProcessTerminations = _securityViolations.Count(v => v.Type == "PROCESS_TERMINATED"),
        UnknownProcesses = _securityViolations.Count(v => v.Type == "UNKNOWN_PROCESS"),
        TotalViolations = _securityViolations.Count
    };
    
    if (securityMetrics.TotalViolations > 10)
    {
        _logger.LogWarning("High security violation count: {@SecurityMetrics}", securityMetrics);
    }
    
    // Report security metrics for monitoring
    _logger.LogInformation("Security validation summary: {@SecurityMetrics}", securityMetrics);
}
```

---

## ⚡ Performance Architecture

### Performance Optimization Strategies

The window management system implements multiple performance optimization layers:

#### 1. Asynchronous Processing Architecture

```csharp
public async Task<IEnumerable<VisualStudioWindow>> DiscoverVSWindowsAsync()
{
    var stopwatch = Stopwatch.StartNew();
    var discoveredWindows = new ConcurrentBag<VisualStudioWindow>();
    
    try
    {
        // Parallel window enumeration with controlled concurrency
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount * 2);
        var tasks = new List<Task>();
        
        await Task.Run(() =>
        {
            EnumWindows(async (handle, _) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var window = await ProcessWindowAsync(handle);
                            if (window != null && IsVisualStudioWindow(window))
                            {
                                discoveredWindows.Add(window);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
                catch
                {
                    semaphore.Release();
                    throw;
                }
                
                return true;
            }, IntPtr.Zero);
        });
        
        // Wait for all window processing to complete
        await Task.WhenAll(tasks);
        
        var result = discoveredWindows.ToList();
        stopwatch.Stop();
        
        _logger.LogInformation("Discovered {WindowCount} VS windows in {ElapsedMs}ms", 
            result.Count, stopwatch.ElapsedMilliseconds);
            
        return result;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during window discovery");
        return Enumerable.Empty<VisualStudioWindow>();
    }
}
```

#### 2. Caching and Memoization

```csharp
private readonly MemoryCache _windowCache;
private readonly MemoryCache _classificationCache;

public WindowClassificationService(ILogger<WindowClassificationService> logger)
{
    _logger = logger;
    
    // Configure caches with appropriate expiry
    _windowCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 1000, // Max 1000 cached windows
        CompactionPercentage = 0.25 // Remove 25% when full
    });
    
    _classificationCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 500, // Max 500 cached classifications
        CompactionPercentage = 0.2
    });
}

private async Task<VisualStudioWindow?> GetCachedWindowAsync(IntPtr handle)
{
    var cacheKey = $"window_{handle}";
    
    if (_windowCache.TryGetValue(cacheKey, out VisualStudioWindow cachedWindow))
    {
        _logger.LogDebug("Using cached window data for handle {Handle}", handle);
        return cachedWindow;
    }
    
    var window = await CreateWindowFromHandleAsync(handle);
    if (window != null)
    {
        _windowCache.Set(cacheKey, window, TimeSpan.FromMinutes(5));
    }
    
    return window;
}
```

### Performance Monitoring and Metrics

#### Real-time Performance Tracking
```csharp
private readonly Dictionary<string, PerformanceCounter> _performanceCounters = new()
{
    ["WindowEnumerationTime"] = new PerformanceCounter(),
    ["ClassificationTime"] = new PerformanceCounter(), 
    ["MemoryUsage"] = new PerformanceCounter(),
    ["CacheHitRate"] = new PerformanceCounter()
};

private void RecordPerformanceMetrics(string operation, TimeSpan duration, long memoryUsed = 0)
{
    if (_performanceCounters.TryGetValue(operation, out var counter))
    {
        counter.Record(duration.TotalMilliseconds, memoryUsed);
    }
    
    // Alert on performance degradation
    if (duration.TotalMilliseconds > GetPerformanceThreshold(operation))
    {
        _logger.LogWarning("Performance alert: {Operation} took {DurationMs}ms (threshold: {ThresholdMs}ms)", 
            operation, duration.TotalMilliseconds, GetPerformanceThreshold(operation));
    }
}
```

### Performance Targets and SLA

| Operation | Target Time | Maximum Time | Memory Limit | Success Rate |
|-----------|-------------|-------------|--------------|--------------|
| **Window Enumeration** | <300ms | 500ms | <10MB | >99% |
| **Window Classification** | <50ms | 100ms | <1MB | >99.5% |
| **Layout Analysis** | <1000ms | 2000ms | <25MB | >98% |
| **Process Validation** | <100ms | 200ms | <2MB | >99% |
| **Cache Operations** | <10ms | 50ms | <500KB | >99.9% |

---

## 🔄 Layout Analysis Engine

### Spatial Analysis Architecture

The layout analysis engine provides comprehensive Visual Studio window arrangement understanding:

#### 1. Docking Detection Algorithm

```csharp
private DockingLayout AnalyzeDockingLayout(IEnumerable<VisualStudioWindow> windows)
{
    var layout = new DockingLayout();
    var mainWindow = windows.FirstOrDefault(w => w.WindowType == VisualStudioWindowType.MainWindow);
    
    if (mainWindow == null)
    {
        _logger.LogWarning("No main window found for docking analysis");
        return layout;
    }
    
    var mainBounds = mainWindow.Bounds;
    const int dockingTolerance = 20; // Pixels tolerance for docking detection
    
    foreach (var window in windows.Where(w => w.WindowType != VisualStudioWindowType.MainWindow))
    {
        var bounds = window.Bounds;
        
        // Left docking detection
        if (Math.Abs(bounds.X - mainBounds.X) <= dockingTolerance && 
            bounds.Y >= mainBounds.Y && 
            bounds.Y + bounds.Height <= mainBounds.Y + mainBounds.Height)
        {
            layout.LeftDockedPanels.Add(window);
            _logger.LogDebug("Window {Title} detected as left-docked", window.Title);
            continue;
        }
        
        // Right docking detection  
        if (Math.Abs((bounds.X + bounds.Width) - (mainBounds.X + mainBounds.Width)) <= dockingTolerance &&
            bounds.Y >= mainBounds.Y && 
            bounds.Y + bounds.Height <= mainBounds.Y + mainBounds.Height)
        {
            layout.RightDockedPanels.Add(window);
            _logger.LogDebug("Window {Title} detected as right-docked", window.Title);
            continue;
        }
        
        // Top docking detection
        if (Math.Abs(bounds.Y - mainBounds.Y) <= dockingTolerance && 
            bounds.X >= mainBounds.X && 
            bounds.X + bounds.Width <= mainBounds.X + mainBounds.Width)
        {
            layout.TopDockedPanels.Add(window);
            _logger.LogDebug("Window {Title} detected as top-docked", window.Title);
            continue;
        }
        
        // Bottom docking detection
        if (Math.Abs((bounds.Y + bounds.Height) - (mainBounds.Y + mainBounds.Height)) <= dockingTolerance &&
            bounds.X >= mainBounds.X && 
            bounds.X + bounds.Width <= mainBounds.X + mainBounds.Width)
        {
            layout.BottomDockedPanels.Add(window);
            _logger.LogDebug("Window {Title} detected as bottom-docked", window.Title);
            continue;
        }
        
        // Floating window (not docked to any side)
        layout.FloatingPanels.Add(window);
        _logger.LogDebug("Window {Title} detected as floating", window.Title);
    }
    
    return layout;
}
```

#### 2. Relationship Mapping
```csharp
private void MapWindowRelationships(List<VisualStudioWindow> windows)
{
    foreach (var window in windows)
    {
        // Find child windows
        EnumChildWindows(window.Handle, (childHandle, _) =>
        {
            var childWindow = windows.FirstOrDefault(w => w.Handle == childHandle);
            if (childWindow != null)
            {
                childWindow.ParentHandle = window.Handle;
                window.ChildWindows.Add(childWindow);
                _logger.LogDebug("Mapped child relationship: {Parent} -> {Child}", 
                    window.Title, childWindow.Title);
            }
            return true;
        }, IntPtr.Zero);
    }
}
```

### Advanced Layout Analysis

#### Overlap Detection Algorithm
```csharp
private List<WindowOverlap> DetectWindowOverlaps(IEnumerable<VisualStudioWindow> windows)
{
    var overlaps = new List<WindowOverlap>();
    var windowList = windows.ToList();
    
    for (int i = 0; i < windowList.Count; i++)
    {
        for (int j = i + 1; j < windowList.Count; j++)
        {
            var window1 = windowList[i];
            var window2 = windowList[j];
            
            var overlap = CalculateOverlap(window1.Bounds, window2.Bounds);
            if (overlap.Area > 0)
            {
                overlaps.Add(new WindowOverlap
                {
                    Window1 = window1,
                    Window2 = window2,
                    OverlapArea = overlap.Area,
                    OverlapPercentage = overlap.Area / (double)Math.Min(
                        window1.Bounds.Width * window1.Bounds.Height,
                        window2.Bounds.Width * window2.Bounds.Height) * 100
                });
            }
        }
    }
    
    return overlaps;
}
```

---

## 🚀 Deployment and Integration Architecture

### Dependency Injection Configuration

#### Service Registration
**File:** `src/VisualStudioMcp.Server/Program.cs`

```csharp
public static void ConfigureServices(IServiceCollection services)
{
    // Core window management services
    services.AddSingleton<IWindowClassificationService, WindowClassificationService>();
    services.AddSingleton<IImagingService, ImagingService>();
    
    // Configure logging with structured output
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.AddDebug();
        builder.SetMinimumLevel(LogLevel.Information);
    });
    
    // Configure memory cache for performance optimization
    services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000;
        options.CompactionPercentage = 0.25;
    });
    
    // Configure HTTP client for potential web integrations
    services.AddHttpClient();
    
    // Configure background services
    services.AddHostedService<WindowCacheMaintenanceService>();
}
```

#### Configuration Management
```csharp
public class WindowManagementOptions
{
    public int EnumerationTimeoutSeconds { get; set; } = 30;
    public int ClassificationCacheExpiryMinutes { get; set; } = 5;
    public int MaxConcurrentWindows { get; set; } = 100;
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public LogLevel SecurityEventLogLevel { get; set; } = LogLevel.Warning;
    public int DockingTolerancePixels { get; set; } = 20;
}
```

### Integration Points

#### MCP Server Integration
```csharp
public class VisualStudioMcpServer
{
    private readonly IWindowClassificationService _windowService;
    private readonly IImagingService _imagingService;
    private readonly ILogger<VisualStudioMcpServer> _logger;

    public VisualStudioMcpServer(
        IWindowClassificationService windowService,
        IImagingService imagingService, 
        ILogger<VisualStudioMcpServer> logger)
    {
        _windowService = windowService;
        _imagingService = imagingService;
        _logger = logger;
    }

    [McpTool("vs_capture_window")]
    public async Task<McpToolResult> CaptureWindowAsync(McpToolCall call)
    {
        try
        {
            var args = call.Arguments.ToObject<CaptureWindowArgs>();
            
            // Validate arguments
            var validation = ValidateCaptureWindowArgs(args);
            if (!validation.IsValid)
            {
                return McpToolResult.Error(validation.ErrorMessage);
            }
            
            // Execute capture with performance monitoring
            var stopwatch = Stopwatch.StartNew();
            var result = await _imagingService.CaptureWindowAsync(args);
            stopwatch.Stop();
            
            // Record performance metrics
            _logger.LogInformation("Window capture completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            
            return McpToolResult.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_capture_window tool");
            return McpToolResult.Error($"Capture failed: {ex.Message}");
        }
    }
}
```

---

## 📊 Monitoring and Observability

### Metrics Collection Architecture

#### Performance Metrics
```csharp
public class WindowManagementMetrics
{
    public int TotalWindowsEnumerated { get; set; }
    public TimeSpan AverageEnumerationTime { get; set; }
    public int ClassificationCacheHitRate { get; set; }
    public int SecurityViolationCount { get; set; }
    public long MemoryUsageBytes { get; set; }
    public int TimeoutEventCount { get; set; }
    public Dictionary<VisualStudioWindowType, int> WindowTypeDistribution { get; set; }
}
```

#### Health Checks
```csharp
public class WindowManagementHealthCheck : IHealthCheck
{
    private readonly IWindowClassificationService _windowService;
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test basic window enumeration
            var stopwatch = Stopwatch.StartNew();
            var windows = await _windowService.DiscoverVSWindowsAsync();
            stopwatch.Stop();
            
            var data = new Dictionary<string, object>
            {
                ["WindowsFound"] = windows.Count(),
                ["EnumerationTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["MemoryUsageMB"] = GC.GetTotalMemory(false) / 1024 / 1024
            };
            
            // Health assessment
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded("Window enumeration is slow", null, data);
            }
            
            if (windows.Count() == 0)
            {
                return HealthCheckResult.Unhealthy("No Visual Studio windows found", null, data);
            }
            
            return HealthCheckResult.Healthy("Window management system operational", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Window management system failure", ex);
        }
    }
}
```

---

## 🔮 Future Architecture Enhancements

### Planned Architectural Improvements

#### 1. Plugin Architecture
```csharp
public interface IWindowClassificationPlugin
{
    string Name { get; }
    int Priority { get; }
    bool CanClassify(VisualStudioWindow window);
    Task<VisualStudioWindowType> ClassifyAsync(VisualStudioWindow window);
}

public class PluginBasedClassificationService : IWindowClassificationService
{
    private readonly IEnumerable<IWindowClassificationPlugin> _plugins;
    
    public async Task<VisualStudioWindowType> ClassifyWindowAsync(IntPtr windowHandle)
    {
        var window = await CreateWindowFromHandle(windowHandle);
        
        // Execute plugins in priority order
        foreach (var plugin in _plugins.OrderBy(p => p.Priority))
        {
            if (plugin.CanClassify(window))
            {
                return await plugin.ClassifyAsync(window);
            }
        }
        
        return VisualStudioWindowType.Unknown;
    }
}
```

#### 2. Event-Driven Architecture
```csharp
public interface IWindowEventSource
{
    event EventHandler<WindowEventArgs> WindowCreated;
    event EventHandler<WindowEventArgs> WindowDestroyed;
    event EventHandler<WindowEventArgs> WindowMoved;
    event EventHandler<WindowEventArgs> WindowActivated;
}

public class WindowEventPublisher : IWindowEventSource
{
    public event EventHandler<WindowEventArgs> WindowCreated;
    public event EventHandler<WindowEventArgs> WindowDestroyed;
    public event EventHandler<WindowEventArgs> WindowMoved;
    public event EventHandler<WindowEventArgs> WindowActivated;
    
    // Windows hook integration for real-time events
    private void InstallWindowsHooks()
    {
        // SetWinEventHook implementation for real-time window events
    }
}
```

#### 3. Machine Learning Integration
```csharp
public interface IMLClassificationService
{
    Task<VisualStudioWindowType> PredictWindowTypeAsync(WindowFeatures features);
    Task TrainModelAsync(IEnumerable<LabeledWindow> trainingData);
    Task<double> GetClassificationConfidence(VisualStudioWindowType prediction);
}

public class WindowFeatures
{
    public string Title { get; set; }
    public string ClassName { get; set; }
    public Rectangle Bounds { get; set; }
    public string ProcessName { get; set; }
    public Dictionary<string, object> ExtendedProperties { get; set; }
}
```

---

## 📚 References and Standards

### Technical Standards Compliance
- **Microsoft COM Guidelines** - All COM interop follows Microsoft best practices
- **Windows API Standards** - Proper P/Invoke declarations and error handling
- **C# Coding Standards** - Follows Microsoft C# coding conventions
- **.NET Performance Guidelines** - Optimized for high-performance scenarios

### Architecture Patterns Applied
- **Repository Pattern** - Window data access abstraction
- **Strategy Pattern** - Multiple classification algorithms
- **Observer Pattern** - Event-driven window monitoring
- **Factory Pattern** - Window object creation
- **RAII Pattern** - Resource management and cleanup

### Documentation References
- [Windows API Documentation](https://docs.microsoft.com/en-us/windows/win32/api/)
- [P/Invoke Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/performance/)
- [Dependency Injection in .NET](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

---

*This architecture document is maintained as part of the Phase 5 implementation and will be updated as the system evolves.*