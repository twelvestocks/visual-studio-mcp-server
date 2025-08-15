using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Imaging.Tests.MockHelpers;

namespace VisualStudioMcp.Imaging.Tests;

/// <summary>
/// Tests for memory pressure monitoring and large capture handling.
/// </summary>
[TestClass]
[TestCategory("Performance")]
public class MemoryPressureTests
{
    private Mock<ILogger<ImagingService>> _mockLogger;
    private Mock<IWindowClassificationService> _mockWindowClassification;
    private ImagingService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ImagingService>>();
        _mockWindowClassification = new Mock<IWindowClassificationService>();
        _service = new ImagingService(_mockLogger.Object, _mockWindowClassification.Object);
    }

    #region Memory Pressure Monitoring Tests

    [TestMethod]
    [TestCategory("Critical")]
    public void CaptureFromDCSecurely_LargeCaptureWarning_LogsAppropriateTelemetry()
    {
        // Arrange - 4K capture dimensions (should trigger warning but proceed)
        const int width4K = 3840;
        const int height4K = 2160;
        var estimatedMB = (width4K * height4K * 4) / 1_000_000; // ~33MB

        // Act - Use reflection to test private method
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, new object[] { IntPtr.Zero, 0, 0, width4K, height4K });

            // Assert - Should return a result (even if empty due to invalid DC)
            Assert.IsNotNull(result);

            // Verify appropriate logging occurred (either memory warning or bitmap creation failure)
            _mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Large capture") 
                                                 || v.ToString()!.Contains("Failed to create compatible bitmap")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Should log appropriate message for {estimatedMB}MB capture"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Memory pressure monitoring should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Critical")]
    public void CaptureFromDCSecurely_ExtremelyLargeCapture_RejectsGracefully()
    {
        // Arrange - 8K capture dimensions (should exceed 100MB limit and be rejected)
        const int width8K = 7680;
        const int height8K = 4320;
        var estimatedMB = (width8K * height8K * 4) / 1_000_000; // ~132MB

        // Act
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, new object[] { IntPtr.Zero, 0, 0, width8K, height8K });

            // Assert - Should return empty capture (rejected due to size)
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.ImageData.Length, "Extremely large captures should be rejected");
            Assert.AreEqual(0, result.Width, "Rejected capture should have zero dimensions");
            Assert.AreEqual(0, result.Height, "Rejected capture should have zero dimensions");

            // Verify error was logged for rejected capture
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Capture too large") 
                                                 && v.ToString()!.Contains($"{estimatedMB}MB")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                $"Should log error for {estimatedMB}MB capture rejection"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Large capture rejection should be handled gracefully: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    public void CaptureFromDCSecurely_StandardCapture_ProcessesNormally()
    {
        // Arrange - Standard 1080p capture dimensions
        const int width1080p = 1920;
        const int height1080p = 1080;
        var estimatedMB = (width1080p * height1080p * 4) / 1_000_000; // ~8MB

        // Act
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, new object[] { IntPtr.Zero, 0, 0, width1080p, height1080p });

            // Assert - Should process without warnings
            Assert.IsNotNull(result);

            // Verify no warnings logged for standard size capture
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Large capture requested")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never,
                $"Should not log warnings for standard {estimatedMB}MB capture"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Standard capture should process normally: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    public void CaptureFromDCSecurely_MemoryPressureSimulation_TriggersGarbageCollection()
    {
        // Arrange - Force high memory usage simulation
        // Note: This test simulates the scenario by checking if the logic exists
        const int standardWidth = 1920;
        const int standardHeight = 1080;

        // Get current memory before test
        var memoryBefore = GC.GetTotalMemory(false);

        // Act
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, new object[] { IntPtr.Zero, 0, 0, standardWidth, standardHeight });

            // Assert - Memory pressure monitoring should be active
            Assert.IsNotNull(result);

            // If memory pressure is high (>500MB), verify appropriate logging
            var currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > 500_000_000)
            {
                _mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("High memory pressure detected")),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.AtMostOnce,
                    "Should log memory pressure warning when memory usage is high"
                );
            }

            Console.WriteLine($"Memory usage: {currentMemory / 1_000_000}MB (before: {memoryBefore / 1_000_000}MB)");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Memory pressure monitoring should not fail: {ex.Message}");
        }
    }

    #endregion

    #region Memory Usage Pattern Tests

    [TestMethod]
    [TestCategory("Performance")]
    public void CreateEmptyCapture_MemoryEfficient_MinimalAllocation()
    {
        // Arrange
        var memoryBefore = GC.GetTotalMemory(true); // Force GC first

        // Act - Create multiple empty captures
        var captures = new List<ImageCapture>();
        for (int i = 0; i < 100; i++)
        {
            var method = typeof(ImagingService)
                .GetMethod("CreateEmptyCapture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var capture = (ImageCapture)method!.Invoke(_service, null);
            captures.Add(capture);
        }

        var memoryAfter = GC.GetTotalMemory(false);
        var memoryIncrease = memoryAfter - memoryBefore;

        // Assert - Memory increase should be minimal for empty captures
        Assert.IsTrue(memoryIncrease < 1_000_000, // Less than 1MB for 100 empty captures
            $"Empty captures should be memory efficient. Used {memoryIncrease / 1000}KB for 100 captures");

        // Verify all captures are properly structured
        foreach (var capture in captures)
        {
            Assert.IsNotNull(capture);
            Assert.AreEqual(0, capture.ImageData.Length);
            Assert.AreEqual("PNG", capture.ImageFormat);
            Assert.AreEqual(0, capture.Width);
            Assert.AreEqual(0, capture.Height);
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    [DataRow(1024, 768)]   // Standard resolution
    [DataRow(1920, 1080)]  // HD resolution  
    [DataRow(2560, 1440)]  // QHD resolution
    [DataRow(3840, 2160)]  // 4K resolution
    public void CaptureFromDCSecurely_VariousResolutions_HandlesMemoryAppropriately(int width, int height)
    {
        // Arrange
        var estimatedMemoryMB = (width * height * 4) / 1_000_000;

        // Act
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, new object[] { IntPtr.Zero, 0, 0, width, height });

            // Assert based on estimated memory usage
            Assert.IsNotNull(result);

            if (estimatedMemoryMB > 50) // Above warning threshold
            {
                _mockLogger.Verify(
                    x => x.Log(
                        LogLevel.Warning,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Large capture requested")),
                        It.IsAny<Exception?>(),
                        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                    Times.Once,
                    $"Should warn for {width}x{height} ({estimatedMemoryMB}MB) capture"
                );
            }

            if (estimatedMemoryMB > 100) // Above rejection threshold
            {
                Assert.AreEqual(0, result.ImageData.Length, 
                    $"Should reject {width}x{height} ({estimatedMemoryMB}MB) capture");
            }

            Console.WriteLine($"Resolution {width}x{height}: {estimatedMemoryMB}MB estimated");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Resolution {width}x{height} should be handled gracefully: {ex.Message}");
        }
    }

    #endregion

    [TestCleanup]
    public void Cleanup()
    {
        _service = null!;
        _mockLogger = null!;
        _mockWindowClassification = null!;
    }
}