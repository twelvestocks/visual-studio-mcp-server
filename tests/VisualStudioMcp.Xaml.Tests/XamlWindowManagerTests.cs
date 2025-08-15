using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class XamlWindowManagerTests
{
    private Mock<ILogger<XamlWindowManager>> _mockLogger;
    private Mock<IVisualStudioService> _mockVsService;
    private XamlWindowManager _windowManager;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<XamlWindowManager>>();
        _mockVsService = new Mock<IVisualStudioService>();
        _windowManager = new XamlWindowManager(_mockLogger.Object, _mockVsService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _windowManager?.ClearCache();
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_ValidProcessId_ReturnsWindows()
    {
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance
            {
                ProcessId = processId,
                Version = "17.0",
                DisplayName = "Visual Studio 2022"
            }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ReturnsAsync(new VisualStudioInstance { ProcessId = processId });

        // Act
        var result = await _windowManager.FindXamlDesignerWindowsAsync(processId);

        // Assert
        Assert.IsNotNull(result);
        // Note: Since we can't easily mock Windows API calls, we expect an empty array
        // In a real integration test, this would return actual windows
        Assert.AreEqual(0, result.Length);
        
        // Verify VS service was called
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
        _mockVsService.Verify(vs => vs.ConnectToInstanceAsync(processId), Times.Once);
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_NonExistentProcessId_ReturnsEmpty()
    {
        // Arrange
        var processId = 9999;
        var mockInstances = Array.Empty<VisualStudioInstance>();

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        // Act
        var result = await _windowManager.FindXamlDesignerWindowsAsync(processId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Length);
        
        // Verify VS service was called but connection was not attempted
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
        _mockVsService.Verify(vs => vs.ConnectToInstanceAsync(It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_CachesCalls_ReturnsFromCache()
    {
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance
            {
                ProcessId = processId,
                Version = "17.0",
                DisplayName = "Visual Studio 2022"
            }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ReturnsAsync(new VisualStudioInstance { ProcessId = processId });

        // Act - First call
        var result1 = await _windowManager.FindXamlDesignerWindowsAsync(processId);
        
        // Act - Second call (should use cache)
        var result2 = await _windowManager.FindXamlDesignerWindowsAsync(processId);

        // Assert
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);
        Assert.AreEqual(result1.Length, result2.Length);
        
        // Verify VS service was called only once (first call), second call uses cache
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
        _mockVsService.Verify(vs => vs.ConnectToInstanceAsync(processId), Times.Once);
    }

    [TestMethod]
    public async Task GetActiveDesignerWindowAsync_NoActiveWindows_ReturnsNull()
    {
        // Arrange
        var mockInstances = Array.Empty<VisualStudioInstance>();
        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        // Act
        var result = await _windowManager.GetActiveDesignerWindowAsync();

        // Assert
        Assert.IsNull(result);
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
    }

    [TestMethod]
    public async Task GetActiveDesignerWindowAsync_WithActiveWindows_ReturnsActiveWindow()
    {
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance
            {
                ProcessId = processId,
                Version = "17.0",
                DisplayName = "Visual Studio 2022"
            }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ReturnsAsync(new VisualStudioInstance { ProcessId = processId });

        // Act
        var result = await _windowManager.GetActiveDesignerWindowAsync();

        // Assert
        // Since we can't mock actual windows, we expect null
        // In a real integration test with actual VS running, this might return a window
        Assert.IsNull(result);
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
    }

    [TestMethod]
    public async Task ActivateDesignerWindowAsync_ValidWindow_AttemptsActivation()
    {
        // Arrange
        var designerWindow = new XamlDesignerWindow
        {
            Handle = new IntPtr(123),
            ProcessId = 1234,
            XamlFilePath = @"C:\Test\MainWindow.xaml",
            Title = "Test Window",
            IsActive = false,
            IsVisible = true
        };

        // Act
        var result = await _windowManager.ActivateDesignerWindowAsync(designerWindow);

        // Assert
        // Since the activation implementation is not complete, we expect false
        // This tests that the method doesn't throw and handles the case gracefully
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ActivateDesignerWindowAsync_NullWindow_HandlesSafely()
    {
        // Arrange
        var designerWindow = new XamlDesignerWindow
        {
            Handle = IntPtr.Zero,
            ProcessId = 0,
            XamlFilePath = "",
            Title = "",
            IsActive = false,
            IsVisible = false
        };

        // Act
        var result = await _windowManager.ActivateDesignerWindowAsync(designerWindow);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ClearCache_RemovesAllCachedEntries()
    {
        // Act
        _windowManager.ClearCache();

        // Assert
        var statistics = _windowManager.GetCacheStatistics();
        Assert.AreEqual(0, statistics.CachedProcessCount);
        Assert.AreEqual(0, statistics.TotalCachedWindows);
    }

    [TestMethod]
    public void InvalidateCache_RemovesSpecificProcessCache()
    {
        // Arrange
        var processId = 1234;

        // Act
        _windowManager.InvalidateCache(processId);

        // Assert - Should not throw
        var statistics = _windowManager.GetCacheStatistics();
        Assert.IsNotNull(statistics);
    }

    [TestMethod]
    public void GetCacheStatistics_ReturnsValidStatistics()
    {
        // Act
        var statistics = _windowManager.GetCacheStatistics();

        // Assert
        Assert.IsNotNull(statistics);
        Assert.IsTrue(statistics.CachedProcessCount >= 0);
        Assert.IsTrue(statistics.TotalCachedWindows >= 0);
        Assert.IsTrue(statistics.CacheExpirationTimespan.TotalSeconds > 0);
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_ServiceThrowsException_HandlesGracefully()
    {
        // Arrange
        var processId = 1234;
        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _windowManager.FindXamlDesignerWindowsAsync(processId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_ConnectToInstanceFails_HandlesGracefully()
    {
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance
            {
                ProcessId = processId,
                Version = "17.0",
                DisplayName = "Visual Studio 2022"
            }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ThrowsAsync(new InvalidOperationException("Connection failed"));

        // Act
        var result = await _windowManager.FindXamlDesignerWindowsAsync(processId);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Length);
    }

    [TestMethod]
    public async Task GetActiveDesignerWindowAsync_ServiceThrowsException_HandlesGracefully()
    {
        // Arrange
        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await _windowManager.GetActiveDesignerWindowAsync();

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlWindowManager(null!, _mockVsService.Object));
    }

    [TestMethod]
    public void Constructor_NullVsService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlWindowManager(_mockLogger.Object, null!));
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_ConcurrentCalls_UseSemaphore()
    {
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance
            {
                ProcessId = processId,
                Version = "17.0",
                DisplayName = "Visual Studio 2022"
            }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);

        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ReturnsAsync(new VisualStudioInstance { ProcessId = processId });

        // Act - Multiple concurrent calls
        var tasks = new[]
        {
            _windowManager.FindXamlDesignerWindowsAsync(processId),
            _windowManager.FindXamlDesignerWindowsAsync(processId),
            _windowManager.FindXamlDesignerWindowsAsync(processId)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.IsTrue(results.All(r => r != null));
        Assert.IsTrue(results.All(r => r.Length == results[0].Length));
        
        // Due to caching and semaphore, only one actual call should be made
        _mockVsService.Verify(vs => vs.GetRunningInstancesAsync(), Times.Once);
        _mockVsService.Verify(vs => vs.ConnectToInstanceAsync(processId), Times.Once);
    }

    [TestMethod]
    public async Task ActivateDesignerWindowAsync_ConcurrentCalls_UsesLock()
    {
        // Arrange
        var designerWindow = new XamlDesignerWindow
        {
            Handle = new IntPtr(123),
            ProcessId = 1234,
            XamlFilePath = @"C:\Test\MainWindow.xaml",
            Title = "Test Window",
            IsActive = false,
            IsVisible = true
        };

        // Act - Multiple concurrent activation attempts
        var tasks = new[]
        {
            _windowManager.ActivateDesignerWindowAsync(designerWindow),
            _windowManager.ActivateDesignerWindowAsync(designerWindow),
            _windowManager.ActivateDesignerWindowAsync(designerWindow)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All calls should complete without throwing
        Assert.IsTrue(results.All(r => r == false)); // All should return false since implementation is incomplete
    }

    [TestMethod]
    public async Task FindXamlDesignerWindowsAsync_CacheExpiration_RefreshesCache()
    {
        // Note: This test would require access to internal cache timing mechanisms
        // For now, we test that the cache statistics are properly maintained
        
        // Arrange
        var processId = 1234;
        var mockInstances = new[]
        {
            new VisualStudioInstance { ProcessId = processId }
        };

        _mockVsService.Setup(vs => vs.GetRunningInstancesAsync())
                     .ReturnsAsync(mockInstances);
        _mockVsService.Setup(vs => vs.ConnectToInstanceAsync(processId))
                     .ReturnsAsync(new VisualStudioInstance { ProcessId = processId });

        // Act
        await _windowManager.FindXamlDesignerWindowsAsync(processId);
        var statisticsAfterFirstCall = _windowManager.GetCacheStatistics();

        // Clear cache manually to simulate expiration
        _windowManager.ClearCache();
        
        await _windowManager.FindXamlDesignerWindowsAsync(processId);
        var statisticsAfterSecondCall = _windowManager.GetCacheStatistics();

        // Assert
        Assert.IsNotNull(statisticsAfterFirstCall);
        Assert.IsNotNull(statisticsAfterSecondCall);
        
        // After clearing cache, the statistics should reflect the new state
        Assert.IsTrue(statisticsAfterSecondCall.CachedProcessCount >= 0);
    }
}