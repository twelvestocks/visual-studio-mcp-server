using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Core;

namespace VisualStudioMcp.Core.Tests;

[TestClass]
public class MemoryMonitorTests
{
    private Mock<ILogger> _mockLogger = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [TestMethod]
    public void GetMemoryInfo_ReturnsValidMemoryInformation()
    {
        // Act
        var memoryInfo = MemoryMonitor.GetMemoryInfo();

        // Assert
        Assert.IsNotNull(memoryInfo);
        Assert.IsTrue(memoryInfo.WorkingSetMB >= 0);
        Assert.IsTrue(memoryInfo.PrivateMemoryMB >= 0);
        Assert.IsTrue(memoryInfo.VirtualMemoryMB >= 0);
        Assert.IsTrue(memoryInfo.GcTotalMemoryMB >= 0);
        Assert.IsTrue(memoryInfo.Gen0Collections >= 0);
        Assert.IsTrue(memoryInfo.Gen1Collections >= 0);
        Assert.IsTrue(memoryInfo.Gen2Collections >= 0);
    }

    [TestMethod]
    public void IsMemoryPressureHigh_WithLowMemoryUsage_ReturnsFalse()
    {
        // Arrange - Set a very high threshold
        var highThreshold = 10000; // 10GB

        // Act
        var isHighPressure = MemoryMonitor.IsMemoryPressureHigh(_mockLogger.Object, highThreshold);

        // Assert
        Assert.IsFalse(isHighPressure);
        VerifyLogCall("Memory usage within normal range", LogLevel.Debug);
    }

    [TestMethod]
    public void IsMemoryPressureHigh_WithHighMemoryUsage_ReturnsTrue()
    {
        // Arrange - Set a very low threshold
        var lowThreshold = 1; // 1MB

        // Act
        var isHighPressure = MemoryMonitor.IsMemoryPressureHigh(_mockLogger.Object, lowThreshold);

        // Assert
        Assert.IsTrue(isHighPressure);
        VerifyLogCall("High memory pressure detected", LogLevel.Warning);
    }

    [TestMethod]
    public void PerformMemoryCleanup_WithoutForceCollection_CompletesSuccessfully()
    {
        // Arrange
        var initialMemory = MemoryMonitor.GetMemoryInfo();

        // Act
        MemoryMonitor.PerformMemoryCleanup(_mockLogger.Object, forceFullCollection: false);

        // Assert
        var finalMemory = MemoryMonitor.GetMemoryInfo();
        VerifyLogCall("Starting memory cleanup", LogLevel.Information);
        VerifyLogCall("Memory cleanup completed", LogLevel.Information);
        
        // Memory should be tracked properly
        Assert.IsTrue(finalMemory.GcTotalMemoryMB >= 0);
    }

    [TestMethod]
    public void PerformMemoryCleanup_WithForceCollection_CompletesSuccessfully()
    {
        // Arrange
        var initialMemory = MemoryMonitor.GetMemoryInfo();

        // Act
        MemoryMonitor.PerformMemoryCleanup(_mockLogger.Object, forceFullCollection: true);

        // Assert
        var finalMemory = MemoryMonitor.GetMemoryInfo();
        VerifyLogCall("Starting memory cleanup", LogLevel.Information);
        VerifyLogCall("Memory cleanup completed", LogLevel.Information);
        
        // Forced collection should have occurred
        Assert.IsTrue(finalMemory.Gen2Collections >= initialMemory.Gen2Collections);
    }

    [TestMethod]
    public async Task MonitorMemoryAsync_WithCancellation_StopsMonitoring()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var monitoringTask = MemoryMonitor.MonitorMemoryAsync(_mockLogger.Object, 100, 1000, cts.Token);

        // Act
        await Task.Delay(50); // Let monitoring start
        cts.Cancel();
        await monitoringTask;

        // Assert
        VerifyLogCall("Starting memory monitoring", LogLevel.Information);
        VerifyLogCall("Memory monitoring stopped", LogLevel.Information);
    }

    [TestMethod]
    public async Task MonitorMemoryAsync_WithHighMemoryPressure_PerformsCleanup()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var lowThreshold = 1; // 1MB - will trigger cleanup
        var monitoringTask = MemoryMonitor.MonitorMemoryAsync(_mockLogger.Object, 50, lowThreshold, cts.Token);

        // Act
        await Task.Delay(100); // Let monitoring run and detect high pressure
        cts.Cancel();
        await monitoringTask;

        // Assert
        VerifyLogCall("Starting memory monitoring", LogLevel.Information);
        VerifyLogCall("High memory pressure detected", LogLevel.Warning);
    }

    [TestMethod]
    public void MemoryInfo_Properties_AreCorrectlySet()
    {
        // Arrange
        var memoryInfo = new MemoryInfo
        {
            WorkingSetMB = 100,
            PrivateMemoryMB = 80,
            VirtualMemoryMB = 200,
            GcTotalMemoryMB = 50,
            Gen0Collections = 10,
            Gen1Collections = 5,
            Gen2Collections = 2
        };

        // Assert
        Assert.AreEqual(100, memoryInfo.WorkingSetMB);
        Assert.AreEqual(80, memoryInfo.PrivateMemoryMB);
        Assert.AreEqual(200, memoryInfo.VirtualMemoryMB);
        Assert.AreEqual(50, memoryInfo.GcTotalMemoryMB);
        Assert.AreEqual(10, memoryInfo.Gen0Collections);
        Assert.AreEqual(5, memoryInfo.Gen1Collections);
        Assert.AreEqual(2, memoryInfo.Gen2Collections);
    }

    [TestMethod]
    public void MemoryInfo_Record_SupportsEquality()
    {
        // Arrange
        var memoryInfo1 = new MemoryInfo
        {
            WorkingSetMB = 100,
            PrivateMemoryMB = 80,
            VirtualMemoryMB = 200,
            GcTotalMemoryMB = 50,
            Gen0Collections = 10,
            Gen1Collections = 5,
            Gen2Collections = 2
        };

        var memoryInfo2 = new MemoryInfo
        {
            WorkingSetMB = 100,
            PrivateMemoryMB = 80,
            VirtualMemoryMB = 200,
            GcTotalMemoryMB = 50,
            Gen0Collections = 10,
            Gen1Collections = 5,
            Gen2Collections = 2
        };

        var memoryInfo3 = new MemoryInfo
        {
            WorkingSetMB = 200, // Different value
            PrivateMemoryMB = 80,
            VirtualMemoryMB = 200,
            GcTotalMemoryMB = 50,
            Gen0Collections = 10,
            Gen1Collections = 5,
            Gen2Collections = 2
        };

        // Assert
        Assert.AreEqual(memoryInfo1, memoryInfo2);
        Assert.AreNotEqual(memoryInfo1, memoryInfo3);
        Assert.AreEqual(memoryInfo1.GetHashCode(), memoryInfo2.GetHashCode());
        Assert.AreNotEqual(memoryInfo1.GetHashCode(), memoryInfo3.GetHashCode());
    }

    [TestMethod]
    public void GetMemoryInfo_ConsecutiveCalls_ShowsReasonableValues()
    {
        // Act
        var memoryInfo1 = MemoryMonitor.GetMemoryInfo();
        
        // Force some memory allocation
        var largeArray = new byte[1024 * 1024]; // 1MB
        
        var memoryInfo2 = MemoryMonitor.GetMemoryInfo();

        // Assert
        Assert.IsTrue(memoryInfo2.GcTotalMemoryMB >= memoryInfo1.GcTotalMemoryMB);
        
        // Clean up
        largeArray = null;
        GC.Collect();
    }

    private void VerifyLogCall(string expectedMessage, LogLevel expectedLevel)
    {
        _mockLogger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}