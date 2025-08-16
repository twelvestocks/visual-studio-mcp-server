using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Core.Monitoring;

/// <summary>
/// Monitors memory usage and provides cleanup recommendations for COM interop operations.
/// </summary>
public static class MemoryMonitor
{
    private static readonly object _lock = new();
    private static DateTime _lastFullCollection = DateTime.MinValue;
    private static long _lastPrivateBytes = 0;
    private static readonly TimeSpan MinimumCollectionInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Checks if memory pressure is high enough to warrant cleanup before expensive operations.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="thresholdMb">Memory threshold in megabytes (default: 500MB).</param>
    /// <returns>True if memory pressure is high.</returns>
    public static bool IsMemoryPressureHigh(ILogger logger, long thresholdMb = 500)
    {
        try
        {
            lock (_lock)
            {
                var currentProcess = Process.GetCurrentProcess();
                var privateMemoryBytes = currentProcess.PrivateMemorySize64;
                var workingSetBytes = currentProcess.WorkingSet64;
                var thresholdBytes = thresholdMb * 1024 * 1024;

                var memoryInfo = new
                {
                    privateMemoryMb = privateMemoryBytes / (1024 * 1024),
                    workingSetMb = workingSetBytes / (1024 * 1024),
                    thresholdMb = thresholdMb,
                    gcTotalMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024),
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2)
                };

                logger.LogDebug("Memory status check: {@MemoryInfo}", memoryInfo);

                // Check if we're over the threshold
                bool isHighPressure = privateMemoryBytes > thresholdBytes || workingSetBytes > thresholdBytes;

                // Additional checks for memory growth patterns
                if (_lastPrivateBytes > 0)
                {
                    var growthBytes = privateMemoryBytes - _lastPrivateBytes;
                    var growthMb = growthBytes / (1024 * 1024);
                    
                    if (growthMb > 100) // Rapid growth of 100MB+ since last check
                    {
                        logger.LogWarning("Rapid memory growth detected: {GrowthMb}MB since last check", growthMb);
                        isHighPressure = true;
                    }
                }

                _lastPrivateBytes = privateMemoryBytes;

                if (isHighPressure)
                {
                    logger.LogWarning("High memory pressure detected: Private={PrivateMb}MB, WorkingSet={WorkingSetMb}MB, Threshold={ThresholdMb}MB",
                        memoryInfo.privateMemoryMb, memoryInfo.workingSetMb, thresholdMb);
                }

                return isHighPressure;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking memory pressure");
            return false; // Assume no pressure if we can't check
        }
    }

    /// <summary>
    /// Performs memory cleanup with configurable aggressiveness.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="forceFullCollection">Whether to force a full garbage collection.</param>
    /// <returns>Memory statistics before and after cleanup.</returns>
    public static MemoryCleanupResult PerformMemoryCleanup(ILogger logger, bool forceFullCollection = false)
    {
        try
        {
            lock (_lock)
            {
                var beforeCleanup = GetMemoryStatistics();
                
                logger.LogInformation("Starting memory cleanup: Before={@BeforeStats}", beforeCleanup);

                // Perform cleanup based on severity
                if (forceFullCollection || ShouldPerformFullCollection())
                {
                    logger.LogInformation("Performing aggressive memory cleanup with full GC collection");
                    
                    // Multiple generation collections for thorough cleanup
                    for (int i = 0; i <= 2; i++)
                    {
                        GC.Collect(i, GCCollectionMode.Forced, true);
                        GC.WaitForPendingFinalizers();
                    }
                    
                    // Final full collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    _lastFullCollection = DateTime.UtcNow;
                }
                else
                {
                    logger.LogInformation("Performing standard memory cleanup");
                    
                    // Standard cleanup - let GC decide
                    GC.Collect(0, GCCollectionMode.Optimized);
                    GC.WaitForPendingFinalizers();
                }

                var afterCleanup = GetMemoryStatistics();
                
                var result = new MemoryCleanupResult
                {
                    BeforeCleanup = beforeCleanup,
                    AfterCleanup = afterCleanup,
                    MemoryFreedMb = (beforeCleanup.PrivateMemoryMb - afterCleanup.PrivateMemoryMb),
                    CleanupType = forceFullCollection ? "Aggressive" : "Standard",
                    CleanupTime = DateTime.UtcNow
                };

                logger.LogInformation("Memory cleanup completed: {@CleanupResult}", result);
                
                return result;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during memory cleanup");
            
            return new MemoryCleanupResult
            {
                BeforeCleanup = GetMemoryStatistics(),
                AfterCleanup = GetMemoryStatistics(),
                MemoryFreedMb = 0,
                CleanupType = "Failed",
                CleanupTime = DateTime.UtcNow,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Gets comprehensive memory statistics for the current process.
    /// </summary>
    /// <returns>Current memory statistics.</returns>
    public static MemoryStatistics GetMemoryStatistics()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            
            return new MemoryStatistics
            {
                PrivateMemoryMb = currentProcess.PrivateMemorySize64 / (1024 * 1024),
                WorkingSetMb = currentProcess.WorkingSet64 / (1024 * 1024),
                VirtualMemoryMb = currentProcess.VirtualMemorySize64 / (1024 * 1024),
                PagedMemoryMb = currentProcess.PagedMemorySize64 / (1024 * 1024),
                GcTotalMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2),
                Timestamp = DateTime.UtcNow
            };
        }
        catch
        {
            // Return empty statistics if we can't get process info
            return new MemoryStatistics
            {
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Determines if a full garbage collection should be performed based on timing and pressure.
    /// </summary>
    private static bool ShouldPerformFullCollection()
    {
        // Don't perform full collections too frequently
        if (DateTime.UtcNow - _lastFullCollection < MinimumCollectionInterval)
        {
            return false;
        }

        // Check if Gen2 collections are getting frequent (indicates memory pressure)
        var gen2Count = GC.CollectionCount(2);
        
        // If we have many Gen2 collections, the system is under pressure
        return gen2Count > 10; // Arbitrary threshold - could be made configurable
    }

    /// <summary>
    /// Monitors COM object allocation patterns for leak detection.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the COM operation being monitored.</param>
    /// <returns>Disposable monitor that tracks the operation lifecycle.</returns>
    public static IDisposable MonitorComOperation(ILogger logger, string operationName)
    {
        return new ComOperationMonitor(logger, operationName);
    }

    /// <summary>
    /// Internal class for monitoring individual COM operations.
    /// </summary>
    private class ComOperationMonitor : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly MemoryStatistics _startMemory;
        private readonly DateTime _startTime;
        private bool _disposed = false;

        public ComOperationMonitor(ILogger logger, string operationName)
        {
            _logger = logger;
            _operationName = operationName;
            _startMemory = GetMemoryStatistics();
            _startTime = DateTime.UtcNow;

            _logger.LogDebug("COM operation started: {OperationName}, StartMemory={StartMemoryMb}MB", 
                operationName, _startMemory.PrivateMemoryMb);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            try
            {
                var endMemory = GetMemoryStatistics();
                var duration = DateTime.UtcNow - _startTime;
                var memoryDelta = endMemory.PrivateMemoryMb - _startMemory.PrivateMemoryMb;

                _logger.LogDebug("COM operation completed: {OperationName}, Duration={Duration}ms, MemoryDelta={MemoryDelta}MB",
                    _operationName, duration.TotalMilliseconds, memoryDelta);

                // Warn about potential memory leaks
                if (memoryDelta > 50) // More than 50MB increase
                {
                    _logger.LogWarning("Potential memory leak detected in COM operation {OperationName}: {MemoryDelta}MB increase",
                        _operationName, memoryDelta);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error monitoring COM operation {OperationName}", _operationName);
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}

/// <summary>
/// Represents memory statistics at a point in time.
/// </summary>
public class MemoryStatistics
{
    public long PrivateMemoryMb { get; set; }
    public long WorkingSetMb { get; set; }
    public long VirtualMemoryMb { get; set; }
    public long PagedMemoryMb { get; set; }
    public long GcTotalMemoryMb { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Represents the result of a memory cleanup operation.
/// </summary>
public class MemoryCleanupResult
{
    public MemoryStatistics BeforeCleanup { get; set; } = new();
    public MemoryStatistics AfterCleanup { get; set; } = new();
    public long MemoryFreedMb { get; set; }
    public string CleanupType { get; set; } = string.Empty;
    public DateTime CleanupTime { get; set; }
    public string? Error { get; set; }
}