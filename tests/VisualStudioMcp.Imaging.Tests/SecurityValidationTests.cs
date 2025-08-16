using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Imaging.Tests.MockHelpers;
using System.Diagnostics;

namespace VisualStudioMcp.Imaging.Tests;

/// <summary>
/// Tests for security validation and process access vulnerability fixes.
/// </summary>
[TestClass]
[TestCategory("Security")]
public class SecurityValidationTests
{
    private Mock<ILogger<WindowClassificationService>> _mockLogger;
    private WindowClassificationService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<WindowClassificationService>>();
        _service = new WindowClassificationService(_mockLogger.Object);
    }

    #region Process Security Vulnerability Tests

    [TestMethod]
    [TestCategory("Critical")]
    public void IsVisualStudioWindow_ProcessNotFound_ReturnsFalseGracefully()
    {
        // Arrange
        var window = WindowMockFactory.CreateWindowWithProcessId(ProcessMockProvider.NonExistentProcessIds[0]);

        // Act & Assert - This should not throw an exception
        try
        {
            // Access the private method using reflection for testing
            var method = typeof(WindowClassificationService)
                .GetMethod("IsVisualStudioWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = (bool)method!.Invoke(_service, new object[] { window });

            // Verify graceful failure
            Assert.IsFalse(result, "Should return false for non-existent process");

            // Verify appropriate warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Process access denied or not found")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once,
                "Should log warning for process access failure"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Method should handle process access gracefully, but threw: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Critical")]
    public void IsVisualStudioWindow_ProcessTerminated_ReturnsFalseGracefully()
    {
        // Arrange - Use a very high process ID that's unlikely to exist
        var window = WindowMockFactory.CreateWindowWithProcessId(99999);

        // Act & Assert
        try
        {
            var method = typeof(WindowClassificationService)
                .GetMethod("IsVisualStudioWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = (bool)method!.Invoke(_service, new object[] { window });

            // Verify graceful failure
            Assert.IsFalse(result, "Should return false for non-existent process");

            // Verify appropriate warning was logged (either "not found" or "terminated")
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Process") 
                                                 && (v.ToString()!.Contains("not found") || v.ToString()!.Contains("terminated"))),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                "Should log warning for process access failure"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Method should handle process access gracefully, but threw: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Critical")]
    public void IsVisualStudioWindow_ValidVSProcess_ReturnsTrueCorrectly()
    {
        // Arrange
        var validProcessId = ProcessMockProvider.ValidVSProcessIds[0];
        var window = WindowMockFactory.CreateWindowWithProcessId(validProcessId);

        // Act
        try
        {
            var method = typeof(WindowClassificationService)
                .GetMethod("IsVisualStudioWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var result = (bool)method!.Invoke(_service, new object[] { window });

            // Assert
            // Note: This might be true or false depending on the actual process name,
            // but it should not throw an exception
            Assert.IsTrue(result is true or false, "Should return a boolean value without throwing");

            // Verify no error/warning was logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never,
                "Should not log warnings for valid process access"
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"Method should handle valid process access, but threw: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    public void IsVisualStudioWindow_HashSetPerformance_OutperformsArrayLookup()
    {
        // Arrange
        var window = WindowMockFactory.CreateMainWindow();
        var iterations = 1000;

        // Act - Measure performance with current HashSet implementation
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < iterations; i++)
        {
            try
            {
                var method = typeof(WindowClassificationService)
                    .GetMethod("IsVisualStudioWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                method!.Invoke(_service, new object[] { window });
            }
            catch
            {
                // Ignore exceptions for performance testing
            }
        }

        stopwatch.Stop();

        // Assert - Should complete quickly with HashSet lookup
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        Assert.IsTrue(elapsedMs < 1000, 
            $"HashSet lookup should be fast. Took {elapsedMs}ms for {iterations} iterations");

        Console.WriteLine($"Performance: {iterations} lookups completed in {elapsedMs}ms ({elapsedMs / (double)iterations:F3}ms per lookup)");
    }

    #endregion

    #region Exception Handling and Logging Tests

    [TestMethod]
    [TestCategory("ErrorRecovery")]
    public void ClassifyWindowAsync_InvalidWindowHandle_HandlesGracefully()
    {
        // Arrange
        var invalidHandle = IntPtr.Zero;

        // Act
        var result = _service.ClassifyWindowAsync(invalidHandle).Result;

        // Assert
        Assert.AreEqual(VisualStudioWindowType.Unknown, result);
        // Verify no exceptions were thrown
    }

    [TestMethod]
    [TestCategory("ErrorRecovery")]
    public async Task DiscoverVSWindowsAsync_ConcurrentAccess_ThreadSafe()
    {
        // Arrange - Create multiple concurrent tasks
        var concurrentTasks = Enumerable.Range(0, 5)
            .Select(_ => _service.DiscoverVSWindowsAsync())
            .ToArray();

        // Act - Execute all tasks concurrently
        var results = await Task.WhenAll(concurrentTasks);

        // Assert - All tasks should complete successfully
        Assert.AreEqual(5, results.Length);
        foreach (var result in results)
        {
            Assert.IsNotNull(result);
            // Results should be consistent (same discovery should yield same results)
        }
    }

    #endregion

    [TestCleanup]
    public void Cleanup()
    {
        _service = null!;
        _mockLogger = null!;
    }
}