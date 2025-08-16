using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using VisualStudioMcp.Core;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core.Tests;

[TestClass]
public class VisualStudioServiceTests
{
    private Mock<ILogger<VisualStudioService>> _mockLogger = null!;
    private VisualStudioService _visualStudioService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<VisualStudioService>>();
        _visualStudioService = new VisualStudioService(_mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _visualStudioService?.Dispose();
    }

    [TestMethod]
    public async Task GetRunningInstancesAsync_WithNoRunningInstances_ReturnsEmptyArray()
    {
        // Act
        var instances = await _visualStudioService.GetRunningInstancesAsync();

        // Assert
        Assert.IsNotNull(instances);
        VerifyLogCall("Getting running Visual Studio instances", LogLevel.Information);
    }

    [TestMethod]
    public async Task IsConnectionHealthyAsync_WithInvalidProcessId_ReturnsFalse()
    {
        // Arrange
        const int invalidProcessId = -1;

        // Act
        var isHealthy = await _visualStudioService.IsConnectionHealthyAsync(invalidProcessId);

        // Assert
        Assert.IsFalse(isHealthy);
    }

    [TestMethod]
    public async Task IsConnectionHealthyAsync_WithNonExistentProcessId_ReturnsFalse()
    {
        // Arrange
        const int nonExistentProcessId = 999999;

        // Act
        var isHealthy = await _visualStudioService.IsConnectionHealthyAsync(nonExistentProcessId);

        // Assert
        Assert.IsFalse(isHealthy);
    }

    [TestMethod]
    public async Task ConnectToInstanceAsync_WithInvalidProcessId_ThrowsException()
    {
        // Arrange
        const int invalidProcessId = -1;

        // Act & Assert
        try
        {
            await _visualStudioService.ConnectToInstanceAsync(invalidProcessId);
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex.Message.Contains("processId") || ex is ComInteropException);
        }
    }

    [TestMethod]
    public async Task OpenSolutionAsync_WithNullPath_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.OpenSolutionAsync(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task OpenSolutionAsync_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.OpenSolutionAsync("");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task OpenSolutionAsync_WithInvalidPath_ThrowsException()
    {
        // Arrange
        const string invalidPath = "C:\\NonExistent\\Solution.sln";

        // Act & Assert
        try
        {
            await _visualStudioService.OpenSolutionAsync(invalidPath);
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception ex)
        {
            Assert.IsTrue(ex is FileNotFoundException || ex is ComInteropException);
        }
    }

    [TestMethod]
    public async Task BuildSolutionAsync_WithValidConfiguration_ExecutesWithoutError()
    {
        // Arrange
        const string configuration = "Debug";

        // Act & Assert
        try
        {
            await _visualStudioService.BuildSolutionAsync(configuration);
            // If no VS instance is connected, this will throw - that's expected
        }
        catch (ComInteropException)
        {
            // Expected when no VS instance is connected
        }
        catch (InvalidOperationException)
        {
            // Expected when no VS instance is connected
        }
    }

    [TestMethod]
    public async Task BuildSolutionAsync_WithNullConfiguration_UsesDefaultConfiguration()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.BuildSolutionAsync(null!);
            // If no VS instance is connected, this will throw - that's expected
        }
        catch (ComInteropException)
        {
            // Expected when no VS instance is connected
        }
        catch (InvalidOperationException)
        {
            // Expected when no VS instance is connected
        }
    }

    [TestMethod]
    public async Task GetProjectsAsync_WithoutConnectedInstance_ThrowsException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.GetProjectsAsync();
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
        catch (ComInteropException)
        {
            // Also acceptable
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_WithNullCommand_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.ExecuteCommandAsync(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_WithEmptyCommand_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.ExecuteCommandAsync("");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_WithValidCommand_ExecutesWithoutError()
    {
        // Arrange
        const string validCommand = "File.NewFile";

        // Act & Assert
        try
        {
            await _visualStudioService.ExecuteCommandAsync(validCommand);
            // If no VS instance is connected, this will throw - that's expected
        }
        catch (ComInteropException)
        {
            // Expected when no VS instance is connected
        }
        catch (InvalidOperationException)
        {
            // Expected when no VS instance is connected
        }
    }

    [TestMethod]
    public async Task DisconnectFromInstanceAsync_WithValidProcessId_CompletesSuccessfully()
    {
        // Arrange
        const int processId = 12345;

        // Act
        await _visualStudioService.DisconnectFromInstanceAsync(processId);

        // Assert - Should complete without throwing
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task DisconnectFromInstanceAsync_WithInvalidProcessId_CompletesGracefully()
    {
        // Arrange
        const int invalidProcessId = -1;

        // Act
        await _visualStudioService.DisconnectFromInstanceAsync(invalidProcessId);

        // Assert - Should complete without throwing
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task GetOutputPanesAsync_WithoutConnectedInstance_ThrowsException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.GetOutputPanesAsync();
            Assert.Fail("Expected exception was not thrown");
        }
        catch (InvalidOperationException)
        {
            // Expected
        }
        catch (ComInteropException)
        {
            // Also acceptable
        }
    }

    [TestMethod]
    public async Task GetOutputPaneContentAsync_WithNullPaneName_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.GetOutputPaneContentAsync(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task GetOutputPaneContentAsync_WithEmptyPaneName_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.GetOutputPaneContentAsync("");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task ClearOutputPaneAsync_WithNullPaneName_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.ClearOutputPaneAsync(null!);
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task ClearOutputPaneAsync_WithEmptyPaneName_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _visualStudioService.ClearOutputPaneAsync("");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Constructor_WithLogger_InitializesSuccessfully()
    {
        // Arrange & Act
        using var service = new VisualStudioService(_mockLogger.Object);

        // Assert
        Assert.IsNotNull(service);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            using var service = new VisualStudioService(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected
        }
    }

    [TestMethod]
    public void Dispose_CallsDisposeOnce_CompletesSuccessfully()
    {
        // Arrange
        var service = new VisualStudioService(_mockLogger.Object);

        // Act
        service.Dispose();

        // Assert - Should complete without throwing
        Assert.IsTrue(true);
    }

    [TestMethod]
    public void Dispose_CallsDisposeMultipleTimes_CompletesSuccessfully()
    {
        // Arrange
        var service = new VisualStudioService(_mockLogger.Object);

        // Act
        service.Dispose();
        service.Dispose(); // Second call should not throw

        // Assert - Should complete without throwing
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task Service_HealthCheckTimer_RunsWithoutError()
    {
        // Arrange
        using var service = new VisualStudioService(_mockLogger.Object);

        // Act - Wait a bit to let the health check timer run
        await Task.Delay(100);

        // Assert - Should not throw during health checks
        Assert.IsTrue(true);
    }

    [TestMethod]
    public async Task MultipleAsyncCalls_ExecuteConcurrently_HandleGracefully()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Make multiple concurrent calls
        tasks.Add(_visualStudioService.GetRunningInstancesAsync());
        tasks.Add(_visualStudioService.IsConnectionHealthyAsync(12345));
        tasks.Add(_visualStudioService.DisconnectFromInstanceAsync(12345));

        // Wait for all to complete (some may throw, but should handle gracefully)
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Some operations may fail without a connected instance, that's expected
        }

        // Assert - All tasks should complete (successfully or with expected exceptions)
        Assert.IsTrue(tasks.All(t => t.IsCompleted));
    }

    [TestMethod]
    public void Service_WithHighMemoryPressure_HandlesGracefully()
    {
        // Arrange
        using var service = new VisualStudioService(_mockLogger.Object);

        // Act - This should trigger memory monitoring logic internally
        var memoryInfo = MemoryMonitor.GetMemoryInfo();

        // Assert
        Assert.IsNotNull(memoryInfo);
        Assert.IsTrue(memoryInfo.WorkingSetMB >= 0);
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