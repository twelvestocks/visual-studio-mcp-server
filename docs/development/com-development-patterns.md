# COM Development Patterns for Visual Studio MCP Server

This document provides comprehensive guidance for COM interop development patterns specific to Visual Studio automation, with emphasis on the Phase 5 Advanced Visual Capture enhancements including P/Invoke integration, security-validated window enumeration, and resource disposal patterns.

## 📋 Overview

COM (Component Object Model) interop is fundamental to Visual Studio automation through the EnvDTE (Development Tools Environment) APIs. Phase 5 introduces sophisticated COM development patterns that combine traditional EnvDTE automation with advanced P/Invoke integration for comprehensive visual capture capabilities.

### 🎯 Pattern Categories

- **Traditional COM Patterns** - Core EnvDTE automation for Visual Studio control
- **P/Invoke Integration Patterns** - Windows API integration for visual capture
- **Security-Validated Patterns** - Process access security and exception handling
- **Resource Management Patterns** - Memory safety and cleanup procedures
- **Performance Optimization Patterns** - Efficient COM object lifecycle management

---

## 🏗️ Core COM Interop Patterns

### 1. Traditional EnvDTE Integration Pattern

#### Basic DTE Connection and Lifecycle Management
```csharp
public sealed class VisualStudioConnection : IDisposable
{
    private DTE? _dte;
    private bool _disposed = false;

    public async Task<bool> ConnectToVisualStudioAsync(int processId)
    {
        try
        {
            // 1. COM Object Creation with Security Context
            _dte = (DTE)Marshal.GetActiveObject($"VisualStudio.DTE.17.0:{processId}");
            
            // 2. Connection Validation
            if (_dte?.Solution == null)
            {
                _logger.LogWarning("Connected to VS instance {ProcessId} but no solution loaded", processId);
                return false;
            }

            // 3. Event Handler Registration
            RegisterEventHandlers();
            
            _logger.LogInformation("Successfully connected to Visual Studio instance {ProcessId}", processId);
            return true;
        }
        catch (COMException ex) when (ex.HResult == -2147221021) // MK_E_UNAVAILABLE
        {
            _logger.LogWarning("Visual Studio instance {ProcessId} not available for COM automation", processId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Visual Studio instance {ProcessId}", processId);
            return false;
        }
    }

    private void RegisterEventHandlers()
    {
        if (_dte?.Events != null)
        {
            // Document events for solution monitoring
            _dte.Events.DocumentEvents.DocumentOpened += OnDocumentOpened;
            _dte.Events.DocumentEvents.DocumentClosing += OnDocumentClosing;
            
            // Solution events for state tracking
            _dte.Events.SolutionEvents.Opened += OnSolutionOpened;
            _dte.Events.SolutionEvents.BeforeClosing += OnSolutionClosing;
            
            // Build events for compilation monitoring
            _dte.Events.BuildEvents.OnBuildBegin += OnBuildBegin;
            _dte.Events.BuildEvents.OnBuildDone += OnBuildDone;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                // 1. Unregister event handlers to prevent memory leaks
                UnregisterEventHandlers();
                
                // 2. Release COM object
                if (_dte != null)
                {
                    Marshal.ReleaseComObject(_dte);
                    _dte = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during COM object disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
```

### 2. Enhanced COM Exception Handling Pattern

#### Comprehensive COM Error Classification
```csharp
public static class COMExceptionHandler
{
    public static async Task<T> ExecuteWithCOMExceptionHandlingAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger logger,
        T fallbackValue = default(T))
    {
        try
        {
            return await operation();
        }
        catch (COMException ex)
        {
            return HandleCOMException(ex, operationName, logger, fallbackValue);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Access denied during {Operation}. Visual Studio may require elevation", operationName);
            return fallbackValue;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation during {Operation}. Visual Studio may be in transitional state", operationName);
            return fallbackValue;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during {Operation}", operationName);
            return fallbackValue;
        }
    }

    private static T HandleCOMException<T>(COMException ex, string operationName, ILogger logger, T fallbackValue)
    {
        switch (ex.HResult)
        {
            case -2147221021: // MK_E_UNAVAILABLE
                logger.LogWarning("COM object unavailable during {Operation}. Visual Studio may be shutting down", operationName);
                break;
                
            case -2147417846: // RPC_E_SERVER_DIED
                logger.LogError("Visual Studio process terminated during {Operation}", operationName);
                break;
                
            case -2147023170: // RPC_S_SERVER_UNAVAILABLE
                logger.LogWarning("Visual Studio COM server unavailable during {Operation}. Retrying may succeed", operationName);
                break;
                
            case -2147024809: // E_INVALIDARG
                logger.LogError("Invalid argument passed to COM method during {Operation}", operationName);
                break;
                
            case -2147024891: // E_ACCESSDENIED
                logger.LogWarning("Access denied to COM object during {Operation}. Check permissions", operationName);
                break;
                
            default:
                logger.LogError(ex, "COM exception (HRESULT: 0x{HResult:X8}) during {Operation}", ex.HResult, operationName);
                break;
        }
        
        return fallbackValue;
    }
}
```

---

## 🔗 P/Invoke Integration Patterns

### 1. Windows API Integration for Visual Capture

#### Safe P/Invoke Wrapper Pattern
```csharp
public static class Win32API
{
    // P/Invoke declarations with proper marshaling
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // Delegate for window enumeration callback
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Safe wrapper for GetWindowText with error handling
    public static string GetWindowTextSafe(IntPtr hWnd, int maxLength = 256)
    {
        try
        {
            var builder = new StringBuilder(maxLength);
            int result = GetWindowText(hWnd, builder, maxLength);
            
            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    throw new Win32Exception(error, $"GetWindowText failed for window {hWnd}");
                }
            }
            
            return builder.ToString();
        }
        catch (Exception ex)
        {
            // Log error but don't propagate - return empty string for robustness
            Debug.WriteLine($"GetWindowTextSafe failed for {hWnd}: {ex.Message}");
            return string.Empty;
        }
    }

    // Safe wrapper for GetClassName with error handling
    public static string GetClassNameSafe(IntPtr hWnd, int maxLength = 256)
    {
        try
        {
            var builder = new StringBuilder(maxLength);
            int result = GetClassName(hWnd, builder, maxLength);
            
            if (result == 0)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    throw new Win32Exception(error, $"GetClassName failed for window {hWnd}");
                }
            }
            
            return builder.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetClassNameSafe failed for {hWnd}: {ex.Message}");
            return string.Empty;
        }
    }
}
```

### 2. Hybrid COM/P/Invoke Integration Pattern

#### Combined EnvDTE and Windows API Approach
```csharp
public sealed class HybridVisualStudioService : IDisposable
{
    private readonly VisualStudioConnection _comConnection;
    private readonly WindowEnumerationService _windowService;
    private readonly ILogger<HybridVisualStudioService> _logger;

    public async Task<VisualStudioSession> CreateSessionAsync(int processId)
    {
        var session = new VisualStudioSession { ProcessId = processId };

        try
        {
            // 1. Establish COM connection for high-level automation
            session.COMAvailable = await _comConnection.ConnectToVisualStudioAsync(processId);
            
            // 2. Enumerate windows using P/Invoke for comprehensive discovery
            session.Windows = await _windowService.EnumerateVisualStudioWindowsAsync(processId);
            
            // 3. Cross-reference COM objects with P/Invoke window data
            if (session.COMAvailable)
            {
                await EnrichSessionWithCOMData(session);
            }
            
            // 4. Validate session completeness
            session.IsValid = ValidateSession(session);
            
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create hybrid Visual Studio session for process {ProcessId}", processId);
            session.Error = ex.Message;
            return session;
        }
    }

    private async Task EnrichSessionWithCOMData(VisualStudioSession session)
    {
        try
        {
            // Get solution information via COM
            if (_comConnection.DTE?.Solution != null)
            {
                session.SolutionPath = _comConnection.DTE.Solution.FullName;
                session.ProjectCount = _comConnection.DTE.Solution.Projects.Count;
                
                // Map COM documents to P/Invoke windows
                foreach (Document document in _comConnection.DTE.Documents)
                {
                    var matchingWindow = session.Windows.FirstOrDefault(w => 
                        w.Title.Contains(Path.GetFileName(document.Name)));
                    
                    if (matchingWindow != null)
                    {
                        matchingWindow.COMDocument = document;
                        matchingWindow.DocumentPath = document.FullName;
                    }
                }
            }
        }
        catch (COMException ex)
        {
            _logger.LogWarning(ex, "Failed to enrich session with COM data");
        }
    }
}
```

---

## 🛡️ Security-Validated Window Enumeration Patterns

### 1. Process Access Security Pattern

#### Comprehensive Process Validation with Exception Handling
```csharp
public sealed class SecureProcessValidator
{
    private readonly ILogger<SecureProcessValidator> _logger;
    private readonly HashSet<string> _knownVSProcessNames;

    public SecureProcessValidator(ILogger<SecureProcessValidator> logger)
    {
        _logger = logger;
        _knownVSProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "devenv", "devenv.exe",
            "Microsoft.ServiceHub.Controller",
            "ServiceHub.RoslynCodeAnalysisService",
            "ServiceHub.Host.CLR.AnyCPU",
            "PerfWatson2",
            "Microsoft.VisualStudio.Web.Host"
        };
    }

    public async Task<ProcessValidationResult> ValidateProcessSecurityAsync(uint processId, IntPtr windowHandle)
    {
        var result = new ProcessValidationResult
        {
            ProcessId = processId,
            WindowHandle = windowHandle,
            ValidationTimestamp = DateTime.UtcNow
        };

        try
        {
            // 1. Process Access Validation with Comprehensive Exception Handling
            using var process = await GetProcessSafelyAsync(processId);
            if (process == null)
            {
                result.IsValid = false;
                result.FailureReason = ProcessValidationFailureReason.ProcessNotFound;
                return result;
            }

            // 2. Process Name Validation
            result.ProcessName = process.ProcessName;
            result.IsVisualStudioProcess = _knownVSProcessNames.Contains(process.ProcessName);

            // 3. Security Context Validation
            result.HasRequiredPermissions = await ValidateProcessPermissionsAsync(process);

            // 4. Process State Validation
            result.IsResponding = !process.HasExited && process.Responding;

            // 5. Overall Validation Assessment
            result.IsValid = result.IsVisualStudioProcess && 
                           result.HasRequiredPermissions && 
                           result.IsResponding;

            if (result.IsValid)
            {
                _logger.LogDebug("Process validation successful for PID {ProcessId} ({ProcessName})", 
                    processId, result.ProcessName);
            }
            else
            {
                _logger.LogWarning("Process validation failed for PID {ProcessId}: {Reason}", 
                    processId, result.FailureReason);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during process validation for PID {ProcessId}", processId);
            result.IsValid = false;
            result.FailureReason = ProcessValidationFailureReason.UnexpectedError;
            result.ErrorDetails = ex.Message;
            return result;
        }
    }

    private async Task<Process?> GetProcessSafelyAsync(uint processId)
    {
        try
        {
            // Convert to int with overflow protection
            if (processId > int.MaxValue)
            {
                _logger.LogWarning("Process ID {ProcessId} exceeds int.MaxValue", processId);
                return null;
            }

            var process = Process.GetProcessById((int)processId);
            
            // Immediate access test to validate security permissions
            _ = process.ProcessName;
            
            return process;
        }
        catch (ArgumentException)
        {
            // Process not found - this is normal for terminated processes
            _logger.LogDebug("Process {ProcessId} not found (likely terminated)", processId);
            return null;
        }
        catch (InvalidOperationException)
        {
            // Process has exited - handle gracefully
            _logger.LogDebug("Process {ProcessId} has exited", processId);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied - security boundary
            _logger.LogWarning("Access denied to process {ProcessId}", processId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error accessing process {ProcessId}", processId);
            return null;
        }
    }

    private async Task<bool> ValidateProcessPermissionsAsync(Process process)
    {
        try
        {
            // Test various process properties to validate permissions
            _ = process.Id;
            _ = process.ProcessName;
            _ = process.StartTime;
            _ = process.Responding;
            
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error validating permissions for process {ProcessId}", process.Id);
            return false;
        }
    }
}
```

### 2. Security-First Window Enumeration Pattern

#### Comprehensive Window Discovery with Security Validation
```csharp
public sealed class SecurityValidatedWindowEnumerator
{
    private readonly SecureProcessValidator _processValidator;
    private readonly ILogger<SecurityValidatedWindowEnumerator> _logger;

    public async Task<IEnumerable<ValidatedWindow>> EnumerateWindowsSecurelyAsync()
    {
        var discoveredWindows = new List<ValidatedWindow>();
        var securityViolations = 0;
        var processedWindows = 0;

        try
        {
            // 1. Window Enumeration with Security Monitoring
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            
            await Task.Run(() =>
            {
                Win32API.EnumWindows((handle, _) =>
                {
                    try
                    {
                        processedWindows++;
                        
                        // 2. Individual Window Security Validation
                        var window = ProcessWindowSecurely(handle).Result;
                        if (window != null)
                        {
                            discoveredWindows.Add(window);
                        }
                        else
                        {
                            securityViolations++;
                        }
                    }
                    catch (SecurityException ex)
                    {
                        securityViolations++;
                        _logger.LogWarning(ex, "Security violation processing window {Handle}", handle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing window {Handle}", handle);
                    }
                    
                    return !cancellationTokenSource.Token.IsCancellationRequested;
                }, IntPtr.Zero);
            }, cancellationTokenSource.Token);

            _logger.LogInformation("Window enumeration complete: {ValidWindows} valid windows, {ProcessedWindows} total processed, {SecurityViolations} security violations",
                discoveredWindows.Count, processedWindows, securityViolations);

            return discoveredWindows;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Window enumeration timed out after 30 seconds");
            return discoveredWindows;
        }
    }

    private async Task<ValidatedWindow?> ProcessWindowSecurely(IntPtr windowHandle)
    {
        try
        {
            // 1. Basic Window Information Extraction
            var windowText = Win32API.GetWindowTextSafe(windowHandle);
            var className = Win32API.GetClassNameSafe(windowHandle);
            
            // 2. Process ID Extraction with Error Handling
            Win32API.GetWindowThreadProcessId(windowHandle, out uint processId);
            if (processId == 0)
            {
                _logger.LogDebug("Unable to get process ID for window {Handle}", windowHandle);
                return null;
            }

            // 3. Security Validation
            var validationResult = await _processValidator.ValidateProcessSecurityAsync(processId, windowHandle);
            if (!validationResult.IsValid)
            {
                _logger.LogDebug("Security validation failed for window {Handle} (PID: {ProcessId}): {Reason}",
                    windowHandle, processId, validationResult.FailureReason);
                return null;
            }

            // 4. Window Classification
            var windowType = ClassifyWindow(windowText, className, validationResult.ProcessName);

            return new ValidatedWindow
            {
                Handle = windowHandle,
                Title = windowText,
                ClassName = className,
                ProcessId = processId,
                ProcessName = validationResult.ProcessName,
                WindowType = windowType,
                SecurityValidation = validationResult,
                DiscoveredAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error processing window {Handle} securely", windowHandle);
            return null;
        }
    }
}
```

---

## 🧹 Resource Management and Disposal Patterns

### 1. Advanced RAII Pattern for COM and P/Invoke Resources

#### Comprehensive Resource Management
```csharp
public sealed class ManagedResourceContext : IDisposable
{
    private readonly List<IDisposable> _managedResources = new();
    private readonly List<IntPtr> _unmanagedHandles = new();
    private readonly Dictionary<IntPtr, Action<IntPtr>> _releaseActions = new();
    private readonly List<object> _comObjects = new();
    private readonly ILogger _logger;
    private bool _disposed = false;

    public ManagedResourceContext(ILogger logger)
    {
        _logger = logger;
    }

    // Managed resource acquisition
    public T AcquireManagedResource<T>(Func<T> resourceFactory) where T : IDisposable
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedResourceContext));

        var resource = resourceFactory();
        _managedResources.Add(resource);
        _logger.LogDebug("Acquired managed resource: {ResourceType}", typeof(T).Name);
        return resource;
    }

    // Unmanaged handle acquisition
    public IntPtr AcquireHandle(Func<IntPtr> handleFactory, Action<IntPtr> releaseAction, string handleType = "Handle")
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedResourceContext));

        var handle = handleFactory();
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to acquire {handleType}");

        _unmanagedHandles.Add(handle);
        _releaseActions[handle] = releaseAction;
        _logger.LogDebug("Acquired unmanaged handle: {HandleType} = {Handle}", handleType, handle);
        return handle;
    }

    // COM object acquisition
    public T AcquireCOMObject<T>(Func<T> comFactory) where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedResourceContext));

        var comObject = comFactory();
        if (comObject == null)
            throw new InvalidOperationException($"Failed to acquire COM object: {typeof(T).Name}");

        _comObjects.Add(comObject);
        _logger.LogDebug("Acquired COM object: {COMType}", typeof(T).Name);
        return comObject;
    }

    // Device context acquisition with automatic release
    public IntPtr AcquireDeviceContext(IntPtr windowHandle)
    {
        return AcquireHandle(
            () => Win32API.GetWindowDC(windowHandle),
            (dc) => Win32API.ReleaseDC(windowHandle, dc),
            $"DeviceContext for window {windowHandle}");
    }

    // Bitmap acquisition with automatic disposal
    public Bitmap AcquireBitmap(int width, int height, PixelFormat format = PixelFormat.Format32bppArgb)
    {
        return AcquireManagedResource(() => new Bitmap(width, height, format));
    }

    // Graphics acquisition with automatic disposal
    public Graphics AcquireGraphics(Image image)
    {
        return AcquireManagedResource(() => Graphics.FromImage(image));
    }

    public void Dispose()
    {
        if (_disposed) return;

        var errors = new List<Exception>();

        try
        {
            // 1. Release COM objects (in reverse order)
            foreach (var comObject in _comObjects.AsEnumerable().Reverse())
            {
                try
                {
                    if (comObject != null && Marshal.IsComObject(comObject))
                    {
                        var refCount = Marshal.ReleaseComObject(comObject);
                        _logger.LogDebug("Released COM object: {COMType}, RefCount: {RefCount}", 
                            comObject.GetType().Name, refCount);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error releasing COM object: {COMType}", comObject?.GetType().Name);
                }
            }

            // 2. Release unmanaged handles (in reverse order)
            foreach (var handle in _unmanagedHandles.AsEnumerable().Reverse())
            {
                try
                {
                    if (_releaseActions.TryGetValue(handle, out var releaseAction))
                    {
                        releaseAction(handle);
                        _logger.LogDebug("Released unmanaged handle: {Handle}", handle);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error releasing unmanaged handle: {Handle}", handle);
                }
            }

            // 3. Dispose managed resources (in reverse order)
            foreach (var resource in _managedResources.AsEnumerable().Reverse())
            {
                try
                {
                    resource?.Dispose();
                    _logger.LogDebug("Disposed managed resource: {ResourceType}", resource?.GetType().Name);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error disposing managed resource: {ResourceType}", resource?.GetType().Name);
                }
            }

            // 4. Force garbage collection if under memory pressure
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > 100_000_000) // 100MB threshold
            {
                _logger.LogInformation("High memory usage detected ({MemoryMB}MB), forcing garbage collection", 
                    currentMemory / 1_000_000);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        finally
        {
            _disposed = true;
            
            // Log any accumulated errors
            if (errors.Any())
            {
                var aggregateException = new AggregateException("Errors occurred during resource disposal", errors);
                _logger.LogError(aggregateException, "Resource disposal completed with {ErrorCount} errors", errors.Count);
            }
            else
            {
                _logger.LogDebug("Resource disposal completed successfully");
            }
        }
    }
}
```

### 2. COM Object Lifecycle Management Pattern

#### Sophisticated COM Object Pooling and Reuse
```csharp
public sealed class COMObjectPool<T> : IDisposable where T : class
{
    private readonly ConcurrentQueue<PooledCOMObject<T>> _pool = new();
    private readonly Func<T> _objectFactory;
    private readonly Action<T>? _objectValidator;
    private readonly int _maxPoolSize;
    private readonly TimeSpan _maxObjectAge;
    private readonly ILogger _logger;
    private readonly Timer _cleanupTimer;
    private int _totalObjects = 0;
    private bool _disposed = false;

    public COMObjectPool(
        Func<T> objectFactory,
        Action<T>? objectValidator = null,
        int maxPoolSize = 10,
        TimeSpan? maxObjectAge = null,
        ILogger? logger = null)
    {
        _objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        _objectValidator = objectValidator;
        _maxPoolSize = maxPoolSize;
        _maxObjectAge = maxObjectAge ?? TimeSpan.FromMinutes(10);
        _logger = logger ?? NullLogger.Instance;

        // Cleanup timer for expired objects
        _cleanupTimer = new Timer(CleanupExpiredObjects, null, _maxObjectAge, _maxObjectAge);
    }

    public async Task<PooledCOMObjectLease<T>> AcquireAsync()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(COMObjectPool<T>));

        // Try to get existing object from pool
        while (_pool.TryDequeue(out var pooledObject))
        {
            try
            {
                // Validate object is still usable
                if (IsObjectValid(pooledObject))
                {
                    _logger.LogDebug("Reusing pooled COM object: {ObjectType}", typeof(T).Name);
                    return new PooledCOMObjectLease<T>(pooledObject.Object, this);
                }
                else
                {
                    // Object is invalid, dispose it
                    await DisposePooledObjectAsync(pooledObject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating pooled COM object: {ObjectType}", typeof(T).Name);
                await DisposePooledObjectAsync(pooledObject);
            }
        }

        // No valid objects in pool, create new one
        try
        {
            var newObject = _objectFactory();
            Interlocked.Increment(ref _totalObjects);
            _logger.LogDebug("Created new COM object: {ObjectType}, Total: {TotalObjects}", 
                typeof(T).Name, _totalObjects);
            return new PooledCOMObjectLease<T>(newObject, this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create new COM object: {ObjectType}", typeof(T).Name);
            throw;
        }
    }

    public async Task ReturnAsync(T comObject)
    {
        if (_disposed || comObject == null) return;

        try
        {
            // Validate object before returning to pool
            _objectValidator?.Invoke(comObject);

            // Check pool size limit
            if (_pool.Count < _maxPoolSize)
            {
                var pooledObject = new PooledCOMObject<T>
                {
                    Object = comObject,
                    CreatedAt = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow
                };

                _pool.Enqueue(pooledObject);
                _logger.LogDebug("Returned COM object to pool: {ObjectType}, Pool size: {PoolSize}", 
                    typeof(T).Name, _pool.Count);
            }
            else
            {
                // Pool is full, dispose object
                await DisposeCOMObjectAsync(comObject);
                Interlocked.Decrement(ref _totalObjects);
                _logger.LogDebug("Disposed COM object (pool full): {ObjectType}", typeof(T).Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error returning COM object to pool: {ObjectType}", typeof(T).Name);
            await DisposeCOMObjectAsync(comObject);
            Interlocked.Decrement(ref _totalObjects);
        }
    }

    private bool IsObjectValid(PooledCOMObject<T> pooledObject)
    {
        // Check age
        if (DateTime.UtcNow - pooledObject.CreatedAt > _maxObjectAge)
            return false;

        try
        {
            // Validate COM object is still accessible
            if (Marshal.IsComObject(pooledObject.Object))
            {
                _objectValidator?.Invoke(pooledObject.Object);
                return true;
            }
        }
        catch
        {
            // Object validation failed
        }

        return false;
    }

    private void CleanupExpiredObjects(object? state)
    {
        try
        {
            var expiredObjects = new List<PooledCOMObject<T>>();
            var validObjects = new List<PooledCOMObject<T>>();

            // Drain pool to check all objects
            while (_pool.TryDequeue(out var pooledObject))
            {
                if (IsObjectValid(pooledObject))
                {
                    validObjects.Add(pooledObject);
                }
                else
                {
                    expiredObjects.Add(pooledObject);
                }
            }

            // Return valid objects to pool
            foreach (var validObject in validObjects)
            {
                _pool.Enqueue(validObject);
            }

            // Dispose expired objects
            foreach (var expiredObject in expiredObjects)
            {
                _ = Task.Run(async () => await DisposePooledObjectAsync(expiredObject));
            }

            if (expiredObjects.Any())
            {
                _logger.LogInformation("Cleaned up {ExpiredCount} expired COM objects from pool", 
                    expiredObjects.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during COM object pool cleanup");
        }
    }

    private async Task DisposePooledObjectAsync(PooledCOMObject<T> pooledObject)
    {
        await DisposeCOMObjectAsync(pooledObject.Object);
        Interlocked.Decrement(ref _totalObjects);
    }

    private async Task DisposeCOMObjectAsync(T comObject)
    {
        try
        {
            if (comObject is IDisposable disposable)
            {
                disposable.Dispose();
            }

            if (Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing COM object: {ObjectType}", typeof(T).Name);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();

        // Dispose all pooled objects
        while (_pool.TryDequeue(out var pooledObject))
        {
            _ = Task.Run(async () => await DisposePooledObjectAsync(pooledObject));
        }

        _disposed = true;
        _logger.LogInformation("COM object pool disposed: {ObjectType}, Final count: {TotalObjects}", 
            typeof(T).Name, _totalObjects);
    }
}

public sealed class PooledCOMObject<T>
{
    public required T Object { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastUsed { get; set; }
}

public sealed class PooledCOMObjectLease<T> : IDisposable where T : class
{
    private readonly COMObjectPool<T> _pool;
    private readonly T _object;
    private bool _disposed = false;

    public T Object => _disposed ? throw new ObjectDisposedException(nameof(PooledCOMObjectLease<T>)) : _object;

    internal PooledCOMObjectLease(T comObject, COMObjectPool<T> pool)
    {
        _object = comObject;
        _pool = pool;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _ = Task.Run(async () => await _pool.ReturnAsync(_object));
            _disposed = true;
        }
    }
}
```

---

## ⚡ Performance Optimization Patterns

### 1. Lazy Initialization and Caching Pattern

#### Intelligent COM Object Caching
```csharp
public sealed class LazyVisualStudioService : IDisposable
{
    private readonly Lazy<DTE> _lazyDTE;
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly ILogger _logger;
    private bool _disposed = false;

    public LazyVisualStudioService(int processId, ILogger logger)
    {
        _logger = logger;
        _lazyDTE = new Lazy<DTE>(() => InitializeDTE(processId), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<T> GetCachedValueAsync<T>(string key, Func<DTE, Task<T>> valueFactory, TimeSpan? cacheExpiration = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(LazyVisualStudioService));

        var cacheKey = $"{typeof(T).Name}:{key}";
        var expirationTime = cacheExpiration ?? TimeSpan.FromMinutes(5);

        // Check cache first
        if (_cache.TryGetValue(cacheKey, out var cachedItem) && cachedItem is CachedValue<T> cached)
        {
            if (DateTime.UtcNow - cached.CachedAt < expirationTime)
            {
                _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                return cached.Value;
            }
            else
            {
                _logger.LogDebug("Cache expired for {CacheKey}", cacheKey);
                _cache.TryRemove(cacheKey, out _);
            }
        }

        // Cache miss - acquire value with locking
        await _initializationLock.WaitAsync();
        try
        {
            // Double-check pattern
            if (_cache.TryGetValue(cacheKey, out cachedItem) && cachedItem is CachedValue<T> doubleChecked)
            {
                if (DateTime.UtcNow - doubleChecked.CachedAt < expirationTime)
                {
                    return doubleChecked.Value;
                }
            }

            // Acquire fresh value
            _logger.LogDebug("Cache miss for {CacheKey}, acquiring fresh value", cacheKey);
            var freshValue = await valueFactory(_lazyDTE.Value);
            
            var newCachedValue = new CachedValue<T>
            {
                Value = freshValue,
                CachedAt = DateTime.UtcNow
            };

            _cache.AddOrUpdate(cacheKey, newCachedValue, (_, _) => newCachedValue);
            return freshValue;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private DTE InitializeDTE(int processId)
    {
        try
        {
            _logger.LogInformation("Initializing DTE connection to process {ProcessId}", processId);
            var dte = (DTE)Marshal.GetActiveObject($"VisualStudio.DTE.17.0:{processId}");
            
            // Validate connection
            _ = dte.Version; // Trigger COM call to validate
            
            _logger.LogInformation("DTE connection established successfully");
            return dte;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DTE connection to process {ProcessId}", processId);
            throw;
        }
    }

    // Example usage methods with caching
    public async Task<string> GetSolutionPathAsync()
    {
        return await GetCachedValueAsync("SolutionPath", 
            async dte => dte.Solution?.FullName ?? string.Empty,
            TimeSpan.FromMinutes(30)); // Solution path changes infrequently
    }

    public async Task<int> GetProjectCountAsync()
    {
        return await GetCachedValueAsync("ProjectCount",
            async dte => dte.Solution?.Projects?.Count ?? 0,
            TimeSpan.FromMinutes(5)); // Project count may change more frequently
    }

    public async Task<IEnumerable<string>> GetOpenDocumentsAsync()
    {
        return await GetCachedValueAsync("OpenDocuments",
            async dte =>
            {
                var documents = new List<string>();
                if (dte.Documents != null)
                {
                    foreach (Document doc in dte.Documents)
                    {
                        documents.Add(doc.FullName);
                    }
                }
                return documents.AsEnumerable();
            },
            TimeSpan.FromSeconds(30)); // Documents change frequently
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _initializationLock.Dispose();
            
            if (_lazyDTE.IsValueCreated)
            {
                Marshal.ReleaseComObject(_lazyDTE.Value);
                _logger.LogInformation("Released DTE COM object");
            }

            _cache.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during LazyVisualStudioService disposal");
        }
        finally
        {
            _disposed = true;
        }
    }

    private sealed class CachedValue<T>
    {
        public required T Value { get; init; }
        public DateTime CachedAt { get; init; }
    }
}
```

### 2. Asynchronous COM Operation Pattern

#### Non-Blocking COM Operations with Task Coordination
```csharp
public sealed class AsyncCOMOperationManager
{
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly ILogger _logger;

    public AsyncCOMOperationManager(int maxConcurrency = Environment.ProcessorCount, ILogger? logger = null)
    {
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task<T> ExecuteAsync<T>(
        Func<T> comOperation,
        string operationName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var operationTimeout = timeout ?? TimeSpan.FromSeconds(30);
        
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        
        try
        {
            _logger.LogDebug("Starting async COM operation: {OperationName}", operationName);
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(operationTimeout);
            
            var stopwatch = Stopwatch.StartNew();
            
            var result = await Task.Run(() =>
            {
                try
                {
                    return comOperation();
                }
                catch (COMException ex)
                {
                    _logger.LogWarning(ex, "COM exception in operation {OperationName}: HRESULT 0x{HResult:X8}", 
                        operationName, ex.HResult);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected exception in operation {OperationName}", operationName);
                    throw;
                }
            }, timeoutCts.Token);
            
            stopwatch.Stop();
            _logger.LogDebug("Completed async COM operation: {OperationName} in {Duration}ms", 
                operationName, stopwatch.ElapsedMilliseconds);
            
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("COM operation {OperationName} cancelled by caller", operationName);
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("COM operation {OperationName} timed out after {Timeout}", 
                operationName, operationTimeout);
            throw new TimeoutException($"COM operation '{operationName}' timed out after {operationTimeout}");
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    // Batch operation support
    public async Task<IEnumerable<T>> ExecuteBatchAsync<T>(
        IEnumerable<Func<T>> operations,
        string batchName,
        int? maxConcurrency = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMaxConcurrency = maxConcurrency ?? Environment.ProcessorCount;
        var operationsList = operations.ToList();
        
        _logger.LogInformation("Starting batch COM operations: {BatchName} with {OperationCount} operations, max concurrency {MaxConcurrency}",
            batchName, operationsList.Count, effectiveMaxConcurrency);

        var semaphore = new SemaphoreSlim(effectiveMaxConcurrency, effectiveMaxConcurrency);
        var tasks = operationsList.Select(async (operation, index) =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteAsync(operation, $"{batchName}[{index}]", cancellationToken: cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        
        _logger.LogInformation("Completed batch COM operations: {BatchName}, {SuccessCount}/{TotalCount} successful",
            batchName, results.Length, operationsList.Count);

        return results;
    }
}
```

---

## 📊 Monitoring and Diagnostics Patterns

### 1. COM Operation Telemetry Pattern

#### Comprehensive Performance and Health Monitoring
```csharp
public sealed class COMOperationTelemetry : IDisposable
{
    private readonly IMetricsCollector _metricsCollector;
    private readonly ILogger _logger;
    private readonly Timer _healthCheckTimer;
    private readonly ConcurrentDictionary<string, OperationMetrics> _operationMetrics = new();

    public COMOperationTelemetry(IMetricsCollector metricsCollector, ILogger logger)
    {
        _metricsCollector = metricsCollector;
        _logger = logger;
        _healthCheckTimer = new Timer(ReportHealthMetrics, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<T> MonitorOperationAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        Dictionary<string, object>? additionalProperties = null)
    {
        var operationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        var startMemory = GC.GetTotalMemory(false);

        using var activity = Activity.StartActivity($"COM.{operationName}");
        activity?.SetTag("operation.id", operationId.ToString());
        activity?.SetTag("operation.name", operationName);

        try
        {
            _logger.LogDebug("Starting monitored COM operation: {OperationName} ({OperationId})", 
                operationName, operationId);

            var result = await operation();

            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);
            var memoryDelta = endMemory - startMemory;

            // Record successful operation metrics
            RecordOperationMetrics(operationName, stopwatch.Elapsed, memoryDelta, true, null, additionalProperties);

            activity?.SetTag("operation.success", true);
            activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("operation.memory_delta_bytes", memoryDelta);

            _logger.LogInformation("COM operation completed successfully: {OperationName} in {Duration}ms, Memory delta: {MemoryDelta}KB",
                operationName, stopwatch.ElapsedMilliseconds, memoryDelta / 1024);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);
            var memoryDelta = endMemory - startMemory;

            // Record failed operation metrics
            RecordOperationMetrics(operationName, stopwatch.Elapsed, memoryDelta, false, ex, additionalProperties);

            activity?.SetTag("operation.success", false);
            activity?.SetTag("operation.error", ex.GetType().Name);
            activity?.SetTag("operation.duration_ms", stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex, "COM operation failed: {OperationName} after {Duration}ms",
                operationName, stopwatch.ElapsedMilliseconds);

            throw;
        }
    }

    private void RecordOperationMetrics(
        string operationName,
        TimeSpan duration,
        long memoryDelta,
        bool success,
        Exception? exception,
        Dictionary<string, object>? additionalProperties)
    {
        var metrics = _operationMetrics.AddOrUpdate(operationName,
            new OperationMetrics { OperationName = operationName },
            (_, existing) => existing);

        lock (metrics)
        {
            metrics.TotalOperations++;
            metrics.TotalDuration += duration;
            
            if (success)
            {
                metrics.SuccessfulOperations++;
            }
            else
            {
                metrics.FailedOperations++;
                if (exception != null)
                {
                    metrics.ExceptionTypes[exception.GetType().Name] = 
                        metrics.ExceptionTypes.GetValueOrDefault(exception.GetType().Name, 0) + 1;
                }
            }

            metrics.TotalMemoryDelta += memoryDelta;
            metrics.LastOperationAt = DateTime.UtcNow;

            // Update min/max duration
            if (duration < metrics.MinDuration || metrics.MinDuration == TimeSpan.Zero)
                metrics.MinDuration = duration;
            if (duration > metrics.MaxDuration)
                metrics.MaxDuration = duration;
        }

        // Send metrics to collector
        _ = Task.Run(async () =>
        {
            try
            {
                await _metricsCollector.RecordOperationAsync(new OperationRecord
                {
                    OperationName = operationName,
                    Duration = duration,
                    MemoryDelta = memoryDelta,
                    Success = success,
                    Exception = exception?.GetType().Name,
                    AdditionalProperties = additionalProperties ?? new Dictionary<string, object>(),
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record operation metrics for {OperationName}", operationName);
            }
        });
    }

    private void ReportHealthMetrics(object? state)
    {
        try
        {
            var healthReport = GenerateHealthReport();
            
            _logger.LogInformation("COM Health Report: {TotalOperations} operations, " +
                "Success rate: {SuccessRate:P2}, Average duration: {AvgDuration}ms",
                healthReport.TotalOperations,
                healthReport.OverallSuccessRate,
                healthReport.AverageDuration.TotalMilliseconds);

            // Send health metrics
            _ = Task.Run(async () =>
            {
                try
                {
                    await _metricsCollector.RecordHealthReportAsync(healthReport);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to record health report metrics");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating COM health report");
        }
    }

    private COMHealthReport GenerateHealthReport()
    {
        var allMetrics = _operationMetrics.Values.ToList();
        
        return new COMHealthReport
        {
            ReportTimestamp = DateTime.UtcNow,
            TotalOperations = allMetrics.Sum(m => m.TotalOperations),
            SuccessfulOperations = allMetrics.Sum(m => m.SuccessfulOperations),
            FailedOperations = allMetrics.Sum(m => m.FailedOperations),
            OverallSuccessRate = allMetrics.Sum(m => m.TotalOperations) > 0 
                ? (double)allMetrics.Sum(m => m.SuccessfulOperations) / allMetrics.Sum(m => m.TotalOperations)
                : 1.0,
            AverageDuration = allMetrics.Any() 
                ? TimeSpan.FromTicks((long)allMetrics.Average(m => m.AverageDuration.Ticks))
                : TimeSpan.Zero,
            TotalMemoryDelta = allMetrics.Sum(m => m.TotalMemoryDelta),
            OperationMetrics = allMetrics.ToDictionary(m => m.OperationName, m => m)
        };
    }

    public void Dispose()
    {
        _healthCheckTimer?.Dispose();
        
        // Final health report
        try
        {
            var finalReport = GenerateHealthReport();
            _logger.LogInformation("Final COM Health Report: {TotalOperations} total operations, " +
                "Success rate: {SuccessRate:P2}",
                finalReport.TotalOperations,
                finalReport.OverallSuccessRate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error generating final health report");
        }
    }
}

public sealed class OperationMetrics
{
    public required string OperationName { get; init; }
    public long TotalOperations { get; set; }
    public long SuccessfulOperations { get; set; }
    public long FailedOperations { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan MinDuration { get; set; }
    public TimeSpan MaxDuration { get; set; }
    public long TotalMemoryDelta { get; set; }
    public DateTime LastOperationAt { get; set; }
    public Dictionary<string, int> ExceptionTypes { get; } = new();

    public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations / TotalOperations : 1.0;
    public TimeSpan AverageDuration => TotalOperations > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalOperations) : TimeSpan.Zero;
    public long AverageMemoryDelta => TotalOperations > 0 ? TotalMemoryDelta / TotalOperations : 0;
}

public sealed class COMHealthReport
{
    public DateTime ReportTimestamp { get; init; }
    public long TotalOperations { get; init; }
    public long SuccessfulOperations { get; init; }
    public long FailedOperations { get; init; }
    public double OverallSuccessRate { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public long TotalMemoryDelta { get; init; }
    public Dictionary<string, OperationMetrics> OperationMetrics { get; init; } = new();
}
```

---

## 🚀 Best Practices and Guidelines

### Development Guidelines

#### 1. **COM Object Lifecycle**
- Always use `using` statements or explicit disposal for COM objects
- Release COM objects in reverse order of acquisition
- Never cache COM objects across method boundaries without proper lifecycle management
- Use object pooling for frequently accessed COM objects

#### 2. **Exception Handling**
- Always handle `COMException` with specific HRESULT checks
- Implement fallback mechanisms for critical COM operations
- Log exceptions with sufficient context for debugging
- Never suppress exceptions without logging

#### 3. **Performance Optimization**
- Use lazy initialization for expensive COM connections
- Implement caching for frequently accessed data
- Limit concurrent COM operations to prevent resource exhaustion
- Monitor memory usage and implement cleanup thresholds

#### 4. **Security Considerations**
- Always validate process access before enumeration
- Implement security boundary respect patterns
- Use comprehensive exception handling for process access
- Log security violations for monitoring

### Testing Patterns

#### 1. **Unit Testing COM Operations**
```csharp
[TestClass]
public class COMOperationTests
{
    private Mock<ILogger> _mockLogger;
    private TestCOMObject _testCOMObject;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
        _testCOMObject = new TestCOMObject();
    }

    [TestMethod]
    public async Task COMOperation_WithValidObject_CompletesSuccessfully()
    {
        // Arrange
        using var resourceContext = new ManagedResourceContext(_mockLogger.Object);
        var comObject = resourceContext.AcquireCOMObject(() => _testCOMObject);

        // Act
        var result = await COMExceptionHandler.ExecuteWithCOMExceptionHandlingAsync(
            () => Task.FromResult(comObject.GetTestValue()),
            "GetTestValue",
            _mockLogger.Object);

        // Assert
        Assert.IsNotNull(result);
        _mockLogger.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), 
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("GetTestValue")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Never); // No errors should be logged
    }

    [TestMethod]
    public async Task COMOperation_WithCOMException_HandlesGracefully()
    {
        // Arrange
        var mockCOMObject = new Mock<ITestCOMInterface>();
        mockCOMObject.Setup(x => x.GetTestValue())
            .Throws(new COMException("Test COM exception", -2147221021)); // MK_E_UNAVAILABLE

        // Act
        var result = await COMExceptionHandler.ExecuteWithCOMExceptionHandlingAsync(
            () => Task.FromResult(mockCOMObject.Object.GetTestValue()),
            "GetTestValue",
            _mockLogger.Object,
            "fallback");

        // Assert
        Assert.AreEqual("fallback", result);
        _mockLogger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("COM object unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
```

### Deployment Considerations

#### 1. **Environment Configuration**
- Ensure proper COM registration for target Visual Studio versions
- Configure appropriate logging levels for production monitoring
- Set memory pressure thresholds based on system capabilities
- Implement health check endpoints for monitoring

#### 2. **Monitoring Setup**
- Configure structured logging with correlation IDs
- Set up performance counter collection
- Implement alerting for COM operation failures
- Monitor memory usage patterns and cleanup effectiveness

---

*This document provides comprehensive guidance for COM development patterns in the Visual Studio MCP Server. These patterns ensure robust, performant, and maintainable COM interop code that integrates seamlessly with the Phase 5 Advanced Visual Capture capabilities.*