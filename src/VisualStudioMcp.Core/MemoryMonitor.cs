using System.Diagnostics;
using System.Runtime;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Core;

/// <summary>
/// Monitors memory usage and provides cleanup recommendations.
/// </summary>
public static class MemoryMonitor
{
    /// <summary>
    /// Gets the current memory usage information.
    /// </summary>
    /// <returns>Memory usage information.</returns>
    public static MemoryInfo GetMemoryInfo()
    {
        using var process = Process.GetCurrentProcess();
        
        return new MemoryInfo
        {
            WorkingSetMB = process.WorkingSet64 / 1024 / 1024,
            PrivateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024,
            VirtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024,
            GcTotalMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024,
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }

    /// <summary>
    /// Checks if memory usage is approaching concerning levels.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="workingSetThresholdMB">Working set threshold in MB (default: 500MB).</param>
    /// <returns>True if memory pressure is high, false otherwise.</returns>
    public static bool IsMemoryPressureHigh(ILogger logger, long workingSetThresholdMB = 500)
    {
        var memInfo = GetMemoryInfo();
        var isHighPressure = memInfo.WorkingSetMB > workingSetThresholdMB;

        if (isHighPressure)
        {
            logger.LogWarning("High memory pressure detected: WorkingSet={WorkingSetMB}MB, Threshold={ThresholdMB}MB, " +
                             "PrivateMemory={PrivateMemoryMB}MB, GCMemory={GcMemoryMB}MB",
                memInfo.WorkingSetMB, workingSetThresholdMB, memInfo.PrivateMemoryMB, memInfo.GcTotalMemoryMB);
        }
        else
        {
            logger.LogDebug("Memory usage within normal range: WorkingSet={WorkingSetMB}MB, Threshold={ThresholdMB}MB",
                memInfo.WorkingSetMB, workingSetThresholdMB);
        }

        return isHighPressure;
    }

    /// <summary>
    /// Performs memory cleanup operations.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="forceFullCollection">Whether to force a full GC collection (default: false).</param>
    public static void PerformMemoryCleanup(ILogger logger, bool forceFullCollection = false)
    {
        var beforeMemory = GetMemoryInfo();
        
        logger.LogInformation("Starting memory cleanup - Before: WorkingSet={WorkingSetMB}MB, GCMemory={GcMemoryMB}MB",
            beforeMemory.WorkingSetMB, beforeMemory.GcTotalMemoryMB);

        // Collect unreferenced objects
        if (forceFullCollection)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);
        }
        else
        {
            GC.Collect();
        }

        // Compact large object heap (LOH) if needed
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        if (forceFullCollection)
        {
            GC.Collect();
        }

        var afterMemory = GetMemoryInfo();
        
        logger.LogInformation("Memory cleanup completed - After: WorkingSet={WorkingSetMB}MB, GCMemory={GcMemoryMB}MB, " +
                             "Freed: {FreedMB}MB working set, {FreedGcMB}MB GC memory",
            afterMemory.WorkingSetMB, afterMemory.GcTotalMemoryMB,
            beforeMemory.WorkingSetMB - afterMemory.WorkingSetMB,
            beforeMemory.GcTotalMemoryMB - afterMemory.GcTotalMemoryMB);
    }

    /// <summary>
    /// Monitors memory usage continuously and performs cleanup when necessary.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="checkIntervalMs">Interval between memory checks in milliseconds (default: 30 seconds).</param>
    /// <param name="workingSetThresholdMB">Working set threshold for cleanup in MB (default: 500MB).</param>
    /// <param name="cancellationToken">Token to cancel monitoring.</param>
    /// <returns>A task that represents the monitoring operation.</returns>
    public static async Task MonitorMemoryAsync(
        ILogger logger, 
        int checkIntervalMs = 30000, 
        long workingSetThresholdMB = 500,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting memory monitoring - Interval: {IntervalMs}ms, Threshold: {ThresholdMB}MB",
            checkIntervalMs, workingSetThresholdMB);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (IsMemoryPressureHigh(logger, workingSetThresholdMB))
                {
                    PerformMemoryCleanup(logger, forceFullCollection: false);
                }

                await Task.Delay(checkIntervalMs, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during memory monitoring");
                await Task.Delay(checkIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogInformation("Memory monitoring stopped");
    }
}

/// <summary>
/// Represents current memory usage information.
/// </summary>
public record MemoryInfo
{
    /// <summary>
    /// Working set in megabytes.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Private memory in megabytes.
    /// </summary>
    public long PrivateMemoryMB { get; init; }

    /// <summary>
    /// Virtual memory in megabytes.
    /// </summary>
    public long VirtualMemoryMB { get; init; }

    /// <summary>
    /// Total managed memory in megabytes.
    /// </summary>
    public long GcTotalMemoryMB { get; init; }

    /// <summary>
    /// Number of generation 0 collections.
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Number of generation 1 collections.
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Number of generation 2 collections.
    /// </summary>
    public int Gen2Collections { get; init; }
}