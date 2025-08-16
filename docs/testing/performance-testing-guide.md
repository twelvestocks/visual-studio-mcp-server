# Performance Testing Guide

## Overview

This document provides comprehensive guidance for performance testing in the Visual Studio MCP Server project. It establishes production-grade performance thresholds, validation procedures, and testing methodologies to ensure optimal system performance.

## Performance Testing Philosophy

### Core Performance Principles
- **Production-Grade Standards**: Performance thresholds based on real enterprise requirements
- **Continuous Monitoring**: Performance validation integrated into development workflow
- **Regression Prevention**: Automated detection of performance degradation
- **Resource Efficiency**: Optimal utilisation of system resources (CPU, memory, I/O)
- **Scalability Validation**: System performance under varying load conditions

### Performance Quality Gates
- **Timing Thresholds**: All operations must complete within specified time limits
- **Memory Management**: Strict memory usage and leak prevention requirements
- **Concurrency Support**: System must handle specified concurrent load
- **Resource Cleanup**: No performance degradation over extended operation cycles
- **Baseline Tracking**: Performance metrics tracked against established baselines

## Production-Grade Thresholds

### Critical Operation Timing Requirements

#### GetRunningInstancesAsync: 1000ms Maximum
**Threshold**: ≤ 1000ms execution time  
**Previous Threshold**: 5000ms (reduced for production requirements)

**Rationale**:
- Enterprise environments typically run 3-5 Visual Studio instances simultaneously
- COM enumeration of multiple VS instances requires optimization for rapid response
- User experience demands quick instance discovery for workflow automation
- Network latency in enterprise environments requires efficient local operations

**Validation Method**:
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
public async Task GetRunningInstancesAsync_EnterpriseLoad_MeetsTimingThreshold()
{
    // Arrange: Setup service with enterprise-typical configuration
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var stopwatch = Stopwatch.StartNew();
    
    // Act: Execute instance discovery
    var instances = await service.GetRunningInstancesAsync(CancellationToken.None);
    stopwatch.Stop();
    
    // Assert: Verify timing threshold
    Assert.IsTrue(stopwatch.ElapsedMilliseconds <= 1000,
        $"GetRunningInstancesAsync took {stopwatch.ElapsedMilliseconds}ms, exceeds 1000ms threshold. " +
        $"Enterprise environments require rapid instance discovery for workflow automation.");
        
    // Verify functional correctness
    Assert.IsNotNull(instances);
    Assert.IsTrue(instances.Count >= 0); // May be zero if no VS instances running
}
```

**Performance Optimization Techniques**:
- **Cached Process Enumeration**: Cache process list for short periods to reduce system calls
- **Parallel Validation**: Validate multiple processes concurrently where safe
- **Early Termination**: Stop enumeration when sufficient instances found for common scenarios
- **Selective Filtering**: Apply process filtering early to reduce validation overhead

#### IsConnectionHealthyAsync: 500ms Maximum  
**Threshold**: ≤ 500ms execution time  
**Previous Threshold**: 2000ms (reduced for enterprise requirements)

**Rationale**:
- Health checks are performed frequently (every 30-60 seconds) in enterprise monitoring
- Rapid health validation enables quick failure detection and recovery
- COM connection validation should be lightweight and efficient
- Multiple concurrent health checks must not overwhelm system resources

**Validation Method**:
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.Concurrency)]
public async Task IsConnectionHealthyAsync_FrequentChecks_MeetsTimingThreshold()
{
    // Arrange: Setup service and simulate frequent health checking
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var healthCheckTasks = new List<Task<(bool isHealthy, long elapsedMs)>>();
    
    // Act: Execute 10 concurrent health checks (typical enterprise monitoring load)
    for (int i = 0; i < 10; i++)
    {
        healthCheckTasks.Add(Task.Run(async () => {
            var stopwatch = Stopwatch.StartNew();
            var isHealthy = await service.IsConnectionHealthyAsync(CancellationToken.None);
            stopwatch.Stop();
            return (isHealthy, stopwatch.ElapsedMilliseconds);
        }));
    }
    
    var results = await Task.WhenAll(healthCheckTasks);
    
    // Assert: Verify all health checks meet timing threshold
    foreach (var (isHealthy, elapsedMs) in results)
    {
        Assert.IsTrue(elapsedMs <= 500,
            $"Health check took {elapsedMs}ms, exceeds 500ms threshold. " +
            $"Enterprise monitoring requires rapid health validation.");
    }
    
    // Verify functional correctness
    Assert.IsTrue(results.All(r => r.isHealthy != null)); // Health status should be deterministic
}
```

**Performance Optimization Techniques**:
- **Connection Pooling**: Reuse validated COM connections when safe
- **Lightweight Validation**: Use minimal COM operations to verify connectivity
- **Timeout Management**: Implement aggressive timeouts for unresponsive connections
- **Circuit Breaker Pattern**: Temporarily skip validation for consistently failing connections

### Memory Management Requirements

#### Memory Growth Tolerance: 20MB Maximum
**Threshold**: ≤ 20MB memory growth per operation cycle  
**Previous Threshold**: 100MB (reduced for COM operation limits)

**Rationale**:
- COM interop operations have inherent memory overhead that must be managed carefully
- Enterprise applications run continuously for days/weeks and cannot tolerate memory leaks
- Visual Studio automation may involve large object graphs that require careful disposal
- Memory pressure can affect overall system performance and stability

**Validation Method**:
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.MemoryManagement)]
public async Task ExtendedOperationCycle_MemoryGrowth_WithinTolerance()
{
    // Arrange: Setup service and establish memory baseline
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    
    // Force garbage collection to establish clean baseline
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var baselineMemory = GC.GetTotalMemory(false);
    
    // Act: Execute 100 operation cycles (simulates extended usage)
    for (int cycle = 0; cycle < 100; cycle++)
    {
        await service.GetRunningInstancesAsync(CancellationToken.None);
        await service.IsConnectionHealthyAsync(CancellationToken.None);
        
        // Periodic garbage collection to ensure accurate measurement
        if (cycle % 10 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
    
    // Final garbage collection
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    var finalMemory = GC.GetTotalMemory(false);
    var memoryGrowth = finalMemory - baselineMemory;
    
    // Assert: Verify memory growth within tolerance
    var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
    Assert.IsTrue(memoryGrowthMB <= 20,
        $"Memory growth {memoryGrowthMB:F2}MB exceeds 20MB threshold after 100 cycles. " +
        $"COM operations must not cause memory leaks in extended usage scenarios.");
    
    // Log memory statistics for monitoring
    Console.WriteLine($"Memory baseline: {baselineMemory / 1024.0 / 1024.0:F2}MB");
    Console.WriteLine($"Memory final: {finalMemory / 1024.0 / 1024.0:F2}MB");  
    Console.WriteLine($"Memory growth: {memoryGrowthMB:F2}MB");
}
```

**Memory Optimization Techniques**:
- **Aggressive COM Cleanup**: Use `Marshal.ReleaseComObject()` consistently
- **Disposable Pattern**: Implement `IDisposable` for all resource-holding classes
- **Weak References**: Use weak references for cached COM objects
- **Periodic Cleanup**: Implement periodic cleanup of cached resources

#### Memory Pressure Detection: <5 seconds Response Time
**Threshold**: ≤ 5 seconds to detect and respond to memory pressure  
**Rationale**: Enterprise systems require rapid detection of resource constraints

**Validation Method**:
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.MemoryManagement)]
public async Task MemoryPressureDetection_RapidResponse_MeetsThreshold()
{
    // Arrange: Setup memory monitor
    var memoryMonitor = new MemoryMonitor(Mock.Of<ILogger<MemoryMonitor>>());
    var pressureDetected = false;
    var detectionTime = TimeSpan.Zero;
    
    memoryMonitor.MemoryPressureDetected += (sender, args) => {
        pressureDetected = true;
        detectionTime = args.DetectionTime;
    };
    
    // Act: Simulate memory pressure
    var stopwatch = Stopwatch.StartNew();
    await memoryMonitor.SimulateMemoryPressureAsync(); // Test-only method
    
    // Wait for pressure detection with timeout
    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
    var detectionTask = Task.Run(async () => {
        while (!pressureDetected && stopwatch.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(100);
        }
    });
    
    await Task.WhenAny(detectionTask, timeoutTask);
    stopwatch.Stop();
    
    // Assert: Verify rapid pressure detection
    Assert.IsTrue(pressureDetected, "Memory pressure was not detected within timeout period");
    Assert.IsTrue(detectionTime.TotalSeconds <= 5,
        $"Memory pressure detection took {detectionTime.TotalSeconds:F2} seconds, exceeds 5 second threshold");
}
```

### Concurrency Requirements

#### Concurrent Operations Support: 50 Minimum
**Threshold**: ≥ 50 simultaneous operations without degradation  
**Previous Threshold**: 10 (increased for enterprise requirements)

**Rationale**:
- Enterprise environments have multiple developers using VS automation simultaneously
- CI/CD pipelines may trigger multiple concurrent build and test operations
- System must maintain performance characteristics under realistic concurrent load
- Thread safety validation requires substantial concurrent operation testing

**Validation Method**:
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.Concurrency)]
public async Task ConcurrentOperations_EnterpriseLoad_MaintainsPerformance()
{
    // Arrange: Setup service for concurrent testing
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    const int concurrentOperations = 50;
    var operationTasks = new List<Task<OperationResult>>();
    
    // Act: Execute 50 concurrent operations
    var overallStopwatch = Stopwatch.StartNew();
    
    for (int i = 0; i < concurrentOperations; i++)
    {
        var operationId = i; // Capture for closure
        operationTasks.Add(Task.Run(async () => {
            var operationStopwatch = Stopwatch.StartNew();
            
            try
            {
                var instances = await service.GetRunningInstancesAsync(CancellationToken.None);
                var isHealthy = await service.IsConnectionHealthyAsync(CancellationToken.None);
                
                operationStopwatch.Stop();
                
                return new OperationResult 
                { 
                    OperationId = operationId,
                    Success = true, 
                    ElapsedMs = operationStopwatch.ElapsedMilliseconds,
                    InstanceCount = instances?.Count ?? 0,
                    IsHealthy = isHealthy
                };
            }
            catch (Exception ex)
            {
                operationStopwatch.Stop();
                return new OperationResult 
                { 
                    OperationId = operationId,
                    Success = false, 
                    ElapsedMs = operationStopwatch.ElapsedMilliseconds,
                    Exception = ex
                };
            }
        }));
    }
    
    var results = await Task.WhenAll(operationTasks);
    overallStopwatch.Stop();
    
    // Assert: Verify concurrent operation performance
    var successfulOperations = results.Where(r => r.Success).ToArray();
    var failedOperations = results.Where(r => !r.Success).ToArray();
    
    // At least 95% success rate under concurrent load
    var successRate = (double)successfulOperations.Length / concurrentOperations;
    Assert.IsTrue(successRate >= 0.95,
        $"Success rate {successRate:P2} under concurrent load is below 95% threshold");
    
    // Individual operation timing should not degrade significantly
    var averageOperationTime = successfulOperations.Average(r => r.ElapsedMs);
    Assert.IsTrue(averageOperationTime <= 2000, // Allow some degradation under load
        $"Average operation time {averageOperationTime:F0}ms under concurrent load exceeds 2000ms");
    
    // Overall concurrent completion should be efficient
    Assert.IsTrue(overallStopwatch.ElapsedMilliseconds <= 10000,
        $"Concurrent operations took {overallStopwatch.ElapsedMilliseconds}ms total, exceeds 10 second threshold");
    
    // Log detailed performance statistics
    Console.WriteLine($"Concurrent operations: {concurrentOperations}");
    Console.WriteLine($"Success rate: {successRate:P2}");
    Console.WriteLine($"Average operation time: {averageOperationTime:F0}ms");
    Console.WriteLine($"Total time: {overallStopwatch.ElapsedMilliseconds}ms");
    
    if (failedOperations.Length > 0)
    {
        Console.WriteLine($"Failed operations: {failedOperations.Length}");
        foreach (var failure in failedOperations.Take(5)) // Log first 5 failures
        {
            Console.WriteLine($"  Operation {failure.OperationId}: {failure.Exception?.Message}");
        }
    }
}

private class OperationResult
{
    public int OperationId { get; set; }
    public bool Success { get; set; }
    public long ElapsedMs { get; set; }
    public int InstanceCount { get; set; }
    public bool IsHealthy { get; set; }
    public Exception? Exception { get; set; }
}
```

**Concurrency Optimization Techniques**:
- **Thread-Safe COM Access**: Ensure COM apartment threading is handled correctly
- **Connection Pooling**: Share validated connections across concurrent operations
- **Resource Throttling**: Limit concurrent COM operations to prevent resource exhaustion
- **Async/Await Patterns**: Use proper async patterns to avoid thread blocking

## Performance Validation Procedures

### Automated Performance Regression Detection

#### CI/CD Performance Pipeline
```yaml
# Performance validation stage
performance_tests:
  stage: performance_validation
  script:
    - dotnet test --filter TestCategory=Performance --logger "trx;LogFileName=performance-results.trx"
    - dotnet run --project Tools.PerformanceAnalyzer -- performance-results.trx baseline-performance.json
  artifacts:
    reports:
      junit: "**/performance-results.trx"
    paths:
      - "performance-results.json"
      - "performance-trends.html"
  rules:
    - if: '$CI_PIPELINE_SOURCE == "merge_request_event"'
  allow_failure: false  # Block merge on performance regressions
```

#### Performance Baseline Management
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
public async Task PerformanceBaseline_Validation_TracksTrends()
{
    // Arrange: Load performance baseline data
    var baselineData = await PerformanceBaseline.LoadAsync("performance-baseline.json");
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    
    // Act: Execute performance measurement
    var metrics = await PerformanceProfiler.MeasureAsync(async () => {
        return await service.GetRunningInstancesAsync(CancellationToken.None);
    });
    
    // Assert: Compare against baseline with tolerance
    var regressionTolerance = 0.20; // 20% regression tolerance
    var baselineTime = baselineData.GetRunningInstancesAvgMs;
    var currentTime = metrics.ElapsedMilliseconds;
    
    var performanceRatio = (double)currentTime / baselineTime;
    Assert.IsTrue(performanceRatio <= (1.0 + regressionTolerance),
        $"Performance regression detected: {currentTime}ms vs baseline {baselineTime}ms " +
        $"(ratio: {performanceRatio:F2}, tolerance: {1.0 + regressionTolerance:F2})");
    
    // Update baseline if performance improved significantly
    if (performanceRatio <= 0.90) // 10% improvement
    {
        await baselineData.UpdateAsync("GetRunningInstancesAvgMs", currentTime);
        Console.WriteLine($"Performance baseline updated: {baselineTime}ms -> {currentTime}ms");
    }
}
```

### Performance Monitoring and Alerting

#### Runtime Performance Monitoring
```csharp
public class PerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly PerformanceCounter _processorCounter;
    private readonly PerformanceCounter _memoryCounter;
    
    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
        _processorCounter = new PerformanceCounter("Process", "% Processor Time", Process.GetCurrentProcess().ProcessName);
        _memoryCounter = new PerformanceCounter("Process", "Working Set", Process.GetCurrentProcess().ProcessName);
    }
    
    public async Task<PerformanceMetrics> MonitorOperationAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        var cpuBefore = _processorCounter.NextValue();
        
        try
        {
            var result = await operation();
            stopwatch.Stop();
            
            var memoryAfter = GC.GetTotalMemory(false);
            var cpuAfter = _processorCounter.NextValue();
            
            var metrics = new PerformanceMetrics
            {
                OperationName = operationName,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                MemoryDeltaBytes = memoryAfter - memoryBefore,
                CpuUsagePercent = cpuAfter - cpuBefore,
                Success = true
            };
            
            // Log performance metrics
            _logger.LogInformation("Performance: {OperationName} completed in {ElapsedMs}ms, " +
                "Memory delta: {MemoryDeltaMB:F2}MB, CPU: {CpuUsage:F1}%",
                operationName, metrics.ElapsedMs, metrics.MemoryDeltaBytes / 1024.0 / 1024.0, metrics.CpuUsagePercent);
            
            // Alert on performance thresholds
            await CheckPerformanceThresholdsAsync(metrics);
            
            return metrics;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Performance monitoring failed for operation: {OperationName}", operationName);
            throw;
        }
    }
    
    private async Task CheckPerformanceThresholdsAsync(PerformanceMetrics metrics)
    {
        var thresholds = await PerformanceThresholds.LoadAsync();
        
        if (metrics.ElapsedMs > thresholds.GetTimeoutMs(metrics.OperationName))
        {
            _logger.LogWarning("Performance threshold exceeded for {OperationName}: {ElapsedMs}ms > {ThresholdMs}ms",
                metrics.OperationName, metrics.ElapsedMs, thresholds.GetTimeoutMs(metrics.OperationName));
        }
        
        if (metrics.MemoryDeltaBytes > thresholds.GetMemoryThresholdBytes(metrics.OperationName))
        {
            _logger.LogWarning("Memory threshold exceeded for {OperationName}: {MemoryDeltaMB:F2}MB > {ThresholdMB:F2}MB",
                metrics.OperationName, metrics.MemoryDeltaBytes / 1024.0 / 1024.0, 
                thresholds.GetMemoryThresholdBytes(metrics.OperationName) / 1024.0 / 1024.0);
        }
    }
}
```

## Stress Testing Methodologies

### Load Testing Patterns

#### Sustained Load Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.Integration)]
public async Task SustainedLoad_ExtendedPeriod_MaintainsPerformance()
{
    // Arrange: Setup for extended load testing (30 minutes)
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var testDuration = TimeSpan.FromMinutes(30);
    var operationInterval = TimeSpan.FromSeconds(5);
    var performanceLog = new List<PerformanceMetrics>();
    
    var stopwatch = Stopwatch.StartNew();
    var operationCount = 0;
    
    // Act: Execute sustained load
    while (stopwatch.Elapsed < testDuration)
    {
        var operationStopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        
        try
        {
            await service.GetRunningInstancesAsync(CancellationToken.None);
            operationStopwatch.Stop();
            
            var memoryAfter = GC.GetTotalMemory(false);
            performanceLog.Add(new PerformanceMetrics
            {
                OperationName = "GetRunningInstances",
                ElapsedMs = operationStopwatch.ElapsedMilliseconds,
                MemoryDeltaBytes = memoryAfter - memoryBefore,
                Timestamp = DateTime.UtcNow,
                OperationNumber = ++operationCount
            });
        }
        catch (Exception ex)
        {
            Assert.Fail($"Operation failed during sustained load testing at {stopwatch.Elapsed}: {ex.Message}");
        }
        
        // Wait before next operation
        await Task.Delay(operationInterval);
        
        // Periodic garbage collection to prevent test artifacts
        if (operationCount % 20 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
    
    // Assert: Verify performance characteristics over time
    var firstQuarter = performanceLog.Take(performanceLog.Count / 4).ToArray();
    var lastQuarter = performanceLog.Skip(3 * performanceLog.Count / 4).ToArray();
    
    var firstQuarterAvgTime = firstQuarter.Average(m => m.ElapsedMs);
    var lastQuarterAvgTime = lastQuarter.Average(m => m.ElapsedMs);
    
    // Performance should not degrade by more than 50% over time
    var degradationRatio = lastQuarterAvgTime / firstQuarterAvgTime;
    Assert.IsTrue(degradationRatio <= 1.5,
        $"Performance degraded over time: {firstQuarterAvgTime:F0}ms -> {lastQuarterAvgTime:F0}ms " +
        $"(ratio: {degradationRatio:F2})");
    
    // Memory usage should remain stable
    var totalMemoryGrowth = performanceLog.Sum(m => m.MemoryDeltaBytes);
    var avgMemoryGrowthMB = totalMemoryGrowth / (1024.0 * 1024.0) / operationCount;
    Assert.IsTrue(avgMemoryGrowthMB <= 1.0,
        $"Average memory growth per operation {avgMemoryGrowthMB:F2}MB exceeds 1MB threshold");
    
    Console.WriteLine($"Sustained load test completed: {operationCount} operations over {stopwatch.Elapsed}");
    Console.WriteLine($"Average operation time: First quarter {firstQuarterAvgTime:F0}ms, Last quarter {lastQuarterAvgTime:F0}ms");
    Console.WriteLine($"Performance degradation ratio: {degradationRatio:F2}");
}
```

#### Spike Load Testing
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.Concurrency)]
public async Task SpikeLoad_SuddenIncrease_HandlesGracefully()
{
    // Arrange: Setup for spike load testing
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var baselineOperations = 5;
    var spikeOperations = 100;
    
    // Act: Execute baseline load
    var baselineTasks = Enumerable.Range(0, baselineOperations)
        .Select(_ => MeasureOperationAsync(service))
        .ToArray();
    
    var baselineResults = await Task.WhenAll(baselineTasks);
    var baselineAvgTime = baselineResults.Average(r => r.ElapsedMs);
    
    // Wait for system to stabilize
    await Task.Delay(TimeSpan.FromSeconds(5));
    
    // Execute spike load
    var spikeTasks = Enumerable.Range(0, spikeOperations)
        .Select(_ => MeasureOperationAsync(service))
        .ToArray();
    
    var spikeResults = await Task.WhenAll(spikeTasks);
    
    // Assert: Verify graceful handling of spike load
    var successfulSpikeOperations = spikeResults.Where(r => r.Success).ToArray();
    var spikeSuccessRate = (double)successfulSpikeOperations.Length / spikeOperations;
    
    Assert.IsTrue(spikeSuccessRate >= 0.90,
        $"Spike load success rate {spikeSuccessRate:P2} below 90% threshold");
    
    var spikeAvgTime = successfulSpikeOperations.Average(r => r.ElapsedMs);
    var spikeSlowdownRatio = spikeAvgTime / baselineAvgTime;
    
    // Allow reasonable slowdown under spike load (up to 3x)
    Assert.IsTrue(spikeSlowdownRatio <= 3.0,
        $"Spike load caused excessive slowdown: {baselineAvgTime:F0}ms -> {spikeAvgTime:F0}ms " +
        $"(ratio: {spikeSlowdownRatio:F2})");
}

private async Task<OperationResult> MeasureOperationAsync(VisualStudioService service)
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await service.GetRunningInstancesAsync(CancellationToken.None);
        stopwatch.Stop();
        return new OperationResult { Success = true, ElapsedMs = stopwatch.ElapsedMilliseconds };
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        return new OperationResult { Success = false, ElapsedMs = stopwatch.ElapsedMilliseconds, Exception = ex };
    }
}
```

## Memory Management Testing

### Memory Leak Detection
```csharp
[TestMethod]
[TestCategory(TestCategories.Performance)]
[TestCategory(TestCategories.MemoryManagement)]
public async Task MemoryLeakDetection_ExtendedCycles_NoLeaks()
{
    // Arrange: Setup for memory leak detection
    var service = new VisualStudioService(Mock.Of<ILogger<VisualStudioService>>());
    var cycleCount = 1000;
    var memoryMeasurements = new List<long>();
    
    // Initial garbage collection
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    
    // Act: Execute operation cycles with memory monitoring
    for (int cycle = 0; cycle < cycleCount; cycle++)
    {
        await service.GetRunningInstancesAsync(CancellationToken.None);
        
        // Measure memory every 50 cycles
        if (cycle % 50 == 0)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var currentMemory = GC.GetTotalMemory(false);
            memoryMeasurements.Add(currentMemory);
        }
    }
    
    // Final memory measurement
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    memoryMeasurements.Add(GC.GetTotalMemory(false));
    
    // Assert: Verify no significant memory leak
    var initialMemory = memoryMeasurements.First();
    var finalMemory = memoryMeasurements.Last();
    var memoryGrowth = finalMemory - initialMemory;
    var memoryGrowthMB = memoryGrowth / (1024.0 * 1024.0);
    
    Assert.IsTrue(memoryGrowthMB <= 10,
        $"Memory leak detected: {memoryGrowthMB:F2}MB growth over {cycleCount} cycles");
    
    // Verify memory growth trend
    var memoryTrend = CalculateLinearTrend(memoryMeasurements);
    Assert.IsTrue(memoryTrend.Slope <= 1024 * 10, // Max 10KB growth per measurement interval
        $"Memory growth trend indicates leak: {memoryTrend.Slope:F0} bytes per measurement");
}

private LinearTrend CalculateLinearTrend(List<long> values)
{
    var n = values.Count;
    var sumX = Enumerable.Range(0, n).Sum();
    var sumY = values.Sum();
    var sumXY = values.Select((y, x) => x * y).Sum();
    var sumXX = Enumerable.Range(0, n).Sum(x => x * x);
    
    var slope = (n * sumXY - sumX * sumY) / (double)(n * sumXX - sumX * sumX);
    var intercept = (sumY - slope * sumX) / n;
    
    return new LinearTrend { Slope = slope, Intercept = intercept };
}

private class LinearTrend
{
    public double Slope { get; set; }
    public double Intercept { get; set; }
}
```

## Performance Test Utilities

### Performance Profiler
```csharp
public class PerformanceProfiler
{
    public static async Task<PerformanceMetrics> MeasureAsync<T>(Func<Task<T>> operation, string operationName = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(false);
        var cpuBefore = Environment.TickCount;
        
        Exception exception = null;
        T result = default(T);
        
        try
        {
            result = await operation();
        }
        catch (Exception ex)
        {
            exception = ex;
        }
        finally
        {
            stopwatch.Stop();
        }
        
        var memoryAfter = GC.GetTotalMemory(false);
        var cpuAfter = Environment.TickCount;
        
        return new PerformanceMetrics
        {
            OperationName = operationName ?? "Unknown",
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            MemoryDeltaBytes = memoryAfter - memoryBefore,
            CpuTimeMs = cpuAfter - cpuBefore,
            Success = exception == null,
            Exception = exception,
            Result = result,
            Timestamp = DateTime.UtcNow
        };
    }
    
    public static async Task<PerformanceMetrics[]> MeasureConcurrentAsync<T>(
        Func<Task<T>> operation, 
        int concurrentCount, 
        string operationName = null)
    {
        var tasks = Enumerable.Range(0, concurrentCount)
            .Select(i => MeasureAsync(operation, $"{operationName ?? "Unknown"}-{i}"))
            .ToArray();
            
        return await Task.WhenAll(tasks);
    }
}

public class PerformanceMetrics
{
    public string OperationName { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
    public long MemoryDeltaBytes { get; set; }
    public long CpuTimeMs { get; set; }
    public double CpuUsagePercent { get; set; }
    public bool Success { get; set; }
    public Exception? Exception { get; set; }
    public object? Result { get; set; }
    public DateTime Timestamp { get; set; }
    public int OperationNumber { get; set; }
}
```

## Conclusion

This Performance Testing Guide establishes production-grade performance standards for the Visual Studio MCP Server project. Key achievements include:

- **Realistic Thresholds**: Performance requirements based on enterprise usage patterns
- **Comprehensive Testing**: Coverage of timing, memory, and concurrency requirements
- **Automated Validation**: CI/CD integration for continuous performance monitoring
- **Regression Prevention**: Baseline tracking and automated regression detection
- **Optimization Guidance**: Specific techniques for performance improvement

Regular application of these performance testing standards ensures the system maintains optimal performance characteristics throughout its development lifecycle and in production environments.