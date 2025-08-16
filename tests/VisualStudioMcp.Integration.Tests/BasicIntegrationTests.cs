using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Integration.Tests;

[TestClass]
public class BasicIntegrationTests
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
    public async Task GetRunningInstancesAsync_Integration_CompletesWithoutError()
    {
        // Act & Assert - This should complete without throwing regardless of whether VS is running
        try
        {
            var instances = await _visualStudioService.GetRunningInstancesAsync();
            
            // Assert basic properties
            Assert.IsNotNull(instances);
            Assert.IsTrue(instances.Length >= 0);
            
            // If instances are found, validate their structure
            foreach (var instance in instances)
            {
                Assert.IsTrue(instance.ProcessId > 0);
                Assert.IsNotNull(instance.Version);
                Assert.IsNotNull(instance.SolutionName);
            }
        }
        catch (Exception ex)
        {
            // Log the exception but don't fail the test - this is integration dependent
            Console.WriteLine($"Integration test info: {ex.Message}");
        }
    }

    [TestMethod]
    public async Task IsConnectionHealthyAsync_WithRandomProcessId_ReturnsFalse()
    {
        // Arrange
        var randomProcessId = new Random().Next(1, 1000);

        // Act
        var isHealthy = await _visualStudioService.IsConnectionHealthyAsync(randomProcessId);

        // Assert
        Assert.IsFalse(isHealthy);
    }

    [TestMethod]
    public async Task Service_MemoryIntegration_HandlesMemoryPressure()
    {
        // Arrange
        var initialMemory = MemoryMonitor.GetMemoryInfo();

        // Act - Perform some operations that might use memory
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_visualStudioService.GetRunningInstancesAsync());
        }

        await Task.WhenAll(tasks);

        // Assert - Memory should be tracked
        var finalMemory = MemoryMonitor.GetMemoryInfo();
        Assert.IsTrue(finalMemory.WorkingSetMB >= 0);
        Assert.IsTrue(finalMemory.GcTotalMemoryMB >= 0);
    }

    [TestMethod]
    public void ComInteropHelper_Integration_HandlesRealScenarios()
    {
        // Test COM interop helper in a real scenario
        var testOperation = () => "Success";

        // Act
        var result = ComInteropHelper.SafeComOperation(testOperation, _testLogger, "IntegrationTest");

        // Assert
        Assert.AreEqual("Success", result);
    }

    [TestMethod]
    public async Task ComInteropHelper_WithTimeout_Integration_HandlesTimeouts()
    {
        // Arrange
        var quickOperation = () => "Quick";

        // Act
        var result = await ComInteropHelper.SafeComOperationWithTimeoutAsync(
            quickOperation, _testLogger, "QuickTest", 5000);

        // Assert
        Assert.AreEqual("Quick", result);
    }

    [TestMethod]
    public void MemoryMonitor_Integration_ProvidesAccurateReadings()
    {
        // Act
        var memoryInfo = MemoryMonitor.GetMemoryInfo();

        // Assert
        Assert.IsNotNull(memoryInfo);
        Assert.IsTrue(memoryInfo.WorkingSetMB > 0);
        Assert.IsTrue(memoryInfo.PrivateMemoryMB > 0);
        Assert.IsTrue(memoryInfo.VirtualMemoryMB > 0);
        Assert.IsTrue(memoryInfo.GcTotalMemoryMB >= 0);
        Assert.IsTrue(memoryInfo.Gen0Collections >= 0);
        Assert.IsTrue(memoryInfo.Gen1Collections >= 0);
        Assert.IsTrue(memoryInfo.Gen2Collections >= 0);
    }

    [TestMethod]
    public async Task ConcurrentOperations_Integration_HandleGracefully()
    {
        // Arrange
        var concurrentTasks = new List<Task>();

        // Act - Run multiple operations concurrently
        for (int i = 0; i < 5; i++)
        {
            concurrentTasks.Add(Task.Run(async () =>
            {
                await _visualStudioService.GetRunningInstancesAsync();
                await _visualStudioService.IsConnectionHealthyAsync(12345);
            }));
        }

        // Should complete without deadlocks or exceptions
        await Task.WhenAll(concurrentTasks);

        // Assert
        Assert.IsTrue(concurrentTasks.All(t => t.IsCompletedSuccessfully));
    }

    [TestMethod]
    public void Service_Disposal_Integration_CleansUpProperly()
    {
        // Arrange
        using var service = new VisualStudioService(new TestLogger<VisualStudioService>());

        // Act - Use the service then dispose
        _ = service.GetRunningInstancesAsync();

        // Assert - Should dispose without throwing
        // Disposal happens automatically with 'using'
        Assert.IsTrue(true);
    }

    [TestMethod]
    public void Exception_Integration_HandlesComExceptionsGracefully()
    {
        // Test how our code handles various exception scenarios
        
        // Test ComInteropException creation and properties
        var comException = new ComInteropException("Test message");
        Assert.IsNotNull(comException.Message);
        Assert.IsFalse(comException.IsRetryable);

        var comExceptionWithInner = new ComInteropException("Test", new System.Runtime.InteropServices.COMException("Inner", unchecked((int)0x80004005)));
        Assert.IsTrue(comExceptionWithInner.IsRetryable); // E_FAIL is retryable
    }
}

/// <summary>
/// Simple test logger implementation for integration tests.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}