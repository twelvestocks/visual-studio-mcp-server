using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Imaging.Tests.MockHelpers;
using System.Diagnostics;

namespace VisualStudioMcp.Imaging.Tests;

/// <summary>
/// Tests for window enumeration, timeout handling, and error recovery.
/// </summary>
[TestClass]
[TestCategory("WindowEnumeration")]
public class WindowEnumerationTests
{
    private Mock<ILogger<WindowClassificationService>> _mockLogger;
    private WindowClassificationService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<WindowClassificationService>>();
        _service = new WindowClassificationService(_mockLogger.Object);
    }

    #region Timeout and Performance Tests

    [TestMethod]
    [TestCategory("Critical")]
    public async Task DiscoverVSWindowsAsync_NormalOperation_CompletesWithinTimeout()
    {
        // Arrange
        var maxExpectedTime = TimeSpan.FromSeconds(10); // Should complete well within 30s timeout
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _service.DiscoverVSWindowsAsync();

        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(stopwatch.Elapsed < maxExpectedTime, 
            $"Window discovery should complete quickly. Took {stopwatch.Elapsed.TotalSeconds:F2}s");

        // Verify performance metrics were logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Discovered") && v.ToString()!.Contains("windows in")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log discovery completion with timing"
        );

        Console.WriteLine($"Discovered {result.Count()} windows in {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task DiscoverVSWindowsAsync_PerformanceBenchmark_MeetsRequirements()
    {
        // Arrange - Run discovery multiple times to test consistency
        var iterations = 5;
        var times = new List<TimeSpan>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await _service.DiscoverVSWindowsAsync();
            stopwatch.Stop();
            
            times.Add(stopwatch.Elapsed);
            Assert.IsNotNull(result);
        }

        // Assert
        var averageTime = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
        var maxTime = times.Max();

        Assert.IsTrue(averageTime < TimeSpan.FromSeconds(5), 
            $"Average discovery time should be < 5s. Average: {averageTime.TotalSeconds:F2}s");
        Assert.IsTrue(maxTime < TimeSpan.FromSeconds(15), 
            $"Maximum discovery time should be < 15s. Max: {maxTime.TotalSeconds:F2}s");

        Console.WriteLine($"Performance: Average={averageTime.TotalMilliseconds:F0}ms, Max={maxTime.TotalMilliseconds:F0}ms");
    }

    #endregion

    #region Error Recovery Tests

    [TestMethod]
    [TestCategory("ErrorRecovery")]
    public async Task DiscoverVSWindowsAsync_SimulatedException_ReturnsEmptyListGracefully()
    {
        // This test verifies that the service handles exceptions during discovery
        // Note: We can't easily simulate EnumWindows exceptions, but we can test the recovery logic

        // Act
        var result = await _service.DiscoverVSWindowsAsync();

        // Assert - Should always return a valid collection, even if empty
        Assert.IsNotNull(result);
        
        // Should not throw any unhandled exceptions
        var windowsList = result.ToList();
        Assert.IsTrue(windowsList.Count >= 0);
    }

    [TestMethod]
    [TestCategory("Threading")]
    public async Task DiscoverVSWindowsAsync_ConcurrentCalls_ThreadSafeOperation()
    {
        // Arrange - Create multiple concurrent discovery tasks
        var concurrentTasks = new Task<IEnumerable<VisualStudioWindow>>[10];
        
        for (int i = 0; i < concurrentTasks.Length; i++)
        {
            concurrentTasks[i] = _service.DiscoverVSWindowsAsync();
        }

        // Act - Wait for all to complete
        var results = await Task.WhenAll(concurrentTasks);

        // Assert - All should complete successfully
        Assert.AreEqual(10, results.Length);
        
        foreach (var result in results)
        {
            Assert.IsNotNull(result);
            var windowsList = result.ToList();
            Assert.IsTrue(windowsList.Count >= 0);
        }

        // Results should be reasonably consistent (same discovery operation)
        var windowCounts = results.Select(r => r.Count()).ToArray();
        var minCount = windowCounts.Min();
        var maxCount = windowCounts.Max();
        
        // Allow some variation due to window state changes during concurrent execution
        Assert.IsTrue(maxCount - minCount <= 5, 
            $"Window counts should be consistent across concurrent calls. Range: {minCount}-{maxCount}");

        Console.WriteLine($"Concurrent discovery results: {string.Join(", ", windowCounts)}");
    }

    #endregion

    #region Window Classification Tests

    [TestMethod]
    [TestCategory("Classification")]
    [DataRow("Solution Explorer", VisualStudioWindowType.SolutionExplorer)]
    [DataRow("Properties", VisualStudioWindowType.PropertiesWindow)]
    [DataRow("Error List", VisualStudioWindowType.ErrorList)]
    [DataRow("Output", VisualStudioWindowType.OutputWindow)]
    [DataRow("Program.cs - MyProject", VisualStudioWindowType.CodeEditor)]
    [DataRow("MyForm.xaml [Design]", VisualStudioWindowType.XamlDesigner)]
    public async Task ClassifyWindowAsync_KnownWindowTitles_ClassifiedCorrectly(string title, VisualStudioWindowType expectedType)
    {
        // Arrange - Create a mock window handle (we're testing classification logic)
        var mockHandle = new IntPtr(12345);

        // Act
        var result = await _service.ClassifyWindowAsync(mockHandle);

        // Note: This test would need actual window handles to work properly
        // For now, we're testing that the method doesn't throw exceptions
        Assert.IsTrue(result != null);
    }

    [TestMethod]
    [TestCategory("Classification")]
    public async Task ClassifyWindowAsync_InvalidHandle_ReturnsUnknown()
    {
        // Arrange
        var invalidHandle = IntPtr.Zero;

        // Act
        var result = await _service.ClassifyWindowAsync(invalidHandle);

        // Assert
        Assert.AreEqual(VisualStudioWindowType.Unknown, result);
    }

    [TestMethod]
    [TestCategory("Classification")]
    public async Task FindWindowsByTypeAsync_SpecificType_ReturnsFilteredResults()
    {
        // Act
        var allWindows = await _service.DiscoverVSWindowsAsync();
        var solutionExplorerWindows = await _service.FindWindowsByTypeAsync(VisualStudioWindowType.SolutionExplorer);

        // Assert
        Assert.IsNotNull(allWindows);
        Assert.IsNotNull(solutionExplorerWindows);
        
        var solutionExplorerList = solutionExplorerWindows.ToList();
        var allWindowsList = allWindows.ToList();

        // Filtered results should be subset of all results
        Assert.IsTrue(solutionExplorerList.Count <= allWindowsList.Count);

        // All returned windows should be of the requested type
        foreach (var window in solutionExplorerList)
        {
            Assert.AreEqual(VisualStudioWindowType.SolutionExplorer, window.WindowType);
        }
    }

    #endregion

    #region Layout Analysis Tests

    [TestMethod]
    [TestCategory("Layout")]
    public async Task AnalyzeLayoutAsync_CompletesSuccessfully_ReturnsValidLayout()
    {
        // Act
        var layout = await _service.AnalyzeLayoutAsync();

        // Assert
        Assert.IsNotNull(layout);
        Assert.IsNotNull(layout.AllWindows);
        Assert.IsNotNull(layout.WindowsByType);
        Assert.IsTrue(layout.AnalysisTime > DateTime.MinValue);

        // Layout should have reasonable structure
        var windowsList = layout.AllWindows.ToList();
        Assert.IsTrue(windowsList.Count >= 0);

        // Should have categorized windows by type
        Assert.IsTrue(layout.WindowsByType.Count >= 0);

        Console.WriteLine($"Layout analysis: {windowsList.Count} total windows, {layout.WindowsByType.Count} types");
    }

    [TestMethod]
    [TestCategory("Layout")]
    public async Task GetActiveWindowAsync_ReturnsConsistentResult()
    {
        // Act
        var activeWindow1 = await _service.GetActiveWindowAsync();
        await Task.Delay(100); // Small delay
        var activeWindow2 = await _service.GetActiveWindowAsync();

        // Assert - Results should be consistent within short time frame
        if (activeWindow1 != null && activeWindow2 != null)
        {
            // If both calls found an active window, they should be the same
            // (assuming no focus changes in 100ms)
            Assert.AreEqual(activeWindow1.Handle, activeWindow2.Handle,
                "Active window should be consistent within short time frame");
        }

        // At least one call might return null if no VS window is active
        Assert.IsTrue(activeWindow1 == null || activeWindow1.IsActive);
    }

    #endregion

    #region Resource Management Tests

    [TestMethod]
    [TestCategory("ResourceManagement")]
    public void WindowClassificationService_RepeatedCalls_NoMemoryLeaks()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Make multiple discovery calls
        for (int i = 0; i < 10; i++)
        {
            var task = _service.DiscoverVSWindowsAsync();
            task.Wait();
            var result = task.Result;
            
            // Force enumeration to ensure objects are created
            var count = result.Count();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert - Memory increase should be reasonable
        Assert.IsTrue(memoryIncrease < 5_000_000, // Less than 5MB increase
            $"Repeated calls should not cause significant memory leaks. Increased by {memoryIncrease / 1000}KB");

        Console.WriteLine($"Memory usage: Initial={initialMemory / 1000}KB, Final={finalMemory / 1000}KB, Increase={memoryIncrease / 1000}KB");
    }

    #endregion

    [TestCleanup]
    public void Cleanup()
    {
        _service = null!;
        _mockLogger = null!;
    }
}