# Memory Management Guide for Visual Studio MCP Server

This comprehensive guide provides detailed strategies, patterns, and best practices for memory management in the Visual Studio MCP Server, with specific focus on the Phase 5 Advanced Visual Capture implementation including memory pressure monitoring, resource cleanup patterns, and performance profiling techniques.

## 📋 Executive Summary

Memory management is critical for the Visual Studio MCP Server, particularly given the large-scale visual capture operations that can consume 100MB+ of memory. Phase 5 introduces sophisticated memory management patterns that ensure system stability, prevent OutOfMemoryException crashes, and maintain optimal performance through intelligent resource allocation and cleanup strategies.

### 🎯 Memory Management Objectives

- **System Stability** - Prevent memory pressure crashes and system instability
- **Predictable Performance** - Consistent memory usage patterns with bounded resource consumption
- **Leak Prevention** - Comprehensive resource cleanup preventing memory leaks
- **Intelligent Allocation** - Smart memory allocation with pressure monitoring
- **Observability** - Detailed memory usage tracking and diagnostics

---

## 🏗️ Memory Architecture Overview

### Memory Management System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                        Memory Management Orchestration                         │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Memory Pressure Monitor │ Allocation Predictor │ Cleanup Coordinator        │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                           Memory Protection Layers                             │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Predictive Assessment │ Active Monitoring │ Emergency Recovery               │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                        Resource Management Services                            │
├─────────────────────────────────────────────────────────────────────────────────┤
│  RAII Pattern │ Object Pooling │ Cleanup Scheduling │ Leak Detection        │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                          Platform Integration                                  │
├─────────────────────────────────────────────────────────────────────────────────┤
│  GC Integration │ P/Invoke Resources │ COM Object Lifecycle │ GDI Management │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Key Components

#### 1. **Memory Pressure Monitor**
- Real-time memory usage tracking
- Predictive allocation assessment
- System-wide memory pressure detection
- Intelligent threshold management

#### 2. **Resource Lifecycle Manager**
- RAII pattern implementation
- Automatic resource cleanup
- Leak detection and prevention
- Performance optimization

#### 3. **Allocation Strategy Engine**
- Smart memory allocation decisions
- Risk assessment algorithms
- Fallback strategy selection
- Performance impact analysis

---

## 🧠 Memory Pressure Monitoring Implementation

### 1. Advanced Memory Pressure Detection

#### Three-Tier Monitoring System
```csharp
public sealed class AdvancedMemoryPressureMonitor : IDisposable
{
    private readonly ILogger<AdvancedMemoryPressureMonitor> _logger;
    private readonly Timer _monitoringTimer;
    private readonly PerformanceCounter _availableMemoryCounter;
    private readonly PerformanceCounter _processMemoryCounter;
    private readonly ConcurrentQueue<MemorySnapshot> _memoryHistory;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    // Configurable thresholds
    private readonly MemoryThresholds _thresholds;

    public sealed class MemoryThresholds
    {
        public long WarningThreshold { get; init; } = 50_000_000;     // 50MB
        public long CriticalThreshold { get; init; } = 100_000_000;   // 100MB
        public long EmergencyThreshold { get; init; } = 200_000_000;  // 200MB
        public long SystemMemoryWarning { get; init; } = 500_000_000; // 500MB process memory
        public double AvailableMemoryWarning { get; init; } = 0.1;    // 10% available system memory
    }

    public AdvancedMemoryPressureMonitor(
        MemoryThresholds? thresholds = null,
        ILogger<AdvancedMemoryPressureMonitor>? logger = null)
    {
        _logger = logger ?? NullLogger<AdvancedMemoryPressureMonitor>.Instance;
        _thresholds = thresholds ?? new MemoryThresholds();
        _memoryHistory = new ConcurrentQueue<MemorySnapshot>();

        // Initialize performance counters
        _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        _processMemoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);

        // Start monitoring timer
        _monitoringTimer = new Timer(MonitorMemoryPressure, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        _logger.LogInformation("Memory pressure monitor initialized with thresholds: Warning={WarningMB}MB, Critical={CriticalMB}MB, Emergency={EmergencyMB}MB",
            _thresholds.WarningThreshold / 1_000_000,
            _thresholds.CriticalThreshold / 1_000_000,
            _thresholds.EmergencyThreshold / 1_000_000);
    }

    public async Task<MemoryAllocationAssessment> AssessAllocationAsync(long requestedBytes, string operationName)
    {
        await _operationLock.WaitAsync();
        try
        {
            var currentSnapshot = await CaptureMemorySnapshotAsync();
            var assessment = new MemoryAllocationAssessment
            {
                RequestedBytes = requestedBytes,
                OperationName = operationName,
                CurrentMemoryUsage = currentSnapshot.ProcessMemoryUsage,
                AvailableSystemMemory = currentSnapshot.AvailableSystemMemory,
                AssessmentTimestamp = DateTime.UtcNow
            };

            // 1. Predictive Memory Usage Calculation
            var projectedUsage = currentSnapshot.ProcessMemoryUsage + requestedBytes;
            assessment.ProjectedMemoryUsage = projectedUsage;

            // 2. Risk Level Assessment
            assessment.RiskLevel = CalculateRiskLevel(requestedBytes, projectedUsage, currentSnapshot);

            // 3. Recommendation Generation
            assessment.Recommendation = GenerateRecommendation(assessment.RiskLevel, requestedBytes, currentSnapshot);

            // 4. Allocation Decision
            assessment.AllowAllocation = ShouldAllowAllocation(assessment.RiskLevel);

            // 5. Performance Impact Prediction
            assessment.PredictedPerformanceImpact = PredictPerformanceImpact(assessment.RiskLevel, requestedBytes);

            _logger.LogDebug("Memory allocation assessment for {Operation}: {RequestedMB}MB requested, Risk: {RiskLevel}, Allowed: {Allowed}",
                operationName, requestedBytes / 1_000_000, assessment.RiskLevel, assessment.AllowAllocation);

            return assessment;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<MemorySnapshot> CaptureMemorySnapshotAsync()
    {
        var snapshot = new MemorySnapshot
        {
            Timestamp = DateTime.UtcNow,
            GCMemoryUsage = GC.GetTotalMemory(false),
            ProcessMemoryUsage = Process.GetCurrentProcess().WorkingSet64,
            AvailableSystemMemory = (long)_availableMemoryCounter.NextValue() * 1_000_000, // Convert MB to bytes
            GCGeneration0Collections = GC.CollectionCount(0),
            GCGeneration1Collections = GC.CollectionCount(1),
            GCGeneration2Collections = GC.CollectionCount(2)
        };

        // Maintain rolling window of snapshots
        _memoryHistory.Enqueue(snapshot);
        while (_memoryHistory.Count > 100) // Keep last 100 snapshots
        {
            _memoryHistory.TryDequeue(out _);
        }

        return snapshot;
    }

    private MemoryRiskLevel CalculateRiskLevel(long requestedBytes, long projectedUsage, MemorySnapshot currentSnapshot)
    {
        // 1. Absolute memory threshold check
        if (projectedUsage > _thresholds.EmergencyThreshold)
            return MemoryRiskLevel.Emergency;

        if (projectedUsage > _thresholds.CriticalThreshold)
            return MemoryRiskLevel.Critical;

        if (projectedUsage > _thresholds.WarningThreshold)
            return MemoryRiskLevel.Warning;

        // 2. System memory availability check
        if (currentSnapshot.ProcessMemoryUsage > _thresholds.SystemMemoryWarning)
            return MemoryRiskLevel.Warning;

        var availablePercentage = (double)currentSnapshot.AvailableSystemMemory / 
            (currentSnapshot.AvailableSystemMemory + currentSnapshot.ProcessMemoryUsage);
        
        if (availablePercentage < _thresholds.AvailableMemoryWarning)
            return MemoryRiskLevel.Critical;

        // 3. Historical trend analysis
        var memoryTrend = AnalyzeMemoryTrend();
        if (memoryTrend == MemoryTrend.RapidIncrease)
            return MemoryRiskLevel.Warning;

        return MemoryRiskLevel.Normal;
    }

    private MemoryTrend AnalyzeMemoryTrend()
    {
        var snapshots = _memoryHistory.ToArray();
        if (snapshots.Length < 5) return MemoryTrend.Stable;

        var recentSnapshots = snapshots.TakeLast(5).ToArray();
        var memoryIncreases = 0;
        var significantIncreases = 0;

        for (int i = 1; i < recentSnapshots.Length; i++)
        {
            var memoryChange = recentSnapshots[i].ProcessMemoryUsage - recentSnapshots[i - 1].ProcessMemoryUsage;
            if (memoryChange > 0)
            {
                memoryIncreases++;
                if (memoryChange > 10_000_000) // 10MB increase
                    significantIncreases++;
            }
        }

        if (significantIncreases >= 3)
            return MemoryTrend.RapidIncrease;

        if (memoryIncreases >= 4)
            return MemoryTrend.GradualIncrease;

        return MemoryTrend.Stable;
    }

    private MemoryAllocationRecommendation GenerateRecommendation(MemoryRiskLevel riskLevel, long requestedBytes, MemorySnapshot snapshot)
    {
        return riskLevel switch
        {
            MemoryRiskLevel.Normal => new MemoryAllocationRecommendation
            {
                Action = AllocationAction.Proceed,
                Message = "Memory allocation within safe limits",
                OptimizationSuggestions = new List<string>()
            },

            MemoryRiskLevel.Warning => new MemoryAllocationRecommendation
            {
                Action = AllocationAction.ProceedWithCaution,
                Message = $"Large allocation requested ({requestedBytes / 1_000_000}MB). Monitor memory usage carefully.",
                OptimizationSuggestions = new List<string>
                {
                    "Consider reducing capture resolution",
                    "Implement progressive processing",
                    "Enable aggressive cleanup"
                }
            },

            MemoryRiskLevel.Critical => new MemoryAllocationRecommendation
            {
                Action = AllocationAction.RequireOptimization,
                Message = "Memory usage approaching critical levels. Optimization required.",
                OptimizationSuggestions = new List<string>
                {
                    "Trigger garbage collection before allocation",
                    "Reduce capture quality settings",
                    "Consider streaming processing approach",
                    "Release cached resources"
                }
            },

            MemoryRiskLevel.Emergency => new MemoryAllocationRecommendation
            {
                Action = AllocationAction.Reject,
                Message = "Memory allocation would exceed safety limits. Operation rejected.",
                OptimizationSuggestions = new List<string>
                {
                    "Significantly reduce operation scope",
                    "Implement chunked processing",
                    "Wait for memory pressure to subside",
                    "Consider alternative capture approach"
                }
            },

            _ => throw new ArgumentOutOfRangeException(nameof(riskLevel))
        };
    }

    private async void MonitorMemoryPressure(object? state)
    {
        try
        {
            var snapshot = await CaptureMemorySnapshotAsync();
            var currentPressure = CalculateMemoryPressureLevel(snapshot);

            if (currentPressure != _lastReportedPressureLevel)
            {
                _logger.LogInformation("Memory pressure level changed: {PreviousLevel} → {CurrentLevel}, Process memory: {ProcessMB}MB, Available: {AvailableMB}MB",
                    _lastReportedPressureLevel, currentPressure,
                    snapshot.ProcessMemoryUsage / 1_000_000,
                    snapshot.AvailableSystemMemory / 1_000_000);

                _lastReportedPressureLevel = currentPressure;

                // Trigger automatic cleanup if needed
                if (currentPressure >= MemoryPressureLevel.High)
                {
                    await TriggerEmergencyCleanupAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory pressure monitoring");
        }
    }

    private MemoryPressureLevel _lastReportedPressureLevel = MemoryPressureLevel.Normal;

    private async Task TriggerEmergencyCleanupAsync()
    {
        _logger.LogWarning("Triggering emergency memory cleanup due to high memory pressure");

        try
        {
            // 1. Force full garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 2. Clear managed caches
            await ClearManagedCachesAsync();

            // 3. Trigger resource cleanup events
            EmergencyCleanupRequested?.Invoke(this, EventArgs.Empty);

            var afterCleanupSnapshot = await CaptureMemorySnapshotAsync();
            _logger.LogInformation("Emergency cleanup completed. Memory usage: {BeforeMB}MB → {AfterMB}MB",
                _memoryHistory.LastOrDefault()?.ProcessMemoryUsage / 1_000_000,
                afterCleanupSnapshot.ProcessMemoryUsage / 1_000_000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during emergency memory cleanup");
        }
    }

    public event EventHandler? EmergencyCleanupRequested;

    public void Dispose()
    {
        _monitoringTimer?.Dispose();
        _availableMemoryCounter?.Dispose();
        _processMemoryCounter?.Dispose();
        _operationLock?.Dispose();
    }
}

// Supporting data structures
public sealed class MemorySnapshot
{
    public DateTime Timestamp { get; init; }
    public long GCMemoryUsage { get; init; }
    public long ProcessMemoryUsage { get; init; }
    public long AvailableSystemMemory { get; init; }
    public int GCGeneration0Collections { get; init; }
    public int GCGeneration1Collections { get; init; }
    public int GCGeneration2Collections { get; init; }
}

public sealed class MemoryAllocationAssessment
{
    public long RequestedBytes { get; init; }
    public string OperationName { get; init; } = string.Empty;
    public long CurrentMemoryUsage { get; init; }
    public long ProjectedMemoryUsage { get; init; }
    public long AvailableSystemMemory { get; init; }
    public MemoryRiskLevel RiskLevel { get; init; }
    public MemoryAllocationRecommendation Recommendation { get; init; } = new();
    public bool AllowAllocation { get; init; }
    public PerformanceImpact PredictedPerformanceImpact { get; init; }
    public DateTime AssessmentTimestamp { get; init; }
}

public enum MemoryRiskLevel
{
    Normal,
    Warning,
    Critical,
    Emergency
}

public enum MemoryTrend
{
    Stable,
    GradualIncrease,
    RapidIncrease
}

public enum MemoryPressureLevel
{
    Normal,
    Moderate,
    High,
    Critical
}

public enum AllocationAction
{
    Proceed,
    ProceedWithCaution,
    RequireOptimization,
    Reject
}

public enum PerformanceImpact
{
    Minimal,
    Moderate,
    Significant,
    Severe
}
```

### 2. Intelligent Memory Allocation Strategy

#### Smart Allocation Decision Engine
```csharp
public sealed class IntelligentMemoryAllocator
{
    private readonly AdvancedMemoryPressureMonitor _pressureMonitor;
    private readonly ILogger<IntelligentMemoryAllocator> _logger;
    private readonly AllocationStrategyConfig _config;

    public sealed class AllocationStrategyConfig
    {
        public bool EnablePredictiveAllocation { get; init; } = true;
        public bool EnableProgressiveProcessing { get; init; } = true;
        public bool EnableChunkedProcessing { get; init; } = true;
        public int MaxChunkSize { get; init; } = 25_000_000; // 25MB chunks
        public double MemoryPressureThresholdForChunking { get; init; } = 0.7; // 70%
    }

    public async Task<AllocatedResource<T>> AllocateResourceAsync<T>(
        Func<T> resourceFactory,
        long estimatedMemoryUsage,
        string operationName,
        CancellationToken cancellationToken = default) where T : IDisposable
    {
        // 1. Memory pressure assessment
        var assessment = await _pressureMonitor.AssessAllocationAsync(estimatedMemoryUsage, operationName);

        if (!assessment.AllowAllocation)
        {
            throw new InsufficientMemoryException(
                $"Memory allocation rejected for operation '{operationName}': {assessment.Recommendation.Message}");
        }

        // 2. Pre-allocation optimization
        if (assessment.RiskLevel >= MemoryRiskLevel.Warning)
        {
            await OptimizeMemoryBeforeAllocationAsync(assessment);
        }

        // 3. Allocation strategy selection
        var strategy = SelectAllocationStrategy(assessment, estimatedMemoryUsage);

        try
        {
            var resource = await ExecuteAllocationStrategyAsync(strategy, resourceFactory, operationName, cancellationToken);
            
            return new AllocatedResource<T>(resource, estimatedMemoryUsage, operationName, _logger);
        }
        catch (OutOfMemoryException ex)
        {
            _logger.LogError(ex, "Out of memory during allocation for {Operation} ({EstimatedMB}MB)", 
                operationName, estimatedMemoryUsage / 1_000_000);

            // Emergency cleanup and retry once
            await _pressureMonitor.TriggerEmergencyCleanupAsync();
            
            try
            {
                var retryResource = await ExecuteAllocationStrategyAsync(strategy, resourceFactory, operationName, cancellationToken);
                _logger.LogInformation("Retry allocation successful after emergency cleanup for {Operation}", operationName);
                return new AllocatedResource<T>(retryResource, estimatedMemoryUsage, operationName, _logger);
            }
            catch (OutOfMemoryException retryEx)
            {
                _logger.LogError(retryEx, "Retry allocation failed for {Operation}", operationName);
                throw new InsufficientMemoryException(
                    $"Unable to allocate memory for operation '{operationName}' even after emergency cleanup", retryEx);
            }
        }
    }

    private AllocationStrategy SelectAllocationStrategy(MemoryAllocationAssessment assessment, long estimatedMemoryUsage)
    {
        // 1. Normal allocation for low-risk scenarios
        if (assessment.RiskLevel == MemoryRiskLevel.Normal && estimatedMemoryUsage < _config.MaxChunkSize)
        {
            return AllocationStrategy.Direct;
        }

        // 2. Progressive allocation for moderate risk
        if (assessment.RiskLevel == MemoryRiskLevel.Warning && _config.EnableProgressiveProcessing)
        {
            return AllocationStrategy.Progressive;
        }

        // 3. Chunked allocation for high risk or large allocations
        if ((assessment.RiskLevel >= MemoryRiskLevel.Critical || estimatedMemoryUsage > _config.MaxChunkSize * 2) 
            && _config.EnableChunkedProcessing)
        {
            return AllocationStrategy.Chunked;
        }

        // 4. Optimized allocation with pre-cleanup
        return AllocationStrategy.Optimized;
    }

    private async Task<T> ExecuteAllocationStrategyAsync<T>(
        AllocationStrategy strategy, 
        Func<T> resourceFactory, 
        string operationName,
        CancellationToken cancellationToken) where T : IDisposable
    {
        return strategy switch
        {
            AllocationStrategy.Direct => await Task.Run(resourceFactory, cancellationToken),
            
            AllocationStrategy.Progressive => await ExecuteProgressiveAllocationAsync(resourceFactory, operationName, cancellationToken),
            
            AllocationStrategy.Chunked => await ExecuteChunkedAllocationAsync(resourceFactory, operationName, cancellationToken),
            
            AllocationStrategy.Optimized => await ExecuteOptimizedAllocationAsync(resourceFactory, operationName, cancellationToken),
            
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };
    }

    private async Task<T> ExecuteProgressiveAllocationAsync<T>(
        Func<T> resourceFactory,
        string operationName,
        CancellationToken cancellationToken) where T : IDisposable
    {
        _logger.LogDebug("Executing progressive allocation for {Operation}", operationName);

        // Progressive allocation with intermediate memory checks
        const int progressSteps = 5;
        var stepDelay = TimeSpan.FromMilliseconds(100);

        for (int step = 0; step < progressSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Check memory pressure before each step
            var currentSnapshot = await _pressureMonitor.CaptureMemorySnapshotAsync();
            var pressureLevel = _pressureMonitor.CalculateMemoryPressureLevel(currentSnapshot);
            
            if (pressureLevel >= MemoryPressureLevel.Critical)
            {
                _logger.LogWarning("Memory pressure too high during progressive allocation, step {Step}/{TotalSteps}", 
                    step + 1, progressSteps);
                
                // Trigger cleanup and wait
                await _pressureMonitor.TriggerEmergencyCleanupAsync();
                await Task.Delay(stepDelay * 2, cancellationToken);
            }
            
            await Task.Delay(stepDelay, cancellationToken);
        }

        return await Task.Run(resourceFactory, cancellationToken);
    }

    private async Task OptimizeMemoryBeforeAllocationAsync(MemoryAllocationAssessment assessment)
    {
        _logger.LogInformation("Optimizing memory before allocation for {Operation} (Risk: {RiskLevel})", 
            assessment.OperationName, assessment.RiskLevel);

        var optimizations = new List<Task>();

        // 1. Garbage collection optimization
        if (assessment.RiskLevel >= MemoryRiskLevel.Warning)
        {
            optimizations.Add(Task.Run(() =>
            {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
            }));
        }

        // 2. Cache cleanup
        if (assessment.RiskLevel >= MemoryRiskLevel.Critical)
        {
            optimizations.Add(ClearNonEssentialCachesAsync());
        }

        // 3. Resource pool optimization
        optimizations.Add(OptimizeResourcePoolsAsync());

        await Task.WhenAll(optimizations);

        _logger.LogDebug("Memory optimization completed for {Operation}", assessment.OperationName);
    }
}

public sealed class AllocatedResource<T> : IDisposable where T : IDisposable
{
    private readonly T _resource;
    private readonly long _estimatedMemoryUsage;
    private readonly string _operationName;
    private readonly ILogger _logger;
    private readonly DateTime _allocatedAt;
    private bool _disposed = false;

    internal AllocatedResource(T resource, long estimatedMemoryUsage, string operationName, ILogger logger)
    {
        _resource = resource;
        _estimatedMemoryUsage = estimatedMemoryUsage;
        _operationName = operationName;
        _logger = logger;
        _allocatedAt = DateTime.UtcNow;
    }

    public T Resource => _disposed ? throw new ObjectDisposedException(nameof(AllocatedResource<T>)) : _resource;

    public TimeSpan Age => DateTime.UtcNow - _allocatedAt;
    public long EstimatedMemoryUsage => _estimatedMemoryUsage;
    public string OperationName => _operationName;

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _resource?.Dispose();
                
                _logger.LogDebug("Released allocated resource for {Operation} after {Duration}ms ({EstimatedMB}MB)", 
                    _operationName, Age.TotalMilliseconds, _estimatedMemoryUsage / 1_000_000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing allocated resource for {Operation}", _operationName);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}

public enum AllocationStrategy
{
    Direct,
    Progressive,
    Chunked,
    Optimized
}
```

---

## 🔧 Resource Cleanup Patterns

### 1. Comprehensive RAII Implementation

#### Advanced Resource Management with Lifecycle Tracking
```csharp
public sealed class AdvancedResourceManager : IDisposable
{
    private readonly List<IDisposable> _managedResources = new();
    private readonly Dictionary<IntPtr, UnmanagedResource> _unmanagedResources = new();
    private readonly List<object> _comObjects = new();
    private readonly ConcurrentDictionary<string, ResourcePool> _resourcePools = new();
    private readonly Timer _cleanupTimer;
    private readonly ILogger<AdvancedResourceManager> _logger;
    private readonly object _lock = new();
    private bool _disposed = false;

    public AdvancedResourceManager(ILogger<AdvancedResourceManager> logger)
    {
        _logger = logger;
        _cleanupTimer = new Timer(PerformScheduledCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    // Enhanced managed resource acquisition with lifecycle tracking
    public T AcquireManagedResource<T>(
        Func<T> resourceFactory,
        string resourceName = "",
        TimeSpan? maxLifetime = null) where T : IDisposable
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdvancedResourceManager));

        lock (_lock)
        {
            var resource = resourceFactory();
            var trackedResource = new TrackedManagedResource<T>(resource, resourceName, maxLifetime);
            
            _managedResources.Add(trackedResource);
            
            _logger.LogDebug("Acquired managed resource: {ResourceType} ({ResourceName}), Total managed: {Count}",
                typeof(T).Name, resourceName, _managedResources.Count);
            
            return resource;
        }
    }

    // Enhanced unmanaged resource acquisition with automatic cleanup
    public IntPtr AcquireUnmanagedResource(
        Func<IntPtr> resourceFactory,
        Action<IntPtr> releaseAction,
        string resourceType = "Handle",
        TimeSpan? maxLifetime = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdvancedResourceManager));

        lock (_lock)
        {
            var handle = resourceFactory();
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to acquire {resourceType}");

            var unmanagedResource = new UnmanagedResource
            {
                Handle = handle,
                ReleaseAction = releaseAction,
                ResourceType = resourceType,
                AcquiredAt = DateTime.UtcNow,
                MaxLifetime = maxLifetime ?? TimeSpan.FromHours(1)
            };

            _unmanagedResources[handle] = unmanagedResource;
            
            _logger.LogDebug("Acquired unmanaged resource: {ResourceType} = {Handle}, Total unmanaged: {Count}",
                resourceType, handle, _unmanagedResources.Count);
            
            return handle;
        }
    }

    // Device context acquisition with automatic lifecycle management
    public IntPtr AcquireDeviceContext(IntPtr windowHandle, TimeSpan? maxLifetime = null)
    {
        return AcquireUnmanagedResource(
            () => Win32API.GetWindowDC(windowHandle),
            dc => Win32API.ReleaseDC(windowHandle, dc),
            $"DeviceContext[Window={windowHandle}]",
            maxLifetime);
    }

    // Bitmap acquisition with memory tracking
    public Bitmap AcquireBitmap(int width, int height, PixelFormat format = PixelFormat.Format32bppArgb)
    {
        var estimatedSize = width * height * (Image.GetPixelFormatSize(format) / 8);
        return AcquireManagedResource(
            () => new Bitmap(width, height, format),
            $"Bitmap[{width}x{height}]",
            TimeSpan.FromMinutes(10));
    }

    // Graphics acquisition with automatic disposal
    public Graphics AcquireGraphics(Image image, string operationName = "")
    {
        return AcquireManagedResource(
            () => Graphics.FromImage(image),
            $"Graphics[{operationName}]",
            TimeSpan.FromMinutes(5));
    }

    // COM object acquisition with reference counting
    public T AcquireCOMObject<T>(Func<T> comFactory, string objectName = "") where T : class
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdvancedResourceManager));

        lock (_lock)
        {
            var comObject = comFactory();
            if (comObject == null)
                throw new InvalidOperationException($"Failed to acquire COM object: {typeof(T).Name}");

            var trackedCOM = new TrackedCOMObject
            {
                Object = comObject,
                ObjectType = typeof(T).Name,
                ObjectName = objectName,
                AcquiredAt = DateTime.UtcNow
            };

            _comObjects.Add(trackedCOM);
            
            _logger.LogDebug("Acquired COM object: {COMType} ({ObjectName}), Total COM: {Count}",
                typeof(T).Name, objectName, _comObjects.Count);
            
            return comObject;
        }
    }

    // Resource pool management
    public T AcquireFromPool<T>(string poolName, Func<T> factory) where T : class, IDisposable
    {
        var pool = _resourcePools.GetOrAdd(poolName, _ => new ResourcePool<T>(factory, poolName, _logger));
        return ((ResourcePool<T>)pool).Acquire();
    }

    public void ReturnToPool<T>(string poolName, T resource) where T : class, IDisposable
    {
        if (_resourcePools.TryGetValue(poolName, out var pool))
        {
            ((ResourcePool<T>)pool).Return(resource);
        }
    }

    // Scheduled cleanup of expired resources
    private void PerformScheduledCleanup(object? state)
    {
        if (_disposed) return;

        try
        {
            lock (_lock)
            {
                var cleanupCount = 0;

                // Clean up expired unmanaged resources
                var expiredUnmanaged = _unmanagedResources.Values
                    .Where(r => DateTime.UtcNow - r.AcquiredAt > r.MaxLifetime)
                    .ToList();

                foreach (var expired in expiredUnmanaged)
                {
                    try
                    {
                        expired.ReleaseAction(expired.Handle);
                        _unmanagedResources.Remove(expired.Handle);
                        cleanupCount++;
                        
                        _logger.LogDebug("Cleaned up expired unmanaged resource: {ResourceType} = {Handle}",
                            expired.ResourceType, expired.Handle);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up expired unmanaged resource: {ResourceType} = {Handle}",
                            expired.ResourceType, expired.Handle);
                    }
                }

                // Clean up expired managed resources
                var expiredManaged = _managedResources.OfType<TrackedManagedResource>()
                    .Where(r => r.IsExpired)
                    .ToList();

                foreach (var expired in expiredManaged)
                {
                    try
                    {
                        expired.Dispose();
                        _managedResources.Remove(expired);
                        cleanupCount++;
                        
                        _logger.LogDebug("Cleaned up expired managed resource: {ResourceType} ({ResourceName})",
                            expired.ResourceType, expired.ResourceName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error cleaning up expired managed resource: {ResourceType} ({ResourceName})",
                            expired.ResourceType, expired.ResourceName);
                    }
                }

                if (cleanupCount > 0)
                {
                    _logger.LogInformation("Scheduled cleanup completed: {CleanupCount} resources cleaned up", cleanupCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled resource cleanup");
        }
    }

    // Memory pressure response
    public async Task RespondToMemoryPressureAsync(MemoryPressureLevel pressureLevel)
    {
        _logger.LogInformation("Responding to memory pressure: {PressureLevel}", pressureLevel);

        switch (pressureLevel)
        {
            case MemoryPressureLevel.Moderate:
                await CleanupNonEssentialResourcesAsync();
                break;

            case MemoryPressureLevel.High:
                await CleanupNonEssentialResourcesAsync();
                await TriggerResourcePoolCleanupAsync();
                break;

            case MemoryPressureLevel.Critical:
                await CleanupNonEssentialResourcesAsync();
                await TriggerResourcePoolCleanupAsync();
                await ForceGarbageCollectionAsync();
                break;
        }
    }

    private async Task CleanupNonEssentialResourcesAsync()
    {
        lock (_lock)
        {
            // Clean up resources that have been idle for more than 5 minutes
            var idleThreshold = TimeSpan.FromMinutes(5);
            var currentTime = DateTime.UtcNow;

            var idleUnmanaged = _unmanagedResources.Values
                .Where(r => currentTime - r.AcquiredAt > idleThreshold)
                .ToList();

            foreach (var idle in idleUnmanaged)
            {
                try
                {
                    idle.ReleaseAction(idle.Handle);
                    _unmanagedResources.Remove(idle.Handle);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error cleaning up idle resource during memory pressure");
                }
            }
        }
    }

    private async Task TriggerResourcePoolCleanupAsync()
    {
        var cleanupTasks = _resourcePools.Values.Select(pool => Task.Run(() => pool.CleanupIdleResources()));
        await Task.WhenAll(cleanupTasks);
    }

    private async Task ForceGarbageCollectionAsync()
    {
        await Task.Run(() =>
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer?.Dispose();

        lock (_lock)
        {
            var errors = new List<Exception>();

            // 1. Dispose resource pools
            foreach (var pool in _resourcePools.Values)
            {
                try
                {
                    pool.Dispose();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error disposing resource pool");
                }
            }

            // 2. Release COM objects (in reverse order)
            foreach (var comObject in _comObjects.Cast<TrackedCOMObject>().Reverse())
            {
                try
                {
                    if (comObject.Object != null && Marshal.IsComObject(comObject.Object))
                    {
                        Marshal.ReleaseComObject(comObject.Object);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error releasing COM object: {COMType}", comObject.ObjectType);
                }
            }

            // 3. Release unmanaged resources (in reverse order)
            foreach (var unmanagedResource in _unmanagedResources.Values.Reverse())
            {
                try
                {
                    unmanagedResource.ReleaseAction(unmanagedResource.Handle);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error releasing unmanaged resource: {ResourceType}", unmanagedResource.ResourceType);
                }
            }

            // 4. Dispose managed resources (in reverse order)
            foreach (var managedResource in _managedResources.AsEnumerable().Reverse())
            {
                try
                {
                    managedResource?.Dispose();
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                    _logger.LogWarning(ex, "Error disposing managed resource");
                }
            }

            _disposed = true;

            if (errors.Any())
            {
                _logger.LogError("Resource disposal completed with {ErrorCount} errors", errors.Count);
            }
            else
            {
                _logger.LogInformation("Resource disposal completed successfully");
            }
        }
    }

    // Supporting classes for resource tracking
    private sealed class TrackedManagedResource<T> : IDisposable where T : IDisposable
    {
        private readonly T _resource;
        private readonly DateTime _acquiredAt;
        private readonly TimeSpan? _maxLifetime;
        private bool _disposed = false;

        public TrackedManagedResource(T resource, string resourceName, TimeSpan? maxLifetime)
        {
            _resource = resource;
            ResourceName = resourceName;
            _acquiredAt = DateTime.UtcNow;
            _maxLifetime = maxLifetime;
        }

        public string ResourceName { get; }
        public string ResourceType => typeof(T).Name;
        public bool IsExpired => _maxLifetime.HasValue && DateTime.UtcNow - _acquiredAt > _maxLifetime.Value;

        public void Dispose()
        {
            if (!_disposed)
            {
                _resource?.Dispose();
                _disposed = true;
            }
        }
    }

    private sealed class TrackedCOMObject
    {
        public required object Object { get; init; }
        public required string ObjectType { get; init; }
        public string ObjectName { get; init; } = string.Empty;
        public DateTime AcquiredAt { get; init; }
    }

    private sealed class UnmanagedResource
    {
        public required IntPtr Handle { get; init; }
        public required Action<IntPtr> ReleaseAction { get; init; }
        public required string ResourceType { get; init; }
        public DateTime AcquiredAt { get; init; }
        public TimeSpan MaxLifetime { get; init; }
    }
}
```

### 2. Memory Leak Detection and Prevention

#### Comprehensive Leak Detection System
```csharp
public sealed class MemoryLeakDetector : IDisposable
{
    private readonly Timer _detectionTimer;
    private readonly ILogger<MemoryLeakDetector> _logger;
    private readonly ConcurrentDictionary<string, ResourceTracker> _resourceTrackers = new();
    private readonly MemoryLeakDetectionConfig _config;

    public sealed class MemoryLeakDetectionConfig
    {
        public TimeSpan DetectionInterval { get; init; } = TimeSpan.FromMinutes(2);
        public long MemoryGrowthThreshold { get; init; } = 50_000_000; // 50MB
        public int SampleWindowSize { get; init; } = 10;
        public double LeakConfidenceThreshold { get; init; } = 0.8; // 80% confidence
    }

    public MemoryLeakDetector(
        MemoryLeakDetectionConfig? config = null,
        ILogger<MemoryLeakDetector>? logger = null)
    {
        _config = config ?? new MemoryLeakDetectionConfig();
        _logger = logger ?? NullLogger<MemoryLeakDetector>.Instance;

        _detectionTimer = new Timer(DetectMemoryLeaks, null, _config.DetectionInterval, _config.DetectionInterval);

        _logger.LogInformation("Memory leak detector initialized with {Interval} detection interval", _config.DetectionInterval);
    }

    public void TrackResourceAllocation(string resourceType, long estimatedSize, string? operationContext = null)
    {
        var tracker = _resourceTrackers.GetOrAdd(resourceType, _ => new ResourceTracker(resourceType));
        tracker.RecordAllocation(estimatedSize, operationContext);
    }

    public void TrackResourceDeallocation(string resourceType, long estimatedSize)
    {
        if (_resourceTrackers.TryGetValue(resourceType, out var tracker))
        {
            tracker.RecordDeallocation(estimatedSize);
        }
    }

    private void DetectMemoryLeaks(object? state)
    {
        try
        {
            var currentMemory = GC.GetTotalMemory(false);
            var detectionResults = new List<LeakDetectionResult>();

            // 1. Overall memory growth analysis
            var overallResult = AnalyzeOverallMemoryGrowth(currentMemory);
            if (overallResult != null)
                detectionResults.Add(overallResult);

            // 2. Resource-specific leak detection
            foreach (var tracker in _resourceTrackers.Values)
            {
                var resourceResult = AnalyzeResourceLeaks(tracker);
                if (resourceResult != null)
                    detectionResults.Add(resourceResult);
            }

            // 3. GC performance analysis
            var gcResult = AnalyzeGCPerformance();
            if (gcResult != null)
                detectionResults.Add(gcResult);

            // 4. Report findings
            if (detectionResults.Any())
            {
                ReportLeakDetectionResults(detectionResults);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory leak detection");
        }
    }

    private LeakDetectionResult? AnalyzeOverallMemoryGrowth(long currentMemory)
    {
        var tracker = _resourceTrackers.GetOrAdd("Overall", _ => new ResourceTracker("Overall"));
        tracker.RecordMemorySnapshot(currentMemory);

        var samples = tracker.GetMemorySamples();
        if (samples.Count < _config.SampleWindowSize)
            return null;

        // Calculate memory growth trend
        var recentSamples = samples.TakeLast(_config.SampleWindowSize).ToList();
        var memoryGrowth = recentSamples.Last().MemoryUsage - recentSamples.First().MemoryUsage;
        var timeSpan = recentSamples.Last().Timestamp - recentSamples.First().Timestamp;

        if (memoryGrowth > _config.MemoryGrowthThreshold)
        {
            var growthRate = memoryGrowth / timeSpan.TotalHours; // MB per hour
            var confidence = CalculateLeakConfidence(recentSamples);

            if (confidence >= _config.LeakConfidenceThreshold)
            {
                return new LeakDetectionResult
                {
                    LeakType = LeakType.OverallMemoryGrowth,
                    ResourceType = "Overall",
                    MemoryGrowth = memoryGrowth,
                    GrowthRate = growthRate,
                    Confidence = confidence,
                    Description = $"Overall memory growth detected: {memoryGrowth / 1_000_000:F1}MB over {timeSpan.TotalMinutes:F1} minutes",
                    Recommendations = new List<string>
                    {
                        "Review recent resource allocations",
                        "Check for unclosed resources",
                        "Consider forcing garbage collection",
                        "Analyze resource disposal patterns"
                    }
                };
            }
        }

        return null;
    }

    private LeakDetectionResult? AnalyzeResourceLeaks(ResourceTracker tracker)
    {
        var allocations = tracker.GetAllocationHistory();
        if (allocations.Count < 5) return null;

        // Check for resources that are allocated but never deallocated
        var recentAllocations = allocations.TakeLast(20).ToList();
        var allocationRate = recentAllocations.Count / 20.0; // allocations per sample
        var deallocationRate = tracker.GetDeallocationRate();

        if (allocationRate > deallocationRate * 1.5) // 50% more allocations than deallocations
        {
            var netAllocationGrowth = tracker.GetNetAllocationGrowth();
            var confidence = Math.Min(1.0, (allocationRate - deallocationRate) / allocationRate);

            if (netAllocationGrowth > _config.MemoryGrowthThreshold / 10) // 10% of threshold for specific resources
            {
                return new LeakDetectionResult
                {
                    LeakType = LeakType.ResourceLeak,
                    ResourceType = tracker.ResourceType,
                    MemoryGrowth = netAllocationGrowth,
                    Confidence = confidence,
                    Description = $"Resource leak detected for {tracker.ResourceType}: {netAllocationGrowth / 1_000_000:F1}MB net growth",
                    Recommendations = new List<string>
                    {
                        $"Review {tracker.ResourceType} disposal patterns",
                        "Check for missing using statements or Dispose calls",
                        "Investigate long-lived resource references",
                        "Consider implementing resource pooling"
                    }
                };
            }
        }

        return null;
    }

    private LeakDetectionResult? AnalyzeGCPerformance()
    {
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        var tracker = _resourceTrackers.GetOrAdd("GC", _ => new ResourceTracker("GC"));
        tracker.RecordGCMetrics(gen0Collections, gen1Collections, gen2Collections);

        var gcHistory = tracker.GetGCHistory();
        if (gcHistory.Count < 5) return null;

        var recentHistory = gcHistory.TakeLast(5).ToList();
        var gen2Increase = recentHistory.Last().Gen2Collections - recentHistory.First().Gen2Collections;

        // High frequency of Gen2 collections indicates memory pressure
        if (gen2Increase > 3) // More than 3 Gen2 collections in recent history
        {
            return new LeakDetectionResult
            {
                LeakType = LeakType.GCPressure,
                ResourceType = "GarbageCollector",
                Description = $"High GC pressure detected: {gen2Increase} Gen2 collections in recent period",
                Confidence = Math.Min(1.0, gen2Increase / 10.0),
                Recommendations = new List<string>
                {
                    "Review object allocation patterns",
                    "Check for large object heap pressure",
                    "Consider object pooling for frequently allocated objects",
                    "Investigate finalizable objects"
                }
            };
        }

        return null;
    }

    private double CalculateLeakConfidence(List<MemorySample> samples)
    {
        if (samples.Count < 3) return 0.0;

        // Calculate how consistently memory is growing
        var growthSteps = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            if (samples[i].MemoryUsage > samples[i - 1].MemoryUsage)
                growthSteps++;
        }

        return (double)growthSteps / (samples.Count - 1);
    }

    private void ReportLeakDetectionResults(List<LeakDetectionResult> results)
    {
        foreach (var result in results.OrderByDescending(r => r.Confidence))
        {
            var logLevel = result.Confidence >= 0.9 ? LogLevel.Error :
                          result.Confidence >= 0.7 ? LogLevel.Warning :
                          LogLevel.Information;

            _logger.Log(logLevel, "Memory leak detected: {LeakType} in {ResourceType} (Confidence: {Confidence:P1}). {Description}",
                result.LeakType, result.ResourceType, result.Confidence, result.Description);

            if (result.Recommendations.Any())
            {
                _logger.LogInformation("Recommendations for {ResourceType}: {Recommendations}",
                    result.ResourceType, string.Join("; ", result.Recommendations));
            }
        }

        // Trigger leak response if high-confidence leaks detected
        var highConfidenceLeaks = results.Where(r => r.Confidence >= 0.8).ToList();
        if (highConfidenceLeaks.Any())
        {
            MemoryLeakDetected?.Invoke(this, new MemoryLeakEventArgs(highConfidenceLeaks));
        }
    }

    public event EventHandler<MemoryLeakEventArgs>? MemoryLeakDetected;

    public void Dispose()
    {
        _detectionTimer?.Dispose();
        
        var finalReport = GenerateFinalLeakReport();
        _logger.LogInformation("Memory leak detector disposed. Final report: {Report}", finalReport);
    }

    private string GenerateFinalLeakReport()
    {
        var totalAllocations = _resourceTrackers.Values.Sum(t => t.GetTotalAllocations());
        var totalDeallocations = _resourceTrackers.Values.Sum(t => t.GetTotalDeallocations());
        var netGrowth = totalAllocations - totalDeallocations;

        return $"Total allocations: {totalAllocations / 1_000_000:F1}MB, " +
               $"Total deallocations: {totalDeallocations / 1_000_000:F1}MB, " +
               $"Net growth: {netGrowth / 1_000_000:F1}MB";
    }
}

// Supporting classes
public sealed class ResourceTracker
{
    private readonly ConcurrentQueue<AllocationRecord> _allocations = new();
    private readonly ConcurrentQueue<MemorySample> _memorySamples = new();
    private readonly ConcurrentQueue<GCMetrics> _gcHistory = new();
    private long _totalAllocated = 0;
    private long _totalDeallocated = 0;

    public string ResourceType { get; }

    public ResourceTracker(string resourceType)
    {
        ResourceType = resourceType;
    }

    public void RecordAllocation(long size, string? context = null)
    {
        Interlocked.Add(ref _totalAllocated, size);
        _allocations.Enqueue(new AllocationRecord
        {
            Size = size,
            Context = context ?? string.Empty,
            Timestamp = DateTime.UtcNow
        });

        // Keep only recent allocations
        while (_allocations.Count > 1000)
        {
            _allocations.TryDequeue(out _);
        }
    }

    public void RecordDeallocation(long size)
    {
        Interlocked.Add(ref _totalDeallocated, size);
    }

    public void RecordMemorySnapshot(long memoryUsage)
    {
        _memorySamples.Enqueue(new MemorySample
        {
            MemoryUsage = memoryUsage,
            Timestamp = DateTime.UtcNow
        });

        while (_memorySamples.Count > 100)
        {
            _memorySamples.TryDequeue(out _);
        }
    }

    public void RecordGCMetrics(int gen0, int gen1, int gen2)
    {
        _gcHistory.Enqueue(new GCMetrics
        {
            Gen0Collections = gen0,
            Gen1Collections = gen1,
            Gen2Collections = gen2,
            Timestamp = DateTime.UtcNow
        });

        while (_gcHistory.Count > 50)
        {
            _gcHistory.TryDequeue(out _);
        }
    }

    public List<AllocationRecord> GetAllocationHistory() => _allocations.ToList();
    public List<MemorySample> GetMemorySamples() => _memorySamples.ToList();
    public List<GCMetrics> GetGCHistory() => _gcHistory.ToList();
    public long GetTotalAllocations() => _totalAllocated;
    public long GetTotalDeallocations() => _totalDeallocated;
    public long GetNetAllocationGrowth() => _totalAllocated - _totalDeallocated;
    public double GetDeallocationRate() => _totalDeallocated > 0 ? (double)_totalDeallocated / _totalAllocated : 0.0;
}

public sealed class AllocationRecord
{
    public long Size { get; init; }
    public string Context { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed class MemorySample
{
    public long MemoryUsage { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class GCMetrics
{
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class LeakDetectionResult
{
    public LeakType LeakType { get; init; }
    public string ResourceType { get; init; } = string.Empty;
    public long MemoryGrowth { get; init; }
    public double GrowthRate { get; init; }
    public double Confidence { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> Recommendations { get; init; } = new();
}

public enum LeakType
{
    OverallMemoryGrowth,
    ResourceLeak,
    GCPressure
}

public sealed class MemoryLeakEventArgs : EventArgs
{
    public List<LeakDetectionResult> DetectedLeaks { get; }

    public MemoryLeakEventArgs(List<LeakDetectionResult> detectedLeaks)
    {
        DetectedLeaks = detectedLeaks;
    }
}
```

---

## 📊 Performance Profiling Techniques

### 1. Memory Performance Profiler

#### Comprehensive Memory Performance Analysis
```csharp
public sealed class MemoryPerformanceProfiler : IDisposable
{
    private readonly ILogger<MemoryPerformanceProfiler> _logger;
    private readonly ConcurrentDictionary<string, OperationProfile> _operationProfiles = new();
    private readonly Timer _reportingTimer;
    private readonly MemoryPerformanceConfig _config;

    public sealed class MemoryPerformanceConfig
    {
        public TimeSpan ReportingInterval { get; init; } = TimeSpan.FromMinutes(5);
        public bool EnableDetailedProfiling { get; init; } = true;
        public bool EnableGCProfiling { get; init; } = true;
        public int MaxOperationHistory { get; init; } = 1000;
    }

    public MemoryPerformanceProfiler(
        MemoryPerformanceConfig? config = null,
        ILogger<MemoryPerformanceProfiler>? logger = null)
    {
        _config = config ?? new MemoryPerformanceConfig();
        _logger = logger ?? NullLogger<MemoryPerformanceProfiler>.Instance;

        _reportingTimer = new Timer(GeneratePerformanceReport, null, 
            _config.ReportingInterval, _config.ReportingInterval);
    }

    public async Task<T> ProfileOperationAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        var profile = _operationProfiles.GetOrAdd(operationName, _ => new OperationProfile(operationName));
        
        var memoryBefore = GC.GetTotalMemory(false);
        var gcBefore = new GCSnapshot();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await operation();
            
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var gcAfter = new GCSnapshot();

            // Record successful operation
            profile.RecordOperation(new OperationMeasurement
            {
                Duration = stopwatch.Elapsed,
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter,
                MemoryDelta = memoryAfter - memoryBefore,
                GCBefore = gcBefore,
                GCAfter = gcAfter,
                Success = true,
                Timestamp = DateTime.UtcNow
            });

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var memoryAfter = GC.GetTotalMemory(false);
            var gcAfter = new GCSnapshot();

            // Record failed operation
            profile.RecordOperation(new OperationMeasurement
            {
                Duration = stopwatch.Elapsed,
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter,
                MemoryDelta = memoryAfter - memoryBefore,
                GCBefore = gcBefore,
                GCAfter = gcAfter,
                Success = false,
                Exception = ex.GetType().Name,
                Timestamp = DateTime.UtcNow
            });

            throw;
        }
    }

    public T ProfileOperation<T>(
        Func<T> operation,
        string operationName)
    {
        return ProfileOperationAsync(() => Task.FromResult(operation()), operationName).Result;
    }

    private void GeneratePerformanceReport(object? state)
    {
        try
        {
            var report = new MemoryPerformanceReport
            {
                ReportTimestamp = DateTime.UtcNow,
                OperationProfiles = _operationProfiles.Values.ToDictionary(p => p.OperationName, p => p.GenerateSummary())
            };

            LogPerformanceReport(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating memory performance report");
        }
    }

    private void LogPerformanceReport(MemoryPerformanceReport report)
    {
        _logger.LogInformation("=== Memory Performance Report ({ReportTime}) ===", report.ReportTimestamp);

        foreach (var (operationName, summary) in report.OperationProfiles.OrderByDescending(p => p.Value.TotalOperations))
        {
            _logger.LogInformation("Operation: {OperationName}", operationName);
            _logger.LogInformation("  Total Operations: {Total}, Success Rate: {SuccessRate:P1}", 
                summary.TotalOperations, summary.SuccessRate);
            _logger.LogInformation("  Average Duration: {AvgDuration}ms, Memory Delta: {AvgMemoryDelta:+#,##0;-#,##0;0}KB", 
                summary.AverageDuration.TotalMilliseconds, summary.AverageMemoryDelta / 1024);
            _logger.LogInformation("  GC Impact: Gen0={Gen0}, Gen1={Gen1}, Gen2={Gen2}", 
                summary.AverageGCGen0Impact, summary.AverageGCGen1Impact, summary.AverageGCGen2Impact);

            if (summary.PerformanceIssues.Any())
            {
                _logger.LogWarning("  Performance Issues: {Issues}", string.Join(", ", summary.PerformanceIssues));
            }
        }
    }

    public MemoryPerformanceReport GetCurrentReport()
    {
        return new MemoryPerformanceReport
        {
            ReportTimestamp = DateTime.UtcNow,
            OperationProfiles = _operationProfiles.Values.ToDictionary(p => p.OperationName, p => p.GenerateSummary())
        };
    }

    public void Dispose()
    {
        _reportingTimer?.Dispose();
        
        var finalReport = GetCurrentReport();
        _logger.LogInformation("Memory performance profiler disposed. Final summary: {TotalOperations} operations across {OperationTypes} operation types",
            finalReport.OperationProfiles.Values.Sum(s => s.TotalOperations),
            finalReport.OperationProfiles.Count);
    }
}

// Supporting classes
public sealed class OperationProfile
{
    private readonly ConcurrentQueue<OperationMeasurement> _measurements = new();
    private readonly object _lock = new();

    public string OperationName { get; }

    public OperationProfile(string operationName)
    {
        OperationName = operationName;
    }

    public void RecordOperation(OperationMeasurement measurement)
    {
        lock (_lock)
        {
            _measurements.Enqueue(measurement);
            
            while (_measurements.Count > 1000) // Keep recent measurements
            {
                _measurements.TryDequeue(out _);
            }
        }
    }

    public OperationSummary GenerateSummary()
    {
        lock (_lock)
        {
            var measurements = _measurements.ToList();
            if (!measurements.Any())
            {
                return new OperationSummary { OperationName = OperationName };
            }

            var successfulMeasurements = measurements.Where(m => m.Success).ToList();
            var failedMeasurements = measurements.Where(m => !m.Success).ToList();

            var summary = new OperationSummary
            {
                OperationName = OperationName,
                TotalOperations = measurements.Count,
                SuccessfulOperations = successfulMeasurements.Count,
                FailedOperations = failedMeasurements.Count,
                SuccessRate = (double)successfulMeasurements.Count / measurements.Count,
                
                AverageDuration = successfulMeasurements.Any() 
                    ? TimeSpan.FromTicks((long)successfulMeasurements.Average(m => m.Duration.Ticks))
                    : TimeSpan.Zero,
                
                AverageMemoryDelta = successfulMeasurements.Any()
                    ? (long)successfulMeasurements.Average(m => m.MemoryDelta)
                    : 0,

                MinDuration = successfulMeasurements.Any() ? successfulMeasurements.Min(m => m.Duration) : TimeSpan.Zero,
                MaxDuration = successfulMeasurements.Any() ? successfulMeasurements.Max(m => m.Duration) : TimeSpan.Zero,
                
                MinMemoryDelta = successfulMeasurements.Any() ? successfulMeasurements.Min(m => m.MemoryDelta) : 0,
                MaxMemoryDelta = successfulMeasurements.Any() ? successfulMeasurements.Max(m => m.MemoryDelta) : 0,

                AverageGCGen0Impact = successfulMeasurements.Any()
                    ? successfulMeasurements.Average(m => m.GCAfter.Gen0Collections - m.GCBefore.Gen0Collections)
                    : 0,
                
                AverageGCGen1Impact = successfulMeasurements.Any()
                    ? successfulMeasurements.Average(m => m.GCAfter.Gen1Collections - m.GCBefore.Gen1Collections)
                    : 0,
                
                AverageGCGen2Impact = successfulMeasurements.Any()
                    ? successfulMeasurements.Average(m => m.GCAfter.Gen2Collections - m.GCBefore.Gen2Collections)
                    : 0
            };

            // Identify performance issues
            summary.PerformanceIssues = IdentifyPerformanceIssues(summary, measurements);

            return summary;
        }
    }

    private List<string> IdentifyPerformanceIssues(OperationSummary summary, List<OperationMeasurement> measurements)
    {
        var issues = new List<string>();

        // High memory allocation
        if (summary.AverageMemoryDelta > 10_000_000) // 10MB average
        {
            issues.Add($"High memory allocation ({summary.AverageMemoryDelta / 1_000_000:F1}MB average)");
        }

        // Frequent GC triggering
        if (summary.AverageGCGen2Impact > 0.1) // Frequent Gen2 collections
        {
            issues.Add($"Frequent Gen2 GC ({summary.AverageGCGen2Impact:F2} per operation)");
        }

        // High duration variance
        var durationVariance = summary.MaxDuration.TotalMilliseconds / Math.Max(1, summary.MinDuration.TotalMilliseconds);
        if (durationVariance > 10) // 10x variance
        {
            issues.Add($"High duration variance ({durationVariance:F1}x)");
        }

        // Memory leaks (consistently positive memory delta)
        var positiveDeltaCount = measurements.Count(m => m.MemoryDelta > 1_000_000); // 1MB+
        if (positiveDeltaCount > measurements.Count * 0.8) // 80% of operations
        {
            issues.Add("Potential memory leak (consistently positive memory delta)");
        }

        // Low success rate
        if (summary.SuccessRate < 0.95) // Less than 95% success
        {
            issues.Add($"Low success rate ({summary.SuccessRate:P1})");
        }

        return issues;
    }
}

public sealed class OperationMeasurement
{
    public TimeSpan Duration { get; init; }
    public long MemoryBefore { get; init; }
    public long MemoryAfter { get; init; }
    public long MemoryDelta { get; init; }
    public GCSnapshot GCBefore { get; init; } = new();
    public GCSnapshot GCAfter { get; init; } = new();
    public bool Success { get; init; }
    public string? Exception { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class GCSnapshot
{
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }

    public GCSnapshot()
    {
        Gen0Collections = GC.CollectionCount(0);
        Gen1Collections = GC.CollectionCount(1);
        Gen2Collections = GC.CollectionCount(2);
    }
}

public sealed class OperationSummary
{
    public string OperationName { get; init; } = string.Empty;
    public int TotalOperations { get; init; }
    public int SuccessfulOperations { get; init; }
    public int FailedOperations { get; init; }
    public double SuccessRate { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public TimeSpan MinDuration { get; init; }
    public TimeSpan MaxDuration { get; init; }
    public long AverageMemoryDelta { get; init; }
    public long MinMemoryDelta { get; init; }
    public long MaxMemoryDelta { get; init; }
    public double AverageGCGen0Impact { get; init; }
    public double AverageGCGen1Impact { get; init; }
    public double AverageGCGen2Impact { get; init; }
    public List<string> PerformanceIssues { get; init; } = new();
}

public sealed class MemoryPerformanceReport
{
    public DateTime ReportTimestamp { get; init; }
    public Dictionary<string, OperationSummary> OperationProfiles { get; init; } = new();
}
```

---

## 📚 Best Practices and Guidelines

### Memory Management Principles

#### 1. **Predictive Memory Management**
- Always estimate memory usage before allocation
- Implement memory pressure assessment for large operations
- Use progressive allocation strategies for risky operations
- Monitor system memory availability continuously

#### 2. **Resource Lifecycle Management**
- Implement RAII patterns for all resources
- Use `using` statements for automatic disposal
- Track resource lifetimes and implement automatic cleanup
- Pool frequently used resources to reduce allocation pressure

#### 3. **Memory Leak Prevention**
- Implement comprehensive leak detection systems
- Monitor allocation vs deallocation patterns
- Use weak references for event handlers and callbacks
- Regular memory pressure monitoring and cleanup

#### 4. **Performance Optimization**
- Profile memory usage patterns regularly
- Identify and optimize high-allocation operations
- Implement intelligent caching strategies
- Use object pooling for frequently allocated objects

### Development Guidelines

#### 1. **Memory-Safe Coding Patterns**
```csharp
// ✅ GOOD: Predictive memory assessment
var assessment = await _memoryMonitor.AssessAllocationAsync(estimatedSize, operationName);
if (assessment.AllowAllocation)
{
    using var resource = await _allocator.AllocateResourceAsync(() => new LargeResource(), estimatedSize, operationName);
    // Use resource safely
}

// ❌ BAD: Uncontrolled allocation
var resource = new LargeResource(); // No memory assessment, no disposal tracking
```

#### 2. **Resource Management Patterns**
```csharp
// ✅ GOOD: Comprehensive resource management
using var resourceManager = new AdvancedResourceManager(_logger);
var bitmap = resourceManager.AcquireBitmap(width, height);
var graphics = resourceManager.AcquireGraphics(bitmap, "capture-operation");
var deviceContext = resourceManager.AcquireDeviceContext(windowHandle);
// All resources automatically cleaned up on disposal

// ❌ BAD: Manual resource management
var bitmap = new Bitmap(width, height); // Manual disposal required
var graphics = Graphics.FromImage(bitmap); // Manual disposal required
var dc = Win32API.GetWindowDC(windowHandle); // Manual release required
```

#### 3. **Memory Monitoring Integration**
```csharp
// ✅ GOOD: Integrated monitoring
public async Task<CaptureResult> CaptureWithMonitoringAsync(CaptureRequest request)
{
    return await _performanceProfiler.ProfileOperationAsync(async () =>
    {
        _leakDetector.TrackResourceAllocation("Capture", request.EstimatedSize, request.OperationName);
        
        try
        {
            var result = await PerformCaptureAsync(request);
            return result;
        }
        finally
        {
            _leakDetector.TrackResourceDeallocation("Capture", request.EstimatedSize);
        }
    }, $"Capture-{request.OperationName}");
}
```

### Testing and Validation

#### 1. **Memory Testing Patterns**
```csharp
[TestMethod]
public async Task MemoryIntensiveOperation_WithMemoryPressure_HandlesGracefully()
{
    // Arrange
    var memoryMonitor = new AdvancedMemoryPressureMonitor(new MemoryThresholds
    {
        WarningThreshold = 10_000_000, // 10MB for testing
        CriticalThreshold = 20_000_000  // 20MB for testing
    });

    // Act & Assert
    using var operation = await memoryMonitor.AssessAllocationAsync(15_000_000, "TestOperation");
    
    Assert.IsNotNull(operation);
    Assert.AreEqual(MemoryRiskLevel.Warning, operation.RiskLevel);
    Assert.IsTrue(operation.AllowAllocation);
}
```

### Production Deployment

#### 1. **Memory Configuration**
```json
{
  "MemoryManagement": {
    "PressureMonitoring": {
      "WarningThreshold": 50000000,
      "CriticalThreshold": 100000000,
      "EmergencyThreshold": 200000000,
      "MonitoringInterval": "00:00:05"
    },
    "LeakDetection": {
      "DetectionInterval": "00:02:00",
      "MemoryGrowthThreshold": 50000000,
      "LeakConfidenceThreshold": 0.8
    },
    "PerformanceProfiling": {
      "ReportingInterval": "00:05:00",
      "EnableDetailedProfiling": true
    }
  }
}
```

#### 2. **Monitoring and Alerting**
- Set up memory usage alerts at 70% and 90% thresholds
- Monitor memory leak detection events
- Track GC performance and frequency
- Alert on memory allocation failures

---

*This guide provides comprehensive memory management strategies for the Visual Studio MCP Server. Following these patterns ensures optimal memory usage, prevents system instability, and maintains high performance during intensive visual capture operations.*