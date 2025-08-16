using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Integration.Tests;

[TestClass]
public class PerformanceTests
{
    private TestLogger<VisualStudioService> _testLogger = null!;
    private VisualStudioService _visualStudioService = null!;

    [TestInitialize]
    public void Setup()
    {
        _testLogger = new TestLogger<VisualStudioService>();
        _visualStudioService = new VisualStudioService(_testLogger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _visualStudioService?.Dispose();
    }

    [TestMethod]
    public async Task GetRunningInstancesAsync_Performance_CompletesWithin5Seconds()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 5000;

        // Act
        await _visualStudioService.GetRunningInstancesAsync();
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"GetRunningInstancesAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task IsConnectionHealthyAsync_Performance_CompletesWithin2Seconds()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 2000;
        const int testProcessId = 99999; // Non-existent process

        // Act
        await _visualStudioService.IsConnectionHealthyAsync(testProcessId);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"IsConnectionHealthyAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void ComInteropHelper_Performance_SafeOperationCompletesFast()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 100;
        var testOperation = () => "Fast operation";

        // Act
        var result = ComInteropHelper.SafeComOperation(testOperation, _testLogger, "PerfTest");
        stopwatch.Stop();

        // Assert
        Assert.AreEqual("Fast operation", result);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"SafeComOperation completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void MemoryMonitor_Performance_GetMemoryInfoCompletesFast()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 50;

        // Act
        var memoryInfo = MemoryMonitor.GetMemoryInfo();
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(memoryInfo);
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Operation took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"GetMemoryInfo completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task ConcurrentOperations_Performance_HandlesMultipleRequestsEfficiently()
    {
        // Arrange
        const int concurrentRequests = 10;
        const int maxAllowedMs = 10000; // 10 seconds for all operations
        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = new List<Task>();
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks.Add(_visualStudioService.GetRunningInstancesAsync());
            tasks.Add(_visualStudioService.IsConnectionHealthyAsync(i + 1000));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Concurrent operations took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"Concurrent operations ({concurrentRequests * 2} tasks) completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task MemoryUsage_Performance_StaysWithinReasonableLimits()
    {
        // Arrange
        var initialMemory = MemoryMonitor.GetMemoryInfo();
        const int operations = 50;

        // Act - Perform many operations
        for (int i = 0; i < operations; i++)
        {
            await _visualStudioService.GetRunningInstancesAsync();
            await _visualStudioService.IsConnectionHealthyAsync(i + 1000);
            
            // Occasional memory check
            if (i % 10 == 0)
            {
                var currentMemory = MemoryMonitor.GetMemoryInfo();
                Console.WriteLine($"Operation {i}: Working Set = {currentMemory.WorkingSetMB}MB, GC Memory = {currentMemory.GcTotalMemoryMB}MB");
            }
        }

        var finalMemory = MemoryMonitor.GetMemoryInfo();

        // Assert - Memory shouldn't grow excessively
        var memoryGrowthMB = finalMemory.WorkingSetMB - initialMemory.WorkingSetMB;
        const long maxAllowedGrowthMB = 100; // 100MB max growth

        Assert.IsTrue(memoryGrowthMB < maxAllowedGrowthMB, 
            $"Memory grew by {memoryGrowthMB}MB, exceeding {maxAllowedGrowthMB}MB limit");
        Console.WriteLine($"Memory growth after {operations} operations: {memoryGrowthMB}MB");
    }

    [TestMethod]
    public async Task ComInteropHelper_Timeout_Performance_RespectsTimeoutLimits()
    {
        // Arrange
        const int timeoutMs = 100;
        var slowOperation = () =>
        {
            Thread.Sleep(200); // Slower than timeout
            return "Should timeout";
        };

        var stopwatch = Stopwatch.StartNew();

        // Act & Assert
        try
        {
            await ComInteropHelper.SafeComOperationWithTimeoutAsync(slowOperation, _testLogger, "TimeoutTest", timeoutMs);
            Assert.Fail("Expected timeout exception was not thrown");
        }
        catch (ComInteropException ex) when (ex.Message.Contains("timed out"))
        {
            stopwatch.Stop();
            
            // Should timeout close to the specified limit
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= timeoutMs);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < timeoutMs + 100); // Allow small margin
            Console.WriteLine($"Timeout operation completed in {stopwatch.ElapsedMilliseconds}ms (expected ~{timeoutMs}ms)");
        }
    }

    [TestMethod]
    public void MemoryMonitor_Performance_HighPressureDetectionFast()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 10;

        // Act
        var isHighPressure = MemoryMonitor.IsMemoryPressureHigh(_testLogger, 1); // Very low threshold
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(isHighPressure); // Should detect high pressure with low threshold
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Memory pressure detection took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"Memory pressure detection completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void MemoryMonitor_Performance_CleanupCompletesWithinReasonableTime()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();
        const int maxAllowedMs = 2000; // 2 seconds for cleanup

        // Act
        MemoryMonitor.PerformMemoryCleanup(_testLogger, forceFullCollection: false);
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs, 
            $"Memory cleanup took {stopwatch.ElapsedMilliseconds}ms, exceeding {maxAllowedMs}ms limit");
        Console.WriteLine($"Memory cleanup completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public async Task StressTest_Performance_HandlesManyOperationsWithoutDegradation()
    {
        // Arrange
        const int iterations = 25;
        var timings = new List<long>();

        // Act - Measure performance over multiple iterations
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await _visualStudioService.GetRunningInstancesAsync();
            sw.Stop();
            timings.Add(sw.ElapsedMilliseconds);
        }

        // Assert - Performance shouldn't degrade significantly over time
        var firstQuartile = timings.Take(iterations / 4).Average();
        var lastQuartile = timings.Skip(3 * iterations / 4).Average();
        var degradationRatio = lastQuartile / firstQuartile;

        Assert.IsTrue(degradationRatio < 2.0, 
            $"Performance degraded by {degradationRatio:F2}x (from {firstQuartile:F1}ms to {lastQuartile:F1}ms average)");
        Console.WriteLine($"Performance over {iterations} iterations: First quarter avg = {firstQuartile:F1}ms, Last quarter avg = {lastQuartile:F1}ms");
    }
}