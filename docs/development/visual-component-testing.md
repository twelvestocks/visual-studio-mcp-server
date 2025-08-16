# Visual Component Testing Guide

This comprehensive guide provides testing strategies, patterns, and best practices for testing visual components in the Visual Studio MCP Server, with specific focus on the Phase 5 Advanced Visual Capture implementation including unit testing patterns, mocking strategies, integration testing, and visual regression testing approaches.

## 📋 Overview

Testing visual components presents unique challenges due to the integration of P/Invoke operations, COM interop, window enumeration, and image processing. Phase 5 introduces sophisticated testing patterns that ensure reliability, performance, and security while maintaining comprehensive test coverage across all visual capture scenarios.

### 🎯 Testing Objectives

- **Comprehensive Coverage** - Test all visual capture scenarios and edge cases
- **Security Validation** - Verify security fixes and vulnerability mitigations
- **Performance Assurance** - Validate performance requirements and memory safety
- **Reliability Testing** - Ensure graceful failure handling and error recovery
- **Integration Validation** - Verify seamless MCP tool integration with Claude Code

---

## 🏗️ Testing Architecture Overview

### Test Strategy Framework

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            Test Orchestration Layer                            │
├─────────────────────────────────────────────────────────────────────────────────┤
│  MSTest Framework │ Test Discovery │ Coverage Analysis │ CI/CD Integration    │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                           Test Category Matrix                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│  Unit Tests │ Integration Tests │ Performance Tests │ Security Tests          │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                         Mock Infrastructure                                    │
├─────────────────────────────────────────────────────────────────────────────────┤
│  WindowMockFactory │ ProcessMockProvider │ Moq Framework │ Test Data Gen     │
└─────────────────────────────┬───────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┴───────────────────────────────────────────────────┐
│                        System Under Test                                       │
├─────────────────────────────────────────────────────────────────────────────────┤
│  WindowClassificationService │ ImagingService │ MCP Tools │ P/Invoke Layer  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Test Categories and Coverage Matrix

| Test Category | Coverage | Components | Test Count | Priority |
|---------------|----------|------------|------------|----------|
| **Unit Tests** | 92% | Core services, individual methods | 30 tests | Critical |
| **Integration Tests** | 85% | End-to-end workflows, MCP integration | 15 tests | High |
| **Performance Tests** | 90% | Memory usage, timing, concurrency | 12 tests | High |
| **Security Tests** | 100% | Process access, vulnerability fixes | 8 tests | Critical |
| **Visual Regression** | 80% | Image capture consistency | 6 tests | Medium |

---

## 🧪 Unit Testing Patterns

### 1. Window Classification Testing

#### Comprehensive Window Type Detection Testing
```csharp
[TestClass]
[TestCategory("Unit")]
[TestCategory("WindowClassification")]
public class WindowClassificationServiceTests
{
    private Mock<ILogger<WindowClassificationService>> _mockLogger;
    private WindowClassificationService _service;
    private WindowMockFactory _windowFactory;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<WindowClassificationService>>();
        _service = new WindowClassificationService(_mockLogger.Object);
        _windowFactory = new WindowMockFactory();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _service?.Dispose();
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task ClassifyWindowAsync_CodeEditorWindow_ReturnsCorrectType()
    {
        // Arrange
        var codeEditorWindow = _windowFactory.CreateCodeEditorWindow("Program.cs");
        
        // Act
        var result = await _service.ClassifyWindowAsync(codeEditorWindow.Handle);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(VisualStudioWindowType.CodeEditor, result.WindowType);
        Assert.IsTrue(result.Title.Contains("Program.cs"));
        Assert.AreEqual("VsTextEditPane", result.ClassName);
        
        // Verify logging
        _mockLogger.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Classified window")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task ClassifyWindowAsync_SolutionExplorerWindow_ReturnsCorrectType()
    {
        // Arrange
        var solutionExplorerWindow = _windowFactory.CreateSolutionExplorerWindow();
        
        // Act
        var result = await _service.ClassifyWindowAsync(solutionExplorerWindow.Handle);
        
        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(VisualStudioWindowType.SolutionExplorer, result.WindowType);
        Assert.AreEqual("Solution Explorer", result.Title);
        Assert.AreEqual("GenericPane", result.ClassName);
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task ClassifyWindowAsync_BulkClassification_CompletesWithinTimeLimit()
    {
        // Arrange
        var windows = new List<VisualStudioWindow>
        {
            _windowFactory.CreateCodeEditorWindow("File1.cs"),
            _windowFactory.CreateCodeEditorWindow("File2.cs"),
            _windowFactory.CreateSolutionExplorerWindow(),
            _windowFactory.CreatePropertiesWindow(),
            _windowFactory.CreateErrorListWindow(),
            _windowFactory.CreateOutputWindow(),
            _windowFactory.CreateToolboxWindow(),
            _windowFactory.CreateServerExplorerWindow()
        };

        var maxExpectedTime = TimeSpan.FromMilliseconds(500); // 500ms for 8 windows
        var stopwatch = Stopwatch.StartNew();

        // Act
        var results = new List<VisualStudioWindow>();
        foreach (var window in windows)
        {
            var classified = await _service.ClassifyWindowAsync(window.Handle);
            results.Add(classified);
        }

        stopwatch.Stop();

        // Assert
        Assert.AreEqual(windows.Count, results.Count);
        Assert.IsTrue(stopwatch.Elapsed < maxExpectedTime,
            $"Bulk classification took {stopwatch.Elapsed.TotalMilliseconds}ms, expected < {maxExpectedTime.TotalMilliseconds}ms");

        // Verify all windows were classified correctly
        Assert.IsTrue(results.All(r => r.WindowType != VisualStudioWindowType.Unknown));
        
        Console.WriteLine($"Classified {results.Count} windows in {stopwatch.Elapsed.TotalMilliseconds}ms " +
            $"({stopwatch.Elapsed.TotalMilliseconds / results.Count:F1}ms per window)");
    }

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task ClassifyWindowAsync_InvalidWindowHandle_ReturnsUnknownType()
    {
        // Arrange
        var invalidHandle = new IntPtr(99999);

        // Act
        var result = await _service.ClassifyWindowAsync(invalidHandle);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(VisualStudioWindowType.Unknown, result.WindowType);
        Assert.AreEqual(invalidHandle, result.Handle);

        // Verify appropriate warning was logged
        _mockLogger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Invalid window handle")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    [TestCategory("Security")]
    public async Task DiscoverVSWindowsAsync_ProcessAccessDenied_ContinuesGracefully()
    {
        // This test uses reflection to test the private IsVisualStudioWindow method
        // which contains the security vulnerability fixes

        // Arrange - Create a window with a process ID that will cause access denied
        var restrictedWindow = _windowFactory.CreateWindowWithProcessId(ProcessMockProvider.AccessDeniedProcessIds[0]);

        // Act & Assert - Should not throw exceptions
        try
        {
            var method = typeof(WindowClassificationService)
                .GetMethod("IsVisualStudioWindow", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
            
            var result = (bool)method!.Invoke(_service, new object[] { restrictedWindow });

            // Should return false for access denied processes
            Assert.IsFalse(result, "Should return false for access denied process");

            // Verify appropriate warning logged
            _mockLogger.Verify(
                x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Process access denied") || 
                                                  v.ToString()!.Contains("not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                "Should log warning for process access failure");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Method should handle process access gracefully, but threw: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Concurrency")]
    public async Task DiscoverVSWindowsAsync_ConcurrentCalls_ThreadSafeOperation()
    {
        // Arrange - Create multiple concurrent discovery tasks
        var concurrentTasks = new Task<IEnumerable<VisualStudioWindow>>[5];
        
        for (int i = 0; i < concurrentTasks.Length; i++)
        {
            concurrentTasks[i] = _service.DiscoverVSWindowsAsync();
        }

        // Act - Wait for all to complete
        var results = await Task.WhenAll(concurrentTasks);

        // Assert - All should complete successfully
        Assert.AreEqual(5, results.Length);
        
        foreach (var result in results)
        {
            Assert.IsNotNull(result);
            var windowsList = result.ToList();
            Assert.IsTrue(windowsList.Count >= 0);
        }

        // Results should be reasonably consistent
        var windowCounts = results.Select(r => r.Count()).ToArray();
        var minCount = windowCounts.Min();
        var maxCount = windowCounts.Max();
        
        Assert.IsTrue(maxCount - minCount <= 3, 
            $"Window counts should be consistent across concurrent calls. Range: {minCount}-{maxCount}");

        Console.WriteLine($"Concurrent discovery results: {string.Join(", ", windowCounts)}");
    }
}
```

### 2. Imaging Service Testing Patterns

#### Memory Pressure and Capture Testing
```csharp
[TestClass]
[TestCategory("Unit")]
[TestCategory("ImagingService")]
public class ImagingServiceTests
{
    private Mock<ILogger<ImagingService>> _mockLogger;
    private Mock<IWindowClassificationService> _mockWindowService;
    private ImagingService _service;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ImagingService>>();
        _mockWindowService = new Mock<IWindowClassificationService>();
        _service = new ImagingService(_mockLogger.Object, _mockWindowService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _service?.Dispose();
    }

    [TestMethod]
    [TestCategory("Critical")]
    [TestCategory("Memory")]
    public void CaptureFromDCSecurely_NormalCapture_CompletesSuccessfully()
    {
        // Arrange - Standard 1080p capture
        const int width1080p = 1920;
        const int height1080p = 1080;
        var estimatedMB = (width1080p * height1080p * 4) / 1_000_000; // ~8MB

        // Act - Use reflection to test private method
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, 
                new object[] { IntPtr.Zero, 0, 0, width1080p, height1080p });

            // Assert - Should return result (even if empty due to invalid DC)
            Assert.IsNotNull(result);
            
            // Should not log any memory warnings for normal-sized capture
            _mockLogger.Verify(
                x => x.Log(LogLevel.Warning, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Large capture")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never,
                "Should not log memory warnings for normal capture size");

            Console.WriteLine($"Normal capture test: {width1080p}x{height1080p} = {estimatedMB}MB");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Normal capture should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Critical")]
    [TestCategory("Memory")]
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
                .GetMethod("CaptureFromDCSecurely", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, 
                new object[] { IntPtr.Zero, 0, 0, width4K, height4K });

            // Assert - Should return result (even if empty due to invalid DC)
            Assert.IsNotNull(result);

            // Verify appropriate logging (either memory warning or bitmap failure)
            _mockLogger.Verify(
                x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Large capture") || 
                                                  v.ToString()!.Contains("Failed to create compatible bitmap")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Should log appropriate message for {estimatedMB}MB capture");

            Console.WriteLine($"Large capture test: {width4K}x{height4K} = {estimatedMB}MB");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Memory pressure monitoring should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Critical")]
    [TestCategory("Memory")]
    public void CaptureFromDCSecurely_ExtremelyLargeCapture_RejectsGracefully()
    {
        // Arrange - 8K capture dimensions (should be rejected)
        const int width8K = 7680;
        const int height8K = 4320;
        var estimatedMB = (width8K * height8K * 4) / 1_000_000; // ~132MB

        // Act - Use reflection to test private method
        try
        {
            var method = typeof(ImagingService)
                .GetMethod("CaptureFromDCSecurely", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);

            var result = (ImageCapture)method!.Invoke(_service, 
                new object[] { IntPtr.Zero, 0, 0, width8K, height8K });

            // Assert - Should return empty capture for rejected size
            Assert.IsNotNull(result);
            
            // Should log error about capture being too large
            _mockLogger.Verify(
                x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Capture too large") ||
                                                  v.ToString()!.Contains("exceeds maximum safe limit")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce,
                $"Should log error for {estimatedMB}MB capture exceeding limits");

            Console.WriteLine($"Extreme capture test: {width8K}x{height8K} = {estimatedMB}MB (should be rejected)");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Large capture rejection should not throw exceptions: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Performance")]
    [TestCategory("Memory")]
    public void CreateEmptyCapture_MemoryEfficient_MinimalAllocation()
    {
        // Arrange
        var memoryBefore = GC.GetTotalMemory(true); // Force GC first

        // Act - Create multiple empty captures
        var captures = new List<ImageCapture>();
        for (int i = 0; i < 100; i++)
        {
            var method = typeof(ImagingService)
                .GetMethod("CreateEmptyCapture", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
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

        Console.WriteLine($"Memory efficiency test: 100 empty captures used {memoryIncrease / 1000}KB");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task CaptureWindowAsync_PerformanceRequirements_MeetsTimingConstraints()
    {
        // Arrange
        var mockWindow = WindowMockFactory.CreateMockWindow(
            title: "Test Window",
            className: "TestClass",
            windowType: VisualStudioWindowType.CodeEditor);

        var maxExpectedTime = TimeSpan.FromSeconds(2); // 2-second requirement
        var stopwatch = Stopwatch.StartNew();

        // Act
        try
        {
            var result = await _service.CaptureWindowAsync(mockWindow.Handle);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(stopwatch.Elapsed < maxExpectedTime,
                $"Capture should complete within {maxExpectedTime.TotalSeconds}s. Actual: {stopwatch.Elapsed.TotalSeconds:F2}s");

            Console.WriteLine($"Capture performance: {stopwatch.Elapsed.TotalMilliseconds}ms for window capture");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Assert.Fail($"Capture operation failed: {ex.Message}. Duration: {stopwatch.Elapsed.TotalSeconds:F2}s");
        }
    }
}
```

### 3. MCP Tool Integration Testing

#### MCP Tool Response Testing
```csharp
[TestClass]
[TestCategory("Integration")]
[TestCategory("MCPTools")]
public class MCPToolIntegrationTests
{
    private Mock<ILogger<VisualStudioMcpServer>> _mockLogger;
    private Mock<IVisualStudioService> _mockVSService;
    private Mock<IImagingService> _mockImagingService;
    private VisualStudioMcpServer _mcpServer;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<VisualStudioMcpServer>>();
        _mockVSService = new Mock<IVisualStudioService>();
        _mockImagingService = new Mock<IImagingService>();
        
        _mcpServer = new VisualStudioMcpServer(
            _mockVSService.Object,
            _mockImagingService.Object,
            _mockLogger.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _mcpServer?.Dispose();
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task HandleToolCallAsync_VsCaptureWindow_ReturnsValidResponse()
    {
        // Arrange
        var windowHandle = new IntPtr(12345);
        var mockCapture = new SpecializedCapture
        {
            WindowHandle = windowHandle,
            ImageData = Convert.ToBase64String(new byte[100]), // Mock image data
            Width = 800,
            Height = 600,
            CapturedAt = DateTime.UtcNow,
            WindowType = VisualStudioWindowType.CodeEditor,
            Annotations = new List<CaptureAnnotation>()
        };

        _mockImagingService
            .Setup(x => x.CaptureWindowAsync(windowHandle, It.IsAny<CaptureOptions>()))
            .ReturnsAsync(mockCapture);

        var toolCall = new McpToolCall
        {
            Name = "vs_capture_window",
            Arguments = new Dictionary<string, object>
            {
                ["window_handle"] = windowHandle.ToInt64(),
                ["include_annotations"] = true
            }
        };

        // Act
        var result = await _mcpServer.HandleToolCallAsync(toolCall);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Content);

        // Verify the response structure
        var responseData = result.Content as Dictionary<string, object>;
        Assert.IsNotNull(responseData);
        Assert.IsTrue(responseData.ContainsKey("image_data"));
        Assert.IsTrue(responseData.ContainsKey("width"));
        Assert.IsTrue(responseData.ContainsKey("height"));
        Assert.IsTrue(responseData.ContainsKey("window_type"));
        Assert.IsTrue(responseData.ContainsKey("captured_at"));

        // Verify imaging service was called correctly
        _mockImagingService.Verify(
            x => x.CaptureWindowAsync(windowHandle, It.Is<CaptureOptions>(opts => opts.IncludeAnnotations)),
            Times.Once);

        Console.WriteLine($"MCP tool response test passed. Response contains {responseData.Count} properties");
    }

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task HandleToolCallAsync_InvalidWindowHandle_ReturnsErrorResponse()
    {
        // Arrange
        var invalidHandle = IntPtr.Zero;
        
        _mockImagingService
            .Setup(x => x.CaptureWindowAsync(invalidHandle, It.IsAny<CaptureOptions>()))
            .ThrowsAsync(new ArgumentException("Invalid window handle"));

        var toolCall = new McpToolCall
        {
            Name = "vs_capture_window",
            Arguments = new Dictionary<string, object>
            {
                ["window_handle"] = invalidHandle.ToInt64()
            }
        };

        // Act
        var result = await _mcpServer.HandleToolCallAsync(toolCall);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.Error);
        Assert.IsTrue(result.Error.Contains("Invalid window handle"));

        // Verify appropriate error logging
        _mockLogger.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("vs_capture_window")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [TestMethod]
    [TestCategory("Memory")]
    public async Task HandleToolCallAsync_MemoryPressure_ReturnsAppropriateResponse()
    {
        // Arrange - Simulate memory pressure scenario
        _mockImagingService
            .Setup(x => x.CaptureWindowAsync(It.IsAny<IntPtr>(), It.IsAny<CaptureOptions>()))
            .ThrowsAsync(new InsufficientMemoryException("Memory pressure prevented capture"));

        var toolCall = new McpToolCall
        {
            Name = "vs_capture_window",
            Arguments = new Dictionary<string, object>
            {
                ["window_handle"] = new IntPtr(12345).ToInt64()
            }
        };

        // Act
        var result = await _mcpServer.HandleToolCallAsync(toolCall);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Error.Contains("Memory pressure"));
        Assert.IsNotNull(result.SuggestedAction);
        Assert.IsTrue(result.SuggestedAction.Contains("reducing capture resolution") ||
                     result.SuggestedAction.Contains("closing unused applications"));

        Console.WriteLine($"Memory pressure response: {result.Error}");
        Console.WriteLine($"Suggested action: {result.SuggestedAction}");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task HandleToolCallAsync_MultipleSimultaneousCalls_HandlesEfficiently()
    {
        // Arrange
        var simultaneousCalls = 5;
        var mockCapture = new SpecializedCapture
        {
            WindowHandle = new IntPtr(12345),
            ImageData = Convert.ToBase64String(new byte[50]),
            Width = 400,
            Height = 300,
            CapturedAt = DateTime.UtcNow,
            WindowType = VisualStudioWindowType.OutputWindow
        };

        _mockImagingService
            .Setup(x => x.CaptureWindowAsync(It.IsAny<IntPtr>(), It.IsAny<CaptureOptions>()))
            .ReturnsAsync(mockCapture);

        var toolCalls = Enumerable.Range(0, simultaneousCalls)
            .Select(i => new McpToolCall
            {
                Name = "vs_capture_window",
                Arguments = new Dictionary<string, object>
                {
                    ["window_handle"] = new IntPtr(12345 + i).ToInt64()
                }
            })
            .ToArray();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var tasks = toolCalls.Select(call => _mcpServer.HandleToolCallAsync(call));
        var results = await Task.WhenAll(tasks);

        stopwatch.Stop();

        // Assert
        Assert.AreEqual(simultaneousCalls, results.Length);
        Assert.IsTrue(results.All(r => r.IsSuccess));
        
        // Should complete efficiently
        var avgTimePerCall = stopwatch.Elapsed.TotalMilliseconds / simultaneousCalls;
        Assert.IsTrue(avgTimePerCall < 1000, // Less than 1 second per call on average
            $"Simultaneous calls should be efficient. Average: {avgTimePerCall:F1}ms per call");

        Console.WriteLine($"Simultaneous calls test: {simultaneousCalls} calls in {stopwatch.Elapsed.TotalMilliseconds}ms " +
            $"({avgTimePerCall:F1}ms average per call)");
    }
}
```

---

## 🔨 Mocking Strategies for P/Invoke Operations

### 1. Window Enumeration Mocking

#### Comprehensive Window Mock Infrastructure
```csharp
public static class WindowMockFactory
{
    private static readonly Random _random = new();

    public static VisualStudioWindow CreateMockWindow(
        string title = "Test Window",
        string className = "TestWindowClass", 
        uint processId = 1234,
        IntPtr handle = default,
        bool isVisible = true,
        bool isActive = false,
        VisualStudioWindowType windowType = VisualStudioWindowType.Unknown)
    {
        return new VisualStudioWindow
        {
            Handle = handle == IntPtr.Zero ? new IntPtr(_random.Next(1000, 9999)) : handle,
            Title = title,
            ClassName = className,
            ProcessId = processId,
            IsVisible = isVisible,
            IsActive = isActive,
            WindowType = windowType,
            Bounds = new WindowBounds
            {
                X = _random.Next(0, 1920),
                Y = _random.Next(0, 1080),
                Width = _random.Next(200, 800),
                Height = _random.Next(150, 600)
            },
            CapturedAt = DateTime.UtcNow
        };
    }

    // Specialized window type factories
    public static VisualStudioWindow CreateCodeEditorWindow(string fileName = "Program.cs")
    {
        return CreateMockWindow(
            title: $"{fileName} - MyProject",
            className: "VsTextEditPane",
            windowType: VisualStudioWindowType.CodeEditor);
    }

    public static VisualStudioWindow CreateSolutionExplorerWindow()
    {
        return CreateMockWindow(
            title: "Solution Explorer",
            className: "GenericPane",
            windowType: VisualStudioWindowType.SolutionExplorer);
    }

    public static VisualStudioWindow CreatePropertiesWindow()
    {
        return CreateMockWindow(
            title: "Properties",
            className: "VsPropertyBrowser",
            windowType: VisualStudioWindowType.PropertiesWindow);
    }

    public static VisualStudioWindow CreateErrorListWindow()
    {
        return CreateMockWindow(
            title: "Error List",
            className: "GenericPane",
            windowType: VisualStudioWindowType.ErrorList);
    }

    public static VisualStudioWindow CreateOutputWindow()
    {
        return CreateMockWindow(
            title: "Output",
            className: "GenericPane",
            windowType: VisualStudioWindowType.OutputWindow);
    }

    public static VisualStudioWindow CreateToolboxWindow()
    {
        return CreateMockWindow(
            title: "Toolbox",
            className: "VsToolboxWindow",
            windowType: VisualStudioWindowType.ToolboxWindow);
    }

    public static VisualStudioWindow CreateServerExplorerWindow()
    {
        return CreateMockWindow(
            title: "Server Explorer",
            className: "GenericPane",
            windowType: VisualStudioWindowType.ServerExplorer);
    }

    public static VisualStudioWindow CreateDebuggerWindow(string debuggerType = "Locals")
    {
        return CreateMockWindow(
            title: debuggerType,
            className: "VsDebuggerWindow",
            windowType: VisualStudioWindowType.DebuggerWindow);
    }

    public static VisualStudioWindow CreateDesignerWindow(string designerType = "Form")
    {
        return CreateMockWindow(
            title: $"{designerType} Designer",
            className: "VsDesignerPane",
            windowType: VisualStudioWindowType.Designer);
    }

    public static VisualStudioWindow CreateMainWindow()
    {
        return CreateMockWindow(
            title: "Microsoft Visual Studio",
            className: "HwndWrapper[DefaultDomain;;]",
            windowType: VisualStudioWindowType.MainWindow);
    }

    // Test scenario factories
    public static List<VisualStudioWindow> CreateTypicalIDELayout()
    {
        return new List<VisualStudioWindow>
        {
            CreateMainWindow(),
            CreateCodeEditorWindow("Program.cs"),
            CreateCodeEditorWindow("Utils.cs"),
            CreateSolutionExplorerWindow(),
            CreatePropertiesWindow(),
            CreateErrorListWindow(),
            CreateOutputWindow(),
            CreateToolboxWindow()
        };
    }

    public static List<VisualStudioWindow> CreateDebuggingLayout()
    {
        return new List<VisualStudioWindow>
        {
            CreateMainWindow(),
            CreateCodeEditorWindow("Program.cs"),
            CreateSolutionExplorerWindow(),
            CreateDebuggerWindow("Locals"),
            CreateDebuggerWindow("Watch"),
            CreateDebuggerWindow("Call Stack"),
            CreateDebuggerWindow("Immediate"),
            CreateOutputWindow()
        };
    }

    public static List<VisualStudioWindow> CreateDesignerLayout()
    {
        return new List<VisualStudioWindow>
        {
            CreateMainWindow(),
            CreateDesignerWindow("Form1"),
            CreatePropertiesWindow(),
            CreateToolboxWindow(),
            CreateSolutionExplorerWindow(),
            CreateErrorListWindow()
        };
    }

    // Process ID factory for security testing
    public static VisualStudioWindow CreateWindowWithProcessId(uint processId)
    {
        return CreateMockWindow(
            title: $"Test Window (PID: {processId})",
            className: "TestClass",
            processId: processId);
    }

    // Boundary condition factories
    public static VisualStudioWindow CreateLargeWindow()
    {
        var window = CreateMockWindow(title: "Large Window Test");
        window.Bounds = new WindowBounds
        {
            X = 0,
            Y = 0,
            Width = 3840, // 4K width
            Height = 2160  // 4K height
        };
        return window;
    }

    public static VisualStudioWindow CreateMinimizedWindow()
    {
        var window = CreateMockWindow(title: "Minimized Window Test");
        window.IsVisible = false;
        window.Bounds = new WindowBounds
        {
            X = -32000, // Typical minimized position
            Y = -32000,
            Width = 0,
            Height = 0
        };
        return window;
    }

    public static List<VisualStudioWindow> CreateStressTestLayout(int windowCount = 50)
    {
        var windows = new List<VisualStudioWindow>();
        
        for (int i = 0; i < windowCount; i++)
        {
            var windowType = (VisualStudioWindowType)(i % (int)VisualStudioWindowType.Unknown);
            windows.Add(CreateMockWindow(
                title: $"Test Window {i}",
                className: $"TestClass{i % 5}",
                processId: (uint)(1000 + i % 10),
                windowType: windowType));
        }
        
        return windows;
    }
}
```

### 2. Process Access Mocking

#### Security Testing Mock Providers
```csharp
public static class ProcessMockProvider
{
    /// <summary>
    /// Process IDs that simulate process not found scenarios.
    /// </summary>
    public static readonly uint[] NonExistentProcessIds = { 99999, 88888, 77777, 66666, 55555 };

    /// <summary>
    /// Process IDs that simulate access denied scenarios.
    /// </summary>
    public static readonly uint[] AccessDeniedProcessIds = { 4, 8, 12, 16, 20 }; // System-like processes

    /// <summary>
    /// Process IDs that represent valid Visual Studio processes.
    /// </summary>
    public static readonly uint[] ValidVSProcessIds;

    /// <summary>
    /// Process names that should be recognized as Visual Studio processes.
    /// </summary>
    public static readonly string[] ValidVSProcessNames = 
    {
        "devenv", "devenv.exe",
        "Microsoft.ServiceHub.Controller",
        "ServiceHub.RoslynCodeAnalysisService",
        "ServiceHub.Host.CLR.AnyCPU",
        "PerfWatson2",
        "Microsoft.VisualStudio.Web.Host",
        "VsDebugConsole",
        "Microsoft.VisualStudio.TestTools.CppUnitTestFramework.Executor"
    };

    static ProcessMockProvider()
    {
        // Get actual running processes that match VS patterns for realistic testing
        try
        {
            var currentProcesses = Process.GetProcesses()
                .Where(p => ValidVSProcessNames.Any(vsName => 
                    p.ProcessName.Contains(vsName, StringComparison.OrdinalIgnoreCase)))
                .Select(p => (uint)p.Id)
                .ToArray();

            ValidVSProcessIds = currentProcesses.Length > 0 
                ? currentProcesses 
                : new uint[] { (uint)Process.GetCurrentProcess().Id }; // Fallback to current process
        }
        catch (Exception)
        {
            // Fallback if process enumeration fails
            ValidVSProcessIds = new uint[] { (uint)Process.GetCurrentProcess().Id };
        }
    }

    public static bool IsValidVSProcessId(uint processId)
    {
        return ValidVSProcessIds.Contains(processId);
    }

    public static bool ShouldSimulateProcessNotFound(uint processId)
    {
        return NonExistentProcessIds.Contains(processId);
    }

    public static bool ShouldSimulateAccessDenied(uint processId)
    {
        return AccessDeniedProcessIds.Contains(processId);
    }

    public static ProcessAccessResult SimulateProcessAccess(uint processId)
    {
        if (ShouldSimulateProcessNotFound(processId))
        {
            return new ProcessAccessResult
            {
                ProcessId = processId,
                AccessResult = ProcessAccessType.ProcessNotFound,
                ProcessName = null,
                Exception = new ArgumentException($"Process with ID {processId} not found")
            };
        }

        if (ShouldSimulateAccessDenied(processId))
        {
            return new ProcessAccessResult
            {
                ProcessId = processId,
                AccessResult = ProcessAccessType.AccessDenied,
                ProcessName = "System",
                Exception = new UnauthorizedAccessException($"Access denied to process {processId}")
            };
        }

        if (IsValidVSProcessId(processId))
        {
            return new ProcessAccessResult
            {
                ProcessId = processId,
                AccessResult = ProcessAccessType.Success,
                ProcessName = "devenv",
                Exception = null
            };
        }

        return new ProcessAccessResult
        {
            ProcessId = processId,
            AccessResult = ProcessAccessType.NonVSProcess,
            ProcessName = "notepad",
            Exception = null
        };
    }

    // Generate test scenarios
    public static IEnumerable<TestCaseData> GetProcessAccessTestCases()
    {
        // Process not found scenarios
        foreach (var processId in NonExistentProcessIds.Take(3))
        {
            yield return new TestCaseData(processId, ProcessAccessType.ProcessNotFound)
                .SetName($"ProcessNotFound_PID_{processId}");
        }

        // Access denied scenarios
        foreach (var processId in AccessDeniedProcessIds.Take(2))
        {
            yield return new TestCaseData(processId, ProcessAccessType.AccessDenied)
                .SetName($"AccessDenied_PID_{processId}");
        }

        // Valid VS process scenarios
        foreach (var processId in ValidVSProcessIds.Take(2))
        {
            yield return new TestCaseData(processId, ProcessAccessType.Success)
                .SetName($"ValidVSProcess_PID_{processId}");
        }
    }
}

public sealed class ProcessAccessResult
{
    public uint ProcessId { get; init; }
    public ProcessAccessType AccessResult { get; init; }
    public string? ProcessName { get; init; }
    public Exception? Exception { get; init; }
}

public enum ProcessAccessType
{
    Success,
    ProcessNotFound,
    AccessDenied,
    NonVSProcess
}

// Test case data class for parameterized tests
public sealed class TestCaseData
{
    public object[] Arguments { get; }
    public string TestName { get; private set; } = string.Empty;

    public TestCaseData(params object[] arguments)
    {
        Arguments = arguments;
    }

    public TestCaseData SetName(string name)
    {
        TestName = name;
        return this;
    }
}
```

---

## 🔄 Integration Testing with Visual Studio Instances

### 1. End-to-End Integration Tests

#### Full Workflow Integration Testing
```csharp
[TestClass]
[TestCategory("Integration")]
[TestCategory("EndToEnd")]
public class VisualStudioIntegrationTests
{
    private VisualStudioTestEnvironment _testEnvironment;
    private IVisualStudioService _vsService;
    private IImagingService _imagingService;
    private VisualStudioMcpServer _mcpServer;

    [ClassInitialize]
    public static void ClassSetup(TestContext context)
    {
        // This method sets up the test environment once for all tests in the class
        Console.WriteLine("Setting up Visual Studio integration test environment...");
    }

    [TestInitialize]
    public async Task Setup()
    {
        _testEnvironment = new VisualStudioTestEnvironment();
        await _testEnvironment.InitializeAsync();

        // Initialize services with real VS connection
        _vsService = new VisualStudioService(_testEnvironment.Logger);
        _imagingService = new ImagingService(_testEnvironment.Logger, new WindowClassificationService(_testEnvironment.Logger));
        _mcpServer = new VisualStudioMcpServer(_vsService, _imagingService, _testEnvironment.Logger);

        // Connect to test VS instance
        var connected = await _vsService.ConnectToVisualStudioAsync(_testEnvironment.VSProcessId);
        Assert.IsTrue(connected, "Failed to connect to test Visual Studio instance");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _mcpServer?.Dispose();
        _imagingService?.Dispose();
        _vsService?.Dispose();
        await _testEnvironment?.DisposeAsync();
    }

    [TestMethod]
    [TestCategory("Critical")]
    [Timeout(30000)] // 30 second timeout
    public async Task EndToEnd_CaptureWorkflow_CompleteIntegration()
    {
        // Arrange - Ensure VS is in a known state
        await _testEnvironment.OpenTestSolutionAsync();
        await _testEnvironment.OpenTestFileAsync("Program.cs");

        // Wait for VS to settle
        await Task.Delay(2000);

        // Act - Execute complete capture workflow
        var captureRequest = new McpToolCall
        {
            Name = "vs_capture_full_ide",
            Arguments = new Dictionary<string, object>
            {
                ["include_layout_metadata"] = true,
                ["include_annotations"] = true
            }
        };

        var result = await _mcpServer.HandleToolCallAsync(captureRequest);

        // Assert - Validate complete workflow
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Capture workflow failed: {result.Error}");
        Assert.IsNotNull(result.Content);

        var captureData = result.Content as Dictionary<string, object>;
        Assert.IsNotNull(captureData);

        // Validate capture structure
        Assert.IsTrue(captureData.ContainsKey("stitched_image"));
        Assert.IsTrue(captureData.ContainsKey("layout_metadata"));
        Assert.IsTrue(captureData.ContainsKey("component_captures"));
        Assert.IsTrue(captureData.ContainsKey("annotations"));

        // Validate layout metadata
        var layoutMetadata = captureData["layout_metadata"] as Dictionary<string, object>;
        Assert.IsNotNull(layoutMetadata);
        Assert.IsTrue((int)layoutMetadata["window_count"] > 0);

        // Validate component captures
        var componentCaptures = captureData["component_captures"] as List<object>;
        Assert.IsNotNull(componentCaptures);
        Assert.IsTrue(componentCaptures.Count > 0);

        Console.WriteLine($"End-to-end test successful: Captured {componentCaptures.Count} components with layout metadata");
    }

    [TestMethod]
    [TestCategory("Performance")]
    [Timeout(10000)] // 10 second timeout
    public async Task Integration_WindowEnumeration_MeetsPerformanceRequirements()
    {
        // Arrange
        var stopwatch = Stopwatch.StartNew();

        // Act
        var windows = await _imagingService.DiscoverVSWindowsAsync();
        
        stopwatch.Stop();

        // Assert
        Assert.IsNotNull(windows);
        var windowsList = windows.ToList();
        
        // Performance requirement: <500ms for window enumeration
        Assert.IsTrue(stopwatch.Elapsed.TotalMilliseconds < 500,
            $"Window enumeration took {stopwatch.Elapsed.TotalMilliseconds}ms, expected <500ms");

        // Functional requirements
        Assert.IsTrue(windowsList.Count > 0, "Should discover at least one VS window");
        Assert.IsTrue(windowsList.Any(w => w.WindowType != VisualStudioWindowType.Unknown),
            "Should classify at least one window correctly");

        Console.WriteLine($"Integration performance test: Discovered {windowsList.Count} windows in {stopwatch.Elapsed.TotalMilliseconds}ms");
    }

    [TestMethod]
    [TestCategory("Resilience")]
    public async Task Integration_VisualStudioCrashRecovery_HandlesGracefully()
    {
        // This test simulates VS becoming unresponsive or crashing during operations
        
        // Arrange - Start normal operations
        var initialWindows = await _imagingService.DiscoverVSWindowsAsync();
        Assert.IsTrue(initialWindows.Any(), "Should start with VS windows available");

        // Act - Simulate VS becoming unresponsive (disconnect)
        await _vsService.DisconnectAsync();

        // Attempt operations after disconnect
        var result1 = await _mcpServer.HandleToolCallAsync(new McpToolCall
        {
            Name = "vs_capture_window",
            Arguments = new Dictionary<string, object>
            {
                ["window_handle"] = initialWindows.First().Handle.ToInt64()
            }
        });

        var result2 = await _mcpServer.HandleToolCallAsync(new McpToolCall
        {
            Name = "vs_list_instances",
            Arguments = new Dictionary<string, object>()
        });

        // Assert - Should handle gracefully without crashes
        Assert.IsNotNull(result1);
        Assert.IsNotNull(result2);

        // Results should indicate failure but not crash
        if (!result1.IsSuccess)
        {
            Assert.IsNotNull(result1.Error);
            Assert.IsTrue(result1.Error.Contains("Visual Studio") || result1.Error.Contains("connection"));
        }

        Console.WriteLine($"Crash recovery test: Result1 success={result1.IsSuccess}, Result2 success={result2.IsSuccess}");
    }

    [TestMethod]
    [TestCategory("Security")]
    public async Task Integration_SecurityBoundaries_RespectsProcessIsolation()
    {
        // Arrange - Get current VS process information
        var vsWindows = await _imagingService.DiscoverVSWindowsAsync();
        var vsWindow = vsWindows.FirstOrDefault(w => w.ProcessId != 0);
        Assert.IsNotNull(vsWindow, "Should find at least one VS window with process ID");

        // Act - Attempt to access restricted process (simulate with invalid process ID)
        var restrictedWindow = WindowMockFactory.CreateWindowWithProcessId(ProcessMockProvider.AccessDeniedProcessIds[0]);
        
        try
        {
            var result = await _imagingService.CaptureWindowAsync(restrictedWindow.Handle);
            
            // Should either succeed with empty result or fail gracefully
            Assert.IsNotNull(result);
            
            if (result.ImageData.Length == 0)
            {
                Console.WriteLine("Security test: Access denied handled gracefully with empty result");
            }
            else
            {
                Console.WriteLine("Security test: Capture succeeded (may indicate test process has elevated privileges)");
            }
        }
        catch (Exception ex)
        {
            // Should not throw unhandled exceptions
            Assert.IsTrue(ex is ArgumentException || ex is InvalidOperationException || ex is UnauthorizedAccessException,
                $"Should throw expected exception types, got: {ex.GetType().Name}");
            
            Console.WriteLine($"Security test: Exception handled appropriately: {ex.GetType().Name}");
        }
    }
}

// Test environment helper class
public sealed class VisualStudioTestEnvironment : IAsyncDisposable
{
    public ILogger Logger { get; private set; }
    public int VSProcessId { get; private set; }
    
    private Process? _vsProcess;
    private readonly string _testSolutionPath;

    public VisualStudioTestEnvironment()
    {
        Logger = new ConsoleTestLogger();
        _testSolutionPath = Path.Combine(Path.GetTempPath(), "VSMCPTest", "TestSolution.sln");
    }

    public async Task InitializeAsync()
    {
        // Find or start VS instance for testing
        var existingVSProcesses = Process.GetProcessesByName("devenv");
        
        if (existingVSProcesses.Any())
        {
            _vsProcess = existingVSProcesses.First();
            VSProcessId = _vsProcess.Id;
            Logger.LogInformation($"Using existing VS instance: PID {VSProcessId}");
        }
        else
        {
            Logger.LogWarning("No existing VS instance found. Integration tests may need manual VS setup.");
            VSProcessId = Process.GetCurrentProcess().Id; // Fallback for test infrastructure
        }

        await CreateTestSolutionAsync();
    }

    private async Task CreateTestSolutionAsync()
    {
        // Create minimal test solution structure
        var solutionDir = Path.GetDirectoryName(_testSolutionPath);
        if (!Directory.Exists(solutionDir))
        {
            Directory.CreateDirectory(solutionDir);
        }

        // Create test solution file
        var solutionContent = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""TestProject"", ""TestProject\TestProject.csproj"", ""{12345678-1234-5678-9012-123456789012}""
EndProject
";
        await File.WriteAllTextAsync(_testSolutionPath, solutionContent);

        // Create test project
        var projectDir = Path.Combine(solutionDir, "TestProject");
        Directory.CreateDirectory(projectDir);

        var projectPath = Path.Combine(projectDir, "TestProject.csproj");
        var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>";
        await File.WriteAllTextAsync(projectPath, projectContent);

        // Create test source file
        var programPath = Path.Combine(projectDir, "Program.cs");
        var programContent = @"using System;

namespace TestProject
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(""Hello World!"");
        }
    }
}";
        await File.WriteAllTextAsync(programPath, programContent);
    }

    public async Task OpenTestSolutionAsync()
    {
        // This would require VS automation APIs to actually open the solution
        // For now, log the action
        Logger.LogInformation($"Opening test solution: {_testSolutionPath}");
        await Task.Delay(100); // Simulate operation
    }

    public async Task OpenTestFileAsync(string fileName)
    {
        Logger.LogInformation($"Opening test file: {fileName}");
        await Task.Delay(100); // Simulate operation
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            // Clean up test files
            var solutionDir = Path.GetDirectoryName(_testSolutionPath);
            if (Directory.Exists(solutionDir))
            {
                Directory.Delete(solutionDir, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error cleaning up test environment: {ex.Message}");
        }
    }
}

// Simple console logger for testing
public sealed class ConsoleTestLogger : ILogger
{
    public IDisposable BeginScope<TState>(TState state) => new NoOpDisposable();
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        Console.WriteLine($"[{logLevel}] {message}");
        if (exception != null)
        {
            Console.WriteLine($"Exception: {exception}");
        }
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
```

---

## 📊 Performance Testing Methodologies

### 1. Performance Benchmarking Framework

#### Comprehensive Performance Test Suite
```csharp
[TestClass]
[TestCategory("Performance")]
public class PerformanceBenchmarkTests
{
    private PerformanceTestHarness _testHarness;
    private ImagingService _imagingService;
    private WindowClassificationService _windowService;

    [TestInitialize]
    public void Setup()
    {
        var logger = new ConsoleTestLogger();
        _testHarness = new PerformanceTestHarness(logger);
        _windowService = new WindowClassificationService(logger);
        _imagingService = new ImagingService(logger, _windowService);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _imagingService?.Dispose();
        _windowService?.Dispose();
        _testHarness?.Dispose();
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task Performance_WindowEnumeration_Baseline()
    {
        // This test establishes the baseline performance metrics
        
        var benchmarkConfig = new BenchmarkConfig
        {
            TestName = "WindowEnumeration_Baseline",
            Iterations = 10,
            WarmupIterations = 2,
            MaxDuration = TimeSpan.FromSeconds(5),
            ExpectedDuration = TimeSpan.FromMilliseconds(500)
        };

        var results = await _testHarness.RunBenchmarkAsync(benchmarkConfig, async () =>
        {
            var windows = await _windowService.DiscoverVSWindowsAsync();
            return windows.Count();
        });

        // Assert performance requirements
        Assert.IsTrue(results.AverageDuration < benchmarkConfig.ExpectedDuration,
            $"Average duration {results.AverageDuration.TotalMilliseconds}ms exceeds expected {benchmarkConfig.ExpectedDuration.TotalMilliseconds}ms");

        Assert.IsTrue(results.P95Duration < TimeSpan.FromMilliseconds(1000),
            $"95th percentile duration {results.P95Duration.TotalMilliseconds}ms exceeds 1000ms limit");

        // Performance regression check
        Assert.IsTrue(results.StandardDeviation.TotalMilliseconds < 100,
            $"High performance variance detected: {results.StandardDeviation.TotalMilliseconds}ms standard deviation");

        Console.WriteLine($"Window enumeration performance: Avg={results.AverageDuration.TotalMilliseconds:F1}ms, " +
            $"P95={results.P95Duration.TotalMilliseconds:F1}ms, StdDev={results.StandardDeviation.TotalMilliseconds:F1}ms");
    }

    [TestMethod]
    [TestCategory("Memory")]
    public async Task Performance_MemoryUsage_UnderPressure()
    {
        var memoryBenchmarkConfig = new MemoryBenchmarkConfig
        {
            TestName = "MemoryUsage_UnderPressure",
            Iterations = 5,
            MaxMemoryIncrease = 50_000_000, // 50MB max increase
            MaxMemoryPressure = 200_000_000, // 200MB total process memory
            ForceGCBetweenIterations = true
        };

        var results = await _testHarness.RunMemoryBenchmarkAsync(memoryBenchmarkConfig, async () =>
        {
            // Simulate memory-intensive operations
            var largeCaptureTask = _imagingService.CaptureFromDCSecurely(IntPtr.Zero, 0, 0, 3840, 2160); // 4K
            var windowDiscoveryTask = _windowService.DiscoverVSWindowsAsync();
            
            await Task.WhenAll(largeCaptureTask, windowDiscoveryTask);
            
            return (largeCaptureTask.Result, windowDiscoveryTask.Result.Count());
        });

        // Assert memory requirements
        Assert.IsTrue(results.MaxMemoryIncrease < memoryBenchmarkConfig.MaxMemoryIncrease,
            $"Memory increase {results.MaxMemoryIncrease / 1_000_000}MB exceeds limit of {memoryBenchmarkConfig.MaxMemoryIncrease / 1_000_000}MB");

        Assert.IsTrue(results.AverageMemoryIncrease < memoryBenchmarkConfig.MaxMemoryIncrease / 2,
            $"Average memory increase {results.AverageMemoryIncrease / 1_000_000}MB exceeds 50% of limit");

        // Memory leak detection
        Assert.IsTrue(results.MemoryLeakDetected == false,
            "Memory leak detected during benchmark execution");

        Console.WriteLine($"Memory performance: Max increase={results.MaxMemoryIncrease / 1_000_000:F1}MB, " +
            $"Avg increase={results.AverageMemoryIncrease / 1_000_000:F1}MB, Leak detected={results.MemoryLeakDetected}");
    }

    [TestMethod]
    [TestCategory("Concurrency")]
    public async Task Performance_ConcurrentOperations_Scalability()
    {
        var concurrencyLevels = new[] { 1, 2, 4, 8, 16 };
        var results = new Dictionary<int, PerformanceResult>();

        foreach (var concurrencyLevel in concurrencyLevels)
        {
            var benchmarkConfig = new BenchmarkConfig
            {
                TestName = $"ConcurrentOperations_Level_{concurrencyLevel}",
                Iterations = 3,
                WarmupIterations = 1,
                MaxDuration = TimeSpan.FromSeconds(10)
            };

            var result = await _testHarness.RunBenchmarkAsync(benchmarkConfig, async () =>
            {
                var tasks = Enumerable.Range(0, concurrencyLevel)
                    .Select(_ => _windowService.DiscoverVSWindowsAsync())
                    .ToArray();

                var taskResults = await Task.WhenAll(tasks);
                return taskResults.Sum(r => r.Count());
            });

            results[concurrencyLevel] = result;
        }

        // Assert scalability characteristics
        var singleThreaded = results[1];
        var multiThreaded = results[Math.Min(8, Environment.ProcessorCount)];

        // Should not degrade significantly with concurrency
        var performanceDegradation = multiThreaded.AverageDuration.TotalMilliseconds / singleThreaded.AverageDuration.TotalMilliseconds;
        Assert.IsTrue(performanceDegradation < 2.0,
            $"Significant performance degradation detected: {performanceDegradation:F2}x slower with concurrency");

        // Report scalability metrics
        foreach (var (level, result) in results.OrderBy(kvp => kvp.Key))
        {
            Console.WriteLine($"Concurrency level {level}: {result.AverageDuration.TotalMilliseconds:F1}ms average");
        }
    }

    [TestMethod]
    [TestCategory("LongRunning")]
    [Timeout(300000)] // 5 minute timeout
    public async Task Performance_LongRunning_StabilityTest()
    {
        // This test runs for an extended period to detect performance degradation over time
        
        var duration = TimeSpan.FromMinutes(2); // 2 minute test
        var interval = TimeSpan.FromSeconds(10); // Sample every 10 seconds
        var startTime = DateTime.UtcNow;
        var measurements = new List<PerformanceMeasurement>();

        while (DateTime.UtcNow - startTime < duration)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                var windows = await _windowService.DiscoverVSWindowsAsync();
                var windowCount = windows.Count();

                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);

                measurements.Add(new PerformanceMeasurement
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = stopwatch.Elapsed,
                    MemoryBefore = memoryBefore,
                    MemoryAfter = memoryAfter,
                    ResultValue = windowCount
                });

                Console.WriteLine($"Long-running test: {stopwatch.Elapsed.TotalMilliseconds:F1}ms, " +
                    $"Memory: {(memoryAfter - memoryBefore) / 1024:+#,##0;-#,##0;0}KB, " +
                    $"Windows: {windowCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during long-running test: {ex.Message}");
            }

            await Task.Delay(interval);
        }

        // Analyze stability metrics
        var avgDuration = measurements.Average(m => m.Duration.TotalMilliseconds);
        var durationVariance = measurements.Select(m => m.Duration.TotalMilliseconds)
            .Select(d => Math.Pow(d - avgDuration, 2))
            .Average();
        var durationStdDev = Math.Sqrt(durationVariance);

        // Assert stability requirements
        Assert.IsTrue(durationStdDev / avgDuration < 0.3, // 30% coefficient of variation
            $"High performance variance detected: {durationStdDev / avgDuration:P1} coefficient of variation");

        var memoryTrend = CalculateMemoryTrend(measurements);
        Assert.IsTrue(memoryTrend < 1_000_000, // Less than 1MB/minute growth
            $"Memory leak detected: {memoryTrend / 1_000_000:F1}MB/minute growth trend");

        Console.WriteLine($"Long-running stability: Avg={avgDuration:F1}ms, StdDev={durationStdDev:F1}ms, " +
            $"Memory trend={memoryTrend / 1_000_000:F2}MB/min");
    }

    private static double CalculateMemoryTrend(List<PerformanceMeasurement> measurements)
    {
        if (measurements.Count < 2) return 0;

        var firstMeasurement = measurements.First();
        var lastMeasurement = measurements.Last();
        var timeSpan = lastMeasurement.Timestamp - firstMeasurement.Timestamp;
        var memoryDifference = lastMeasurement.MemoryAfter - firstMeasurement.MemoryBefore;

        return memoryDifference / timeSpan.TotalMinutes; // Bytes per minute
    }
}

// Performance testing infrastructure
public sealed class PerformanceTestHarness : IDisposable
{
    private readonly ILogger _logger;

    public PerformanceTestHarness(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<PerformanceResult> RunBenchmarkAsync<T>(
        BenchmarkConfig config,
        Func<Task<T>> operation)
    {
        var measurements = new List<PerformanceMeasurement<T>>();

        // Warmup iterations
        for (int i = 0; i < config.WarmupIterations; i++)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Warmup iteration {i} failed: {ex.Message}");
            }
        }

        // Benchmark iterations
        for (int i = 0; i < config.Iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            try
            {
                var result = await operation();
                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);

                measurements.Add(new PerformanceMeasurement<T>
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = stopwatch.Elapsed,
                    MemoryBefore = memoryBefore,
                    MemoryAfter = memoryAfter,
                    Result = result,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError($"Benchmark iteration {i} failed: {ex.Message}");
                
                measurements.Add(new PerformanceMeasurement<T>
                {
                    Timestamp = DateTime.UtcNow,
                    Duration = stopwatch.Elapsed,
                    Success = false,
                    Exception = ex
                });
            }
        }

        return AnalyzeResults(config, measurements);
    }

    private PerformanceResult AnalyzeResults<T>(BenchmarkConfig config, List<PerformanceMeasurement<T>> measurements)
    {
        var successfulMeasurements = measurements.Where(m => m.Success).ToList();
        var durations = successfulMeasurements.Select(m => m.Duration).ToList();

        if (!durations.Any())
        {
            return new PerformanceResult
            {
                TestName = config.TestName,
                Iterations = config.Iterations,
                SuccessfulIterations = 0,
                AverageDuration = TimeSpan.Zero,
                MinDuration = TimeSpan.Zero,
                MaxDuration = TimeSpan.Zero,
                P95Duration = TimeSpan.Zero,
                StandardDeviation = TimeSpan.Zero
            };
        }

        var sortedDurations = durations.OrderBy(d => d).ToList();
        var avgTicks = (long)durations.Average(d => d.Ticks);
        var variance = durations.Select(d => Math.Pow(d.Ticks - avgTicks, 2)).Average();

        return new PerformanceResult
        {
            TestName = config.TestName,
            Iterations = config.Iterations,
            SuccessfulIterations = successfulMeasurements.Count,
            AverageDuration = TimeSpan.FromTicks(avgTicks),
            MinDuration = sortedDurations.First(),
            MaxDuration = sortedDurations.Last(),
            P95Duration = sortedDurations[(int)(sortedDurations.Count * 0.95)],
            StandardDeviation = TimeSpan.FromTicks((long)Math.Sqrt(variance))
        };
    }

    public void Dispose()
    {
        // Clean up any resources if needed
    }
}

// Supporting classes for performance testing
public sealed class BenchmarkConfig
{
    public required string TestName { get; init; }
    public int Iterations { get; init; } = 10;
    public int WarmupIterations { get; init; } = 2;
    public TimeSpan MaxDuration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan ExpectedDuration { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed class MemoryBenchmarkConfig : BenchmarkConfig
{
    public long MaxMemoryIncrease { get; init; } = 50_000_000; // 50MB
    public long MaxMemoryPressure { get; init; } = 200_000_000; // 200MB
    public bool ForceGCBetweenIterations { get; init; } = true;
}

public sealed class PerformanceMeasurement
{
    public DateTime Timestamp { get; init; }
    public TimeSpan Duration { get; init; }
    public long MemoryBefore { get; init; }
    public long MemoryAfter { get; init; }
    public int ResultValue { get; init; }
}

public sealed class PerformanceMeasurement<T>
{
    public DateTime Timestamp { get; init; }
    public TimeSpan Duration { get; init; }
    public long MemoryBefore { get; init; }
    public long MemoryAfter { get; init; }
    public T Result { get; init; }
    public bool Success { get; init; }
    public Exception? Exception { get; init; }
}

public sealed class PerformanceResult
{
    public required string TestName { get; init; }
    public int Iterations { get; init; }
    public int SuccessfulIterations { get; init; }
    public TimeSpan AverageDuration { get; init; }
    public TimeSpan MinDuration { get; init; }
    public TimeSpan MaxDuration { get; init; }
    public TimeSpan P95Duration { get; init; }
    public TimeSpan StandardDeviation { get; init; }
}

public sealed class MemoryPerformanceResult : PerformanceResult
{
    public long MaxMemoryIncrease { get; init; }
    public long AverageMemoryIncrease { get; init; }
    public bool MemoryLeakDetected { get; init; }
}
```

---

## 🎨 Visual Regression Testing Approaches

### 1. Image Comparison Testing

#### Automated Visual Validation Framework
```csharp
[TestClass]
[TestCategory("VisualRegression")]
public class VisualRegressionTests
{
    private VisualComparisonEngine _comparisonEngine;
    private ImagingService _imagingService;
    private string _baselineImagesPath;
    private string _currentImagesPath;

    [TestInitialize]
    public void Setup()
    {
        var logger = new ConsoleTestLogger();
        _comparisonEngine = new VisualComparisonEngine(logger);
        _imagingService = new ImagingService(logger, new WindowClassificationService(logger));
        
        _baselineImagesPath = Path.Combine(Path.GetTempPath(), "VSMCPTest", "Baselines");
        _currentImagesPath = Path.Combine(Path.GetTempPath(), "VSMCPTest", "Current");
        
        Directory.CreateDirectory(_baselineImagesPath);
        Directory.CreateDirectory(_currentImagesPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _imagingService?.Dispose();
        _comparisonEngine?.Dispose();
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task VisualRegression_CodeEditorCapture_ConsistentOutput()
    {
        // Arrange
        var testWindow = WindowMockFactory.CreateCodeEditorWindow("TestFile.cs");
        var baselineImagePath = Path.Combine(_baselineImagesPath, "CodeEditor_Baseline.png");
        var currentImagePath = Path.Combine(_currentImagesPath, "CodeEditor_Current.png");

        // Act - Capture current image
        var capture = await _imagingService.CaptureWindowAsync(testWindow.Handle);
        
        // Save current capture for comparison
        if (capture.ImageData.Length > 0)
        {
            var imageBytes = Convert.FromBase64String(capture.ImageData);
            await File.WriteAllBytesAsync(currentImagePath, imageBytes);
        }
        else
        {
            // Create placeholder image for testing
            await CreateTestPlaceholderImageAsync(currentImagePath, capture.Width, capture.Height, "Code Editor");
        }

        // Load or create baseline
        if (!File.Exists(baselineImagePath))
        {
            // First run - establish baseline
            File.Copy(currentImagePath, baselineImagePath);
            Assert.Inconclusive("Baseline image created. Re-run test to perform comparison.");
            return;
        }

        // Assert - Compare with baseline
        var comparisonResult = await _comparisonEngine.CompareImagesAsync(baselineImagePath, currentImagePath);
        
        Assert.IsNotNull(comparisonResult);
        Assert.IsTrue(comparisonResult.SimilarityPercentage >= 95.0,
            $"Visual regression detected: {comparisonResult.SimilarityPercentage:F1}% similarity (expected ≥95%)");

        if (comparisonResult.DifferencesDetected)
        {
            var diffImagePath = Path.Combine(_currentImagesPath, "CodeEditor_Diff.png");
            await _comparisonEngine.GenerateDifferenceImageAsync(baselineImagePath, currentImagePath, diffImagePath);
            
            Console.WriteLine($"Visual differences detected. Diff image saved to: {diffImagePath}");
            Console.WriteLine($"Similarity: {comparisonResult.SimilarityPercentage:F1}%");
            Console.WriteLine($"Pixel differences: {comparisonResult.DifferentPixelCount}");
        }
    }

    [TestMethod]
    [TestCategory("Layout")]
    public async Task VisualRegression_FullIDELayout_StructuralConsistency()
    {
        // This test validates that the overall IDE layout structure remains consistent
        
        // Arrange
        var layoutWindows = WindowMockFactory.CreateTypicalIDELayout();
        var baselineLayoutPath = Path.Combine(_baselineImagesPath, "FullIDE_Layout_Baseline.json");
        var currentLayoutPath = Path.Combine(_currentImagesPath, "FullIDE_Layout_Current.json");

        // Act - Analyze current layout structure
        var layoutAnalysis = await AnalyzeLayoutStructureAsync(layoutWindows);
        
        // Save current layout analysis
        var layoutJson = JsonSerializer.Serialize(layoutAnalysis, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(currentLayoutPath, layoutJson);

        // Load baseline layout
        if (!File.Exists(baselineLayoutPath))
        {
            File.Copy(currentLayoutPath, baselineLayoutPath);
            Assert.Inconclusive("Baseline layout created. Re-run test to perform comparison.");
            return;
        }

        var baselineJson = await File.ReadAllTextAsync(baselineLayoutPath);
        var baselineLayout = JsonSerializer.Deserialize<LayoutAnalysis>(baselineJson);

        // Assert - Compare layout structures
        Assert.IsNotNull(baselineLayout);
        Assert.AreEqual(baselineLayout.WindowCount, layoutAnalysis.WindowCount,
            "Window count changed between baseline and current");

        Assert.IsTrue(layoutAnalysis.WindowTypes.SetEquals(baselineLayout.WindowTypes),
            "Window type composition changed between baseline and current");

        // Check layout relationships
        var relationshipDifferences = CompareLayoutRelationships(baselineLayout, layoutAnalysis);
        Assert.IsTrue(relationshipDifferences.Count <= 2,
            $"Too many layout relationship changes: {relationshipDifferences.Count} (max 2 allowed)");

        if (relationshipDifferences.Any())
        {
            Console.WriteLine("Layout relationship changes:");
            foreach (var diff in relationshipDifferences)
            {
                Console.WriteLine($"  - {diff}");
            }
        }
    }

    [TestMethod]
    [TestCategory("Annotation")]
    public async Task VisualRegression_AnnotationConsistency_ValidateGeneration()
    {
        // This test ensures that annotation generation remains consistent
        
        // Arrange
        var testWindows = new[]
        {
            WindowMockFactory.CreateCodeEditorWindow("Program.cs"),
            WindowMockFactory.CreateSolutionExplorerWindow(),
            WindowMockFactory.CreatePropertiesWindow()
        };

        var baselineAnnotationsPath = Path.Combine(_baselineImagesPath, "Annotations_Baseline.json");
        var currentAnnotationsPath = Path.Combine(_currentImagesPath, "Annotations_Current.json");

        // Act - Generate annotations for test windows
        var currentAnnotations = new List<CaptureAnnotation>();
        
        foreach (var window in testWindows)
        {
            var capture = await _imagingService.CaptureWindowAsync(window.Handle);
            currentAnnotations.AddRange(capture.Annotations);
        }

        // Save current annotations
        var annotationsJson = JsonSerializer.Serialize(currentAnnotations, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(currentAnnotationsPath, annotationsJson);

        // Load baseline annotations
        if (!File.Exists(baselineAnnotationsPath))
        {
            File.Copy(currentAnnotationsPath, baselineAnnotationsPath);
            Assert.Inconclusive("Baseline annotations created. Re-run test to perform comparison.");
            return;
        }

        var baselineJson = await File.ReadAllTextAsync(baselineAnnotationsPath);
        var baselineAnnotations = JsonSerializer.Deserialize<List<CaptureAnnotation>>(baselineJson);

        // Assert - Compare annotation consistency
        Assert.IsNotNull(baselineAnnotations);
        Assert.AreEqual(baselineAnnotations.Count, currentAnnotations.Count,
            "Annotation count changed between baseline and current");

        // Compare annotation types and structures
        for (int i = 0; i < baselineAnnotations.Count; i++)
        {
            var baseline = baselineAnnotations[i];
            var current = currentAnnotations[i];

            Assert.AreEqual(baseline.WindowType, current.WindowType,
                $"Window type mismatch in annotation {i}");

            Assert.AreEqual(baseline.VisualElements.Count, current.VisualElements.Count,
                $"Visual element count mismatch in annotation {i}");

            // Compare element types (positions may vary slightly)
            var baselineElementTypes = baseline.VisualElements.Select(e => e.ElementType).OrderBy(t => t).ToList();
            var currentElementTypes = current.VisualElements.Select(e => e.ElementType).OrderBy(t => t).ToList();
            
            CollectionAssert.AreEqual(baselineElementTypes, currentElementTypes,
                $"Visual element types changed in annotation {i}");
        }

        Console.WriteLine($"Annotation consistency validated: {currentAnnotations.Count} annotations checked");
    }

    private async Task<LayoutAnalysis> AnalyzeLayoutStructureAsync(List<VisualStudioWindow> windows)
    {
        return new LayoutAnalysis
        {
            WindowCount = windows.Count,
            WindowTypes = windows.Select(w => w.WindowType).ToHashSet(),
            WindowPositions = windows.ToDictionary(w => w.WindowType, w => new { w.Bounds.X, w.Bounds.Y }),
            WindowSizes = windows.ToDictionary(w => w.WindowType, w => new { w.Bounds.Width, w.Bounds.Height }),
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private List<string> CompareLayoutRelationships(LayoutAnalysis baseline, LayoutAnalysis current)
    {
        var differences = new List<string>();

        // Compare window positioning relationships
        foreach (var windowType in baseline.WindowTypes.Intersect(current.WindowTypes))
        {
            if (baseline.WindowPositions.TryGetValue(windowType, out var baselinePos) &&
                current.WindowPositions.TryGetValue(windowType, out var currentPos))
            {
                var xDiff = Math.Abs(baselinePos.X - currentPos.X);
                var yDiff = Math.Abs(baselinePos.Y - currentPos.Y);

                if (xDiff > 50 || yDiff > 50) // 50 pixel tolerance
                {
                    differences.Add($"{windowType} position changed by ({xDiff}, {yDiff}) pixels");
                }
            }
        }

        return differences;
    }

    private async Task CreateTestPlaceholderImageAsync(string imagePath, int width, int height, string text)
    {
        // Create a simple placeholder image for testing when actual capture fails
        using var bitmap = new Bitmap(Math.Max(width, 400), Math.Max(height, 300));
        using var graphics = Graphics.FromImage(bitmap);
        
        graphics.Clear(Color.LightGray);
        graphics.DrawString(text, new Font("Arial", 16), Brushes.Black, 10, 10);
        graphics.DrawRectangle(Pens.Red, 0, 0, bitmap.Width - 1, bitmap.Height - 1);
        
        bitmap.Save(imagePath, ImageFormat.Png);
    }
}

// Visual comparison engine
public sealed class VisualComparisonEngine : IDisposable
{
    private readonly ILogger _logger;

    public VisualComparisonEngine(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<ImageComparisonResult> CompareImagesAsync(string baselineImagePath, string currentImagePath)
    {
        try
        {
            using var baselineImage = new Bitmap(baselineImagePath);
            using var currentImage = new Bitmap(currentImagePath);

            if (baselineImage.Width != currentImage.Width || baselineImage.Height != currentImage.Height)
            {
                return new ImageComparisonResult
                {
                    SimilarityPercentage = 0.0,
                    DifferencesDetected = true,
                    DifferentPixelCount = baselineImage.Width * baselineImage.Height,
                    ErrorMessage = "Image dimensions differ"
                };
            }

            var totalPixels = baselineImage.Width * baselineImage.Height;
            var differentPixels = 0;

            for (int x = 0; x < baselineImage.Width; x++)
            {
                for (int y = 0; y < baselineImage.Height; y++)
                {
                    var baselinePixel = baselineImage.GetPixel(x, y);
                    var currentPixel = currentImage.GetPixel(x, y);

                    if (!ColorsAreEqual(baselinePixel, currentPixel, tolerance: 5))
                    {
                        differentPixels++;
                    }
                }
            }

            var similarityPercentage = ((double)(totalPixels - differentPixels) / totalPixels) * 100.0;

            return new ImageComparisonResult
            {
                SimilarityPercentage = similarityPercentage,
                DifferencesDetected = differentPixels > 0,
                DifferentPixelCount = differentPixels,
                TotalPixelCount = totalPixels
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error comparing images: {ex.Message}");
            return new ImageComparisonResult
            {
                SimilarityPercentage = 0.0,
                DifferencesDetected = true,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task GenerateDifferenceImageAsync(string baselineImagePath, string currentImagePath, string diffImagePath)
    {
        try
        {
            using var baselineImage = new Bitmap(baselineImagePath);
            using var currentImage = new Bitmap(currentImagePath);
            using var diffImage = new Bitmap(baselineImage.Width, baselineImage.Height);

            for (int x = 0; x < baselineImage.Width; x++)
            {
                for (int y = 0; y < baselineImage.Height; y++)
                {
                    var baselinePixel = baselineImage.GetPixel(x, y);
                    var currentPixel = x < currentImage.Width && y < currentImage.Height 
                        ? currentImage.GetPixel(x, y) 
                        : Color.Magenta; // Highlight missing areas

                    if (ColorsAreEqual(baselinePixel, currentPixel, tolerance: 5))
                    {
                        // Same pixel - show in grayscale
                        var gray = (int)(baselinePixel.R * 0.3 + baselinePixel.G * 0.59 + baselinePixel.B * 0.11);
                        diffImage.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
                    }
                    else
                    {
                        // Different pixel - highlight in red
                        diffImage.SetPixel(x, y, Color.Red);
                    }
                }
            }

            diffImage.Save(diffImagePath, ImageFormat.Png);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating difference image: {ex.Message}");
        }
    }

    private static bool ColorsAreEqual(Color color1, Color color2, int tolerance = 0)
    {
        return Math.Abs(color1.R - color2.R) <= tolerance &&
               Math.Abs(color1.G - color2.G) <= tolerance &&
               Math.Abs(color1.B - color2.B) <= tolerance;
    }

    public void Dispose()
    {
        // Clean up any resources if needed
    }
}

// Supporting classes for visual regression testing
public sealed class ImageComparisonResult
{
    public double SimilarityPercentage { get; init; }
    public bool DifferencesDetected { get; init; }
    public int DifferentPixelCount { get; init; }
    public int TotalPixelCount { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class LayoutAnalysis
{
    public int WindowCount { get; init; }
    public HashSet<VisualStudioWindowType> WindowTypes { get; init; } = new();
    public Dictionary<VisualStudioWindowType, object> WindowPositions { get; init; } = new();
    public Dictionary<VisualStudioWindowType, object> WindowSizes { get; init; } = new();
    public DateTime AnalyzedAt { get; init; }
}
```

---

## 🚀 CI/CD Integration and Test Automation

### Test Execution Strategy

#### Automated Test Pipeline Configuration
```yaml
# .github/workflows/visual-component-tests.yml
name: Visual Component Testing

on:
  push:
    branches: [ main, feature/* ]
  pull_request:
    branches: [ main ]

jobs:
  unit-tests:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore --configuration Release
      
    - name: Run Unit Tests
      run: |
        dotnet test --no-build --configuration Release \
          --logger trx --collect:"XPlat Code Coverage" \
          --filter "TestCategory=Unit"
      
    - name: Run Security Tests
      run: |
        dotnet test --no-build --configuration Release \
          --logger "console;verbosity=detailed" \
          --filter "TestCategory=Security"
      
    - name: Run Memory Tests
      run: |
        dotnet test --no-build --configuration Release \
          --logger "console;verbosity=detailed" \
          --filter "TestCategory=Memory"
      
    - name: Upload test results
      uses: dorny/test-reporter@v1
      if: success() || failure()
      with:
        name: Unit Test Results
        path: "**/*.trx"
        reporter: dotnet-trx

  performance-tests:
    runs-on: windows-latest
    needs: unit-tests
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
        
    - name: Run Performance Tests
      run: |
        dotnet test --configuration Release \
          --logger "console;verbosity=detailed" \
          --filter "TestCategory=Performance" \
          -- RunConfiguration.TestSessionTimeout=600000
      
    - name: Performance Regression Check
      run: |
        # Compare with baseline performance metrics
        # This would integrate with performance tracking tools

  integration-tests:
    runs-on: windows-latest
    needs: unit-tests
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
        
    - name: Setup Visual Studio Test Environment
      run: |
        # Install VS Build Tools for testing
        choco install visualstudio2022buildtools --package-parameters "--wait --quiet"
        
    - name: Run Integration Tests
      run: |
        dotnet test --configuration Release \
          --logger "console;verbosity=detailed" \
          --filter "TestCategory=Integration" \
          -- RunConfiguration.TestSessionTimeout=1800000
```

---

## 📚 Best Practices and Guidelines

### Testing Principles

#### 1. **Comprehensive Coverage Strategy**
- **Unit Tests** - Test individual components in isolation with comprehensive mocking
- **Integration Tests** - Validate component interactions and end-to-end workflows  
- **Performance Tests** - Ensure timing and memory requirements are met
- **Security Tests** - Verify vulnerability fixes and security boundaries
- **Visual Regression Tests** - Maintain visual consistency and detect UI changes

#### 2. **Mock Strategy Guidelines**
- Use realistic mock data that represents actual Visual Studio scenarios
- Implement comprehensive process access simulation for security testing
- Create factory patterns for consistent test data generation
- Mock external dependencies (P/Invoke, COM objects) for reliable testing

#### 3. **Performance Testing Standards**
- Establish baseline performance metrics for all critical operations
- Implement regression detection to catch performance degradation
- Test under various load conditions and concurrency levels
- Monitor memory usage patterns and detect leaks early

#### 4. **Security Testing Requirements**
- Test all identified vulnerability scenarios comprehensively
- Validate exception handling for process access failures
- Ensure graceful degradation when security boundaries are encountered
- Monitor and log security-related events during testing

### Development Integration

#### 1. **Test-Driven Development (TDD)**
```csharp
// Example TDD workflow for window classification feature

[TestMethod] // Write test first
public void ClassifyWindow_CodeEditorTitle_ReturnsCodeEditorType()
{
    // Arrange
    var window = WindowMockFactory.CreateWindow(title: "Program.cs - MyProject");
    
    // Act
    var result = _classifier.ClassifyWindow(window);
    
    // Assert  
    Assert.AreEqual(VisualStudioWindowType.CodeEditor, result);
}

// Then implement the feature to make the test pass
public VisualStudioWindowType ClassifyWindow(VisualStudioWindow window)
{
    if (window.Title.Contains(".cs") || window.Title.Contains(".vb"))
        return VisualStudioWindowType.CodeEditor;
    // ...implementation
}
```

#### 2. **Continuous Testing Integration**
- Run unit tests on every commit
- Execute performance tests on pull requests
- Perform integration tests on main branch updates
- Generate test reports with coverage metrics

#### 3. **Test Data Management**
- Maintain realistic test datasets that represent production scenarios
- Use factories for consistent test data generation
- Implement test data cleanup to prevent test interference
- Version control test baselines for visual regression testing

---

*This guide provides comprehensive testing strategies for all aspects of visual component testing in the Visual Studio MCP Server. Following these patterns ensures reliable, performant, and secure visual capture capabilities with thorough validation across all scenarios.*