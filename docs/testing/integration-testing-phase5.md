# Integration Testing Guide - Phase 5 Advanced Visual Capture

Comprehensive guide for integration testing of Phase 5 Advanced Visual Capture features, covering end-to-end scenarios, Claude Code integration testing, multi-monitor testing procedures, performance integration testing, and security integration validation.

## 📋 Overview

Integration testing for Phase 5 ensures that the advanced visual capture capabilities work seamlessly across the entire technology stack, from low-level Windows API integration through COM interop to Claude Code MCP tool integration. This guide provides systematic approaches for validating the complete system functionality.

### Integration Testing Scope

- **End-to-End Capture Workflows**: Complete capture operations from initiation to Claude Code consumption
- **Claude Code MCP Integration**: Tool registration, invocation, and response handling
- **Multi-Monitor System Testing**: Comprehensive testing across various display configurations
- **Performance Integration**: System-wide performance validation under realistic conditions
- **Security Integration**: Cross-component security validation and boundary testing

---

## 🎯 Testing Architecture

### Integration Test Structure

```
Integration Tests
├── End-to-End Tests
│   ├── Capture Workflow Tests
│   ├── Visual State Analysis Tests
│   └── Error Recovery Tests
├── Claude Code Integration Tests
│   ├── MCP Tool Registration Tests
│   ├── Tool Invocation Tests
│   └── Response Validation Tests
├── Multi-Monitor Tests
│   ├── Display Configuration Tests
│   ├── Cross-Monitor Capture Tests
│   └── DPI Scaling Tests
├── Performance Integration Tests
│   ├── Load Testing
│   ├── Stress Testing
│   └── Endurance Testing
└── Security Integration Tests
    ├── Process Access Tests
    ├── Memory Security Tests
    └── COM Security Tests
```

### Test Environment Requirements

#### Hardware Configuration
- **Primary Development Machine**: Visual Studio 2022, Windows 11, 16GB RAM, dedicated graphics
- **Multi-Monitor Test Rig**: Dual/triple monitor setup with different resolutions and DPI scaling
- **Performance Test Environment**: High-spec machine for load testing (32GB RAM, high-end CPU/GPU)
- **Minimal Spec Environment**: Low-spec machine to test minimum requirements (8GB RAM, integrated graphics)

#### Software Requirements
```bash
# Core requirements
Visual Studio 2022 Professional (17.8+)
.NET 8 SDK
Windows 11 (or Windows 10 22H2)

# Testing tools
MSTest v3.0+
Moq v4.20+
FluentAssertions v6.12+
NBomber (for load testing)
Selenium WebDriver (for UI automation)

# Monitoring tools
Application Insights SDK
PerfView (for ETW tracing)
Process Monitor (for system monitoring)
```

---

## 🔄 End-to-End Testing Scenarios

### 1. Complete Capture Workflow Testing

#### Single Window Capture E2E Test
```csharp
[TestClass]
[TestCategory("EndToEnd")]
[TestCategory("CaptureWorkflow")]
public class SingleWindowCaptureE2ETests
{
    private VisualStudioInstance _vsInstance;
    private ImagingService _imagingService;
    private IMcpServer _mcpServer;

    [TestInitialize]
    public async Task Setup()
    {
        // Start fresh Visual Studio instance
        _vsInstance = await VisualStudioTestHelper.StartCleanInstanceAsync();
        
        // Initialize imaging service
        _imagingService = new ImagingService(_logger, _memoryManager);
        
        // Start MCP server
        _mcpServer = await McpServerTestHelper.StartServerAsync();
        
        // Wait for VS to fully initialize
        await _vsInstance.WaitForFullInitializationAsync(TimeSpan.FromMinutes(2));
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task CaptureCodeEditorWindow_EndToEnd_Success()
    {
        // Arrange: Open a code file in Visual Studio
        var codeFile = TestFiles.GetSampleCSharpFile();
        await _vsInstance.OpenFileAsync(codeFile);
        
        var codeEditorWindow = await _vsInstance.GetCodeEditorWindowAsync();
        Assert.IsNotNull(codeEditorWindow, "Code editor window should be available");

        // Act: Perform capture through MCP server
        var mcpRequest = new McpToolRequest
        {
            ToolName = "vs_capture_window",
            Parameters = new
            {
                window_handle = codeEditorWindow.Handle.ToInt64(),
                include_annotations = true,
                capture_quality = "balanced"
            }
        };

        var mcpResponse = await _mcpServer.InvokeToolAsync(mcpRequest);

        // Assert: Validate complete response
        Assert.IsTrue(mcpResponse.IsSuccess, $"MCP tool should succeed: {mcpResponse.Error}");
        
        var captureResult = JsonConvert.DeserializeObject<SpecializedCapture>(mcpResponse.Content);
        Assert.IsNotNull(captureResult, "Capture result should not be null");
        Assert.IsTrue(captureResult.ImageData.Length > 0, "Image data should not be empty");
        Assert.AreEqual(VisualStudioWindowType.CodeEditor, captureResult.WindowType);
        
        // Validate annotations
        Assert.IsNotNull(captureResult.Annotations, "Annotations should be present");
        Assert.IsTrue(captureResult.Annotations.VisualElements.Count > 0, "Visual elements should be detected");
        
        // Validate image quality
        var imageMetadata = await ImageAnalysisHelper.AnalyzeImageAsync(captureResult.ImageData);
        Assert.IsTrue(imageMetadata.Width >= 800, "Minimum width requirement");
        Assert.IsTrue(imageMetadata.Height >= 600, "Minimum height requirement");
        Assert.IsTrue(imageMetadata.TextClarity >= 0.8, "Text should be clearly readable");
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task CaptureFullIdeLayout_EndToEnd_Success()
    {
        // Arrange: Set up comprehensive Visual Studio layout
        await _vsInstance.ConfigureLayoutAsync(new LayoutConfiguration
        {
            ShowSolutionExplorer = true,
            ShowPropertiesWindow = true,
            ShowOutputWindow = true,
            ShowErrorList = true,
            DockingLayout = DockingLayout.Standard
        });

        // Create test solution with multiple projects
        var testSolution = await TestSolutionHelper.CreateComplexSolutionAsync();
        await _vsInstance.OpenSolutionAsync(testSolution.Path);

        // Act: Capture full IDE layout
        var mcpRequest = new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new
            {
                include_layout_metadata = true,
                include_annotations = true,
                capture_quality = "high"
            }
        };

        var mcpResponse = await _mcpServer.InvokeToolAsync(mcpRequest);

        // Assert: Validate comprehensive IDE capture
        Assert.IsTrue(mcpResponse.IsSuccess, $"Full IDE capture should succeed: {mcpResponse.Error}");
        
        var fullIdeCapture = JsonConvert.DeserializeObject<FullIdeCapture>(mcpResponse.Content);
        Assert.IsNotNull(fullIdeCapture, "Full IDE capture result should not be null");
        
        // Validate stitched image
        Assert.IsNotNull(fullIdeCapture.StitchedImage, "Stitched image should be present");
        Assert.IsTrue(fullIdeCapture.StitchedImage.ImageData.Length > 0, "Stitched image data should not be empty");
        
        // Validate layout metadata
        Assert.IsNotNull(fullIdeCapture.LayoutMetadata, "Layout metadata should be present");
        Assert.IsTrue(fullIdeCapture.LayoutMetadata.WindowHierarchy.Count >= 4, "Should capture major window components");
        
        // Validate component captures
        Assert.IsNotNull(fullIdeCapture.ComponentCaptures, "Component captures should be present");
        Assert.IsTrue(fullIdeCapture.ComponentCaptures.Count >= 4, "Should have individual component captures");
        
        // Validate annotations
        Assert.IsNotNull(fullIdeCapture.Annotations, "Annotations should be present");
        Assert.IsTrue(fullIdeCapture.Annotations.Count > 0, "Should have window annotations");
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _mcpServer?.StopAsync();
        await _vsInstance?.CloseAsync();
    }
}
```

### 2. Visual State Analysis E2E Testing

#### Visual State Comparison Testing
```csharp
[TestClass]
[TestCategory("EndToEnd")]
[TestCategory("VisualStateAnalysis")]
public class VisualStateAnalysisE2ETests
{
    [TestMethod]
    [TestCategory("Critical")]
    public async Task VisualStateAnalysis_DetectChanges_EndToEnd()
    {
        // Arrange: Capture initial state
        var initialState = await CaptureInitialVisualStateAsync();
        
        // Make significant changes to Visual Studio layout
        await _vsInstance.ModifyLayoutAsync(new LayoutModification
        {
            OpenAdditionalWindows = new[] { "TaskList", "BookmarkWindow" },
            ChangeCodeEditorContent = true,
            ModifyDockingLayout = true
        });

        // Wait for changes to be reflected
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Act: Analyze visual state changes
        var mcpRequest = new McpToolRequest
        {
            ToolName = "vs_analyse_visual_state",
            Parameters = new
            {
                compare_with_previous = true,
                include_change_detection = true,
                generate_insights = true
            }
        };

        var mcpResponse = await _mcpServer.InvokeToolAsync(mcpRequest);

        // Assert: Validate change detection
        Assert.IsTrue(mcpResponse.IsSuccess, $"Visual state analysis should succeed: {mcpResponse.Error}");
        
        var stateAnalysis = JsonConvert.DeserializeObject<VisualStateAnalysis>(mcpResponse.Content);
        Assert.IsNotNull(stateAnalysis, "State analysis should not be null");
        
        // Validate component changes
        Assert.IsNotNull(stateAnalysis.ComponentChanges, "Component changes should be detected");
        Assert.IsTrue(stateAnalysis.ComponentChanges.Count >= 2, "Should detect multiple component changes");
        
        // Validate layout changes
        Assert.IsNotNull(stateAnalysis.LayoutChanges, "Layout changes should be detected");
        Assert.IsTrue(stateAnalysis.LayoutChanges.NewWindows.Count >= 2, "Should detect new windows");
        
        // Validate actionable insights
        Assert.IsNotNull(stateAnalysis.ActionableInsights, "Should provide actionable insights");
        Assert.IsTrue(stateAnalysis.ActionableInsights.Count > 0, "Should have specific insights");
    }

    private async Task<FullIdeCapture> CaptureInitialVisualStateAsync()
    {
        var mcpRequest = new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new { include_layout_metadata = true }
        };

        var response = await _mcpServer.InvokeToolAsync(mcpRequest);
        return JsonConvert.DeserializeObject<FullIdeCapture>(response.Content);
    }
}
```

### 3. Error Recovery and Resilience Testing

#### Error Recovery E2E Tests
```csharp
[TestClass]
[TestCategory("EndToEnd")]
[TestCategory("ErrorRecovery")]
public class ErrorRecoveryE2ETests
{
    [TestMethod]
    [TestCategory("Resilience")]
    public async Task CaptureOperations_VisualStudioCrash_GracefulRecovery()
    {
        // Arrange: Start capture operation
        var captureTask = StartLongRunningCaptureAsync();
        
        // Act: Simulate Visual Studio crash during capture
        await Task.Delay(TimeSpan.FromSeconds(2)); // Let capture start
        await _vsInstance.SimulateCrashAsync();

        // Wait for capture operation to complete
        var captureResult = await captureTask;

        // Assert: Validate graceful error handling
        Assert.IsFalse(captureResult.IsSuccess, "Capture should fail gracefully");
        Assert.AreEqual("VS_CONNECTION_LOST", captureResult.ErrorCode);
        Assert.IsNotNull(captureResult.ErrorDetails, "Should provide error details");
        Assert.IsTrue(captureResult.ErrorDetails.Contains("recovery"), "Should suggest recovery");
    }

    [TestMethod]
    [TestCategory("Resilience")]
    public async Task MemoryPressure_GracefulDegradation_EndToEnd()
    {
        // Arrange: Set low memory thresholds for testing
        var testConfig = new MemoryConfiguration
        {
            WarningThreshold = 10_000_000,  // 10MB
            RejectionThreshold = 20_000_000  // 20MB
        };
        
        await _imagingService.ConfigureMemoryLimitsAsync(testConfig);

        // Generate memory pressure
        var memoryConsumers = new List<byte[]>();
        for (int i = 0; i < 50; i++)
        {
            memoryConsumers.Add(new byte[1_000_000]); // 1MB each
        }

        // Act: Attempt capture under memory pressure
        var mcpRequest = new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new { capture_quality = "high" }
        };

        var mcpResponse = await _mcpServer.InvokeToolAsync(mcpRequest);

        // Assert: Validate graceful degradation
        if (mcpResponse.IsSuccess)
        {
            // Should have automatically reduced quality
            var capture = JsonConvert.DeserializeObject<FullIdeCapture>(mcpResponse.Content);
            Assert.IsTrue(capture.QualityReduced, "Quality should be automatically reduced");
        }
        else
        {
            // Should provide clear memory pressure error
            Assert.AreEqual("MEMORY_PRESSURE", mcpResponse.ErrorCode);
            Assert.IsTrue(mcpResponse.Error.Contains("memory"), "Error should mention memory pressure");
        }
    }
}
```

---

## 🔌 Claude Code Integration Testing

### 1. MCP Tool Registration and Discovery

#### Tool Registration Tests
```csharp
[TestClass]
[TestCategory("ClaudeIntegration")]
[TestCategory("MCPTools")]
public class McpToolRegistrationTests
{
    [TestMethod]
    [TestCategory("Critical")]
    public async Task McpServer_ToolRegistration_AllToolsAvailable()
    {
        // Arrange & Act: Start MCP server and get tool list
        var tools = await _mcpServer.GetAvailableToolsAsync();

        // Assert: Validate all Phase 5 tools are registered
        var expectedTools = new[]
        {
            "vs_capture_window",
            "vs_capture_full_ide", 
            "vs_analyse_visual_state",
            "vs_list_windows",
            "vs_get_window_info",
            "vs_capture_specialized"
        };

        foreach (var expectedTool in expectedTools)
        {
            var tool = tools.FirstOrDefault(t => t.Name == expectedTool);
            Assert.IsNotNull(tool, $"Tool {expectedTool} should be registered");
            Assert.IsNotNull(tool.Description, $"Tool {expectedTool} should have description");
            Assert.IsNotNull(tool.Parameters, $"Tool {expectedTool} should have parameter schema");
        }
    }

    [TestMethod]
    [TestCategory("Validation")]
    public async Task McpTools_ParameterValidation_CorrectSchemas()
    {
        // Act: Get tool schemas
        var tools = await _mcpServer.GetAvailableToolsAsync();
        var captureWindowTool = tools.First(t => t.Name == "vs_capture_window");

        // Assert: Validate parameter schema structure
        var schema = captureWindowTool.Parameters;
        Assert.IsNotNull(schema.Properties, "Should have parameter properties");
        
        // Validate required parameters
        Assert.IsTrue(schema.Required.Contains("window_handle"), "window_handle should be required");
        
        // Validate optional parameters
        Assert.IsTrue(schema.Properties.ContainsKey("include_annotations"), "Should support annotations");
        Assert.IsTrue(schema.Properties.ContainsKey("capture_quality"), "Should support quality setting");
        Assert.IsTrue(schema.Properties.ContainsKey("timeout"), "Should support timeout setting");
    }
}
```

### 2. Claude Code Tool Invocation Testing

#### Tool Invocation Integration Tests
```csharp
[TestClass]
[TestCategory("ClaudeIntegration")]
[TestCategory("ToolInvocation")]
public class ClaudeCodeToolInvocationTests
{
    private IClaudeCodeSimulator _claudeSimulator;

    [TestInitialize]
    public async Task Setup()
    {
        _claudeSimulator = new ClaudeCodeSimulator(_mcpServer);
        await _claudeSimulator.InitializeAsync();
    }

    [TestMethod]
    [TestCategory("Critical")]
    public async Task ClaudeCode_InvokeCaptureWindow_SuccessfulIntegration()
    {
        // Arrange: Prepare Claude Code simulation environment
        var codeEditorWindow = await _vsInstance.GetCodeEditorWindowAsync();
        
        // Act: Simulate Claude Code tool invocation
        var claudeRequest = new ClaudeToolRequest
        {
            Tool = "vs_capture_window",
            Arguments = new
            {
                window_handle = codeEditorWindow.Handle.ToInt64(),
                include_annotations = true,
                capture_quality = "balanced"
            }
        };

        var claudeResponse = await _claudeSimulator.InvokeToolAsync(claudeRequest);

        // Assert: Validate Claude Code integration
        Assert.IsTrue(claudeResponse.Success, $"Claude Code integration should succeed: {claudeResponse.Error}");
        Assert.IsNotNull(claudeResponse.Result, "Should return capture result");
        
        // Validate response format for Claude Code consumption
        var responseData = JsonConvert.DeserializeObject<dynamic>(claudeResponse.Result);
        Assert.IsNotNull(responseData.image_data, "Should contain image data");
        Assert.IsNotNull(responseData.metadata, "Should contain metadata");
        Assert.IsNotNull(responseData.annotations, "Should contain annotations");
        
        // Validate Claude Code can process the response
        var analysisResult = await _claudeSimulator.AnalyzeVisualContentAsync(claudeResponse.Result);
        Assert.IsTrue(analysisResult.CanAnalyze, "Claude should be able to analyze the content");
        Assert.IsTrue(analysisResult.ContentQuality >= 0.8, "Content quality should be sufficient for Claude");
    }

    [TestMethod]
    [TestCategory("Performance")]
    public async Task ClaudeCode_ConcurrentToolInvocations_CorrectHandling()
    {
        // Arrange: Prepare multiple concurrent requests
        var concurrentRequests = new List<Task<ClaudeToolResponse>>();
        
        for (int i = 0; i < 5; i++)
        {
            var request = new ClaudeToolRequest
            {
                Tool = "vs_capture_window",
                Arguments = new { window_handle = _defaultWindowHandle }
            };
            
            concurrentRequests.Add(_claudeSimulator.InvokeToolAsync(request));
        }

        // Act: Execute concurrent requests
        var responses = await Task.WhenAll(concurrentRequests);

        // Assert: Validate all requests succeeded
        Assert.IsTrue(responses.All(r => r.Success), "All concurrent requests should succeed");
        
        // Validate no resource conflicts
        var uniqueResults = responses.Select(r => r.RequestId).Distinct().Count();
        Assert.AreEqual(5, uniqueResults, "Should handle concurrent requests independently");
    }

    [TestMethod]
    [TestCategory("ErrorHandling")]
    public async Task ClaudeCode_InvalidParameters_AppropriateErrorResponse()
    {
        // Act: Invoke tool with invalid parameters
        var claudeRequest = new ClaudeToolRequest
        {
            Tool = "vs_capture_window",
            Arguments = new
            {
                window_handle = "invalid_handle",
                capture_quality = "ultra_super_high", // Invalid quality
                timeout = -1 // Invalid timeout
            }
        };

        var claudeResponse = await _claudeSimulator.InvokeToolAsync(claudeRequest);

        // Assert: Validate error handling
        Assert.IsFalse(claudeResponse.Success, "Should fail with invalid parameters");
        Assert.IsNotNull(claudeResponse.Error, "Should provide error message");
        Assert.IsTrue(claudeResponse.Error.Contains("parameter"), "Error should mention parameter issues");
        
        // Validate error format is Claude-friendly
        Assert.IsNotNull(claudeResponse.ErrorCode, "Should provide structured error code");
        Assert.IsNotNull(claudeResponse.SuggestedAction, "Should provide suggested action");
    }
}
```

### 3. Response Format and Data Validation

#### Claude Code Response Format Tests
```csharp
[TestClass]
[TestCategory("ClaudeIntegration")]
[TestCategory("ResponseFormat")]
public class ClaudeCodeResponseFormatTests
{
    [TestMethod]
    [TestCategory("DataFormat")]
    public async Task CaptureResponse_FormatValidation_ClaudeCompatible()
    {
        // Act: Perform capture operation
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_capture_window",
            Parameters = new { window_handle = _testWindowHandle }
        });

        // Assert: Validate response structure for Claude Code
        Assert.IsTrue(response.IsSuccess, "Capture should succeed");
        
        var captureData = JsonConvert.DeserializeObject<dynamic>(response.Content);
        
        // Validate required fields for Claude Code
        Assert.IsNotNull(captureData.image_data, "image_data is required for Claude");
        Assert.IsNotNull(captureData.image_format, "image_format should be specified");
        Assert.IsNotNull(captureData.timestamp, "timestamp should be included");
        Assert.IsNotNull(captureData.window_info, "window_info should be included");
        
        // Validate image data format
        var imageData = captureData.image_data.ToString();
        Assert.IsTrue(imageData.StartsWith("data:image/"), "Should use data URI format");
        Assert.IsTrue(imageData.Contains("base64,"), "Should include base64 encoding");
        
        // Validate metadata structure
        Assert.IsNotNull(captureData.metadata, "metadata should be present");
        Assert.IsNotNull(captureData.metadata.dimensions, "dimensions should be included");
        Assert.IsNotNull(captureData.metadata.dpi_scaling, "DPI scaling info should be included");
    }

    [TestMethod]
    [TestCategory("ImageProcessing")]
    public async Task CaptureResponse_ImageQuality_ClaudeAnalysisReady()
    {
        // Act: Capture with high quality for Claude analysis
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_capture_window",
            Parameters = new 
            { 
                window_handle = _testWindowHandle,
                capture_quality = "high",
                optimize_for_analysis = true
            }
        });

        // Assert: Validate image quality for Claude analysis
        Assert.IsTrue(response.IsSuccess, "High quality capture should succeed");
        
        var captureData = JsonConvert.DeserializeObject<dynamic>(response.Content);
        var imageAnalysis = await ImageQualityAnalyzer.AnalyzeForClaudeAsync(captureData);
        
        Assert.IsTrue(imageAnalysis.TextReadability >= 0.9, "Text should be highly readable");
        Assert.IsTrue(imageAnalysis.ContrastRatio >= 4.5, "Should meet accessibility contrast standards");
        Assert.IsTrue(imageAnalysis.Sharpness >= 0.8, "Image should be sharp for analysis");
        Assert.IsTrue(imageAnalysis.ColorAccuracy >= 0.9, "Colors should be accurate");
    }
}
```

---

## 🖥️ Multi-Monitor Testing Procedures

### 1. Display Configuration Testing

#### Multi-Monitor Setup Validation
```csharp
[TestClass]
[TestCategory("MultiMonitor")]
[TestCategory("DisplayConfiguration")]
public class MultiMonitorConfigurationTests
{
    [TestMethod]
    [TestCategory("Critical")]
    public async Task MultiMonitorSetup_DisplayDetection_AllMonitorsDetected()
    {
        // Arrange: Verify test environment has multiple monitors
        var displays = await DisplayHelper.GetConnectedDisplaysAsync();
        Assume.That(displays.Count >= 2, "Test requires at least 2 monitors");

        // Act: Detect displays through MCP server
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_list_displays",
            Parameters = new { include_details = true }
        });

        // Assert: Validate all displays are detected
        Assert.IsTrue(response.IsSuccess, "Display detection should succeed");
        
        var detectedDisplays = JsonConvert.DeserializeObject<List<DisplayInfo>>(response.Content);
        Assert.AreEqual(displays.Count, detectedDisplays.Count, "Should detect all connected displays");
        
        foreach (var display in detectedDisplays)
        {
            Assert.IsTrue(display.Width > 0, "Display width should be valid");
            Assert.IsTrue(display.Height > 0, "Display height should be valid");
            Assert.IsNotNull(display.DeviceName, "Device name should be provided");
            Assert.IsTrue(display.DpiScaling > 0, "DPI scaling should be provided");
        }
    }

    [TestMethod]
    [TestCategory("Configuration")]
    public async Task MultiMonitorSetup_DifferentDpiScaling_CorrectDetection()
    {
        // Arrange: Configure monitors with different DPI scaling
        var primaryMonitor = new DisplayConfiguration { DpiScaling = 100 };
        var secondaryMonitor = new DisplayConfiguration { DpiScaling = 150 };
        
        await DisplayConfigurationHelper.ConfigureTestSetupAsync(primaryMonitor, secondaryMonitor);

        // Act: Detect DPI scaling differences
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_analyze_display_setup",
            Parameters = new { include_dpi_analysis = true }
        });

        // Assert: Validate DPI scaling detection
        Assert.IsTrue(response.IsSuccess, "DPI analysis should succeed");
        
        var analysis = JsonConvert.DeserializeObject<DisplayAnalysis>(response.Content);
        Assert.IsTrue(analysis.HasMixedDpiScaling, "Should detect mixed DPI scaling");
        Assert.IsTrue(analysis.RequiresDpiCompensation, "Should require DPI compensation");
        Assert.IsNotNull(analysis.RecommendedCaptureStrategy, "Should provide capture strategy");
    }
}
```

### 2. Cross-Monitor Capture Testing

#### Cross-Monitor Window Capture Tests
```csharp
[TestClass]
[TestCategory("MultiMonitor")]
[TestCategory("CrossMonitorCapture")]
public class CrossMonitorCaptureTests
{
    [TestMethod]
    [TestCategory("Critical")]
    public async Task VisualStudio_SpanningMultipleMonitors_CorrectCapture()
    {
        // Arrange: Configure Visual Studio to span multiple monitors
        await _vsInstance.ConfigureMultiMonitorLayoutAsync(new MultiMonitorLayout
        {
            PrimaryEditor = MonitorId.Primary,
            SolutionExplorer = MonitorId.Secondary,
            OutputWindows = MonitorId.Secondary,
            ToolWindows = MonitorId.Secondary
        });

        // Act: Capture full IDE spanning monitors
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new 
            { 
                multi_monitor_handling = "stitch",
                coordinate_normalization = true
            }
        });

        // Assert: Validate cross-monitor capture
        Assert.IsTrue(response.IsSuccess, "Cross-monitor capture should succeed");
        
        var fullIdeCapture = JsonConvert.DeserializeObject<FullIdeCapture>(response.Content);
        Assert.IsNotNull(fullIdeCapture.StitchedImage, "Should have stitched image");
        
        // Validate stitched image dimensions
        var imageMetadata = await ImageAnalysisHelper.AnalyzeImageAsync(fullIdeCapture.StitchedImage.ImageData);
        Assert.IsTrue(imageMetadata.Width > 1920, "Stitched image should be wider than single monitor");
        
        // Validate layout metadata includes all monitors
        Assert.IsTrue(fullIdeCapture.LayoutMetadata.SpansMultipleMonitors, "Should indicate multi-monitor span");
        Assert.IsTrue(fullIdeCapture.LayoutMetadata.MonitorCount >= 2, "Should indicate monitor count");
    }

    [TestMethod]
    [TestCategory("Positioning")]
    public async Task CrossMonitorCapture_CoordinateNormalization_CorrectPositioning()
    {
        // Arrange: Place Visual Studio windows on different monitors
        var primaryWindow = await _vsInstance.GetPrimaryWindowAsync();
        var secondaryWindow = await _vsInstance.GetSolutionExplorerAsync();
        
        await WindowPositioningHelper.MoveToMonitorAsync(primaryWindow, MonitorId.Primary);
        await WindowPositioningHelper.MoveToMonitorAsync(secondaryWindow, MonitorId.Secondary);

        // Act: Capture with coordinate normalization
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new 
            { 
                normalize_coordinates = true,
                include_position_metadata = true
            }
        });

        // Assert: Validate coordinate normalization
        Assert.IsTrue(response.IsSuccess, "Coordinate normalization should succeed");
        
        var capture = JsonConvert.DeserializeObject<FullIdeCapture>(response.Content);
        var layout = capture.LayoutMetadata;
        
        // Validate normalized coordinates
        var primaryWindowLayout = layout.WindowHierarchy.First(w => w.WindowType == VisualStudioWindowType.MainWindow);
        var secondaryWindowLayout = layout.WindowHierarchy.First(w => w.WindowType == VisualStudioWindowType.SolutionExplorer);
        
        Assert.IsTrue(primaryWindowLayout.NormalizedBounds.X >= 0, "Primary window X should be normalized");
        Assert.IsTrue(secondaryWindowLayout.NormalizedBounds.X > primaryWindowLayout.NormalizedBounds.Right, 
            "Secondary window should be positioned after primary");
    }
}
```

### 3. DPI Scaling Integration Testing

#### DPI Scaling Compensation Tests
```csharp
[TestClass]
[TestCategory("MultiMonitor")]
[TestCategory("DpiScaling")]
public class DpiScalingIntegrationTests
{
    [TestMethod]
    [TestCategory("DpiCompensation")]
    public async Task MixedDpiEnvironment_CaptureCompensation_ConsistentResults()
    {
        // Arrange: Configure mixed DPI environment
        var dpiConfig = new DpiTestConfiguration
        {
            PrimaryMonitor = new MonitorDpiConfig { DpiScaling = 100, Resolution = new Size(1920, 1080) },
            SecondaryMonitor = new MonitorDpiConfig { DpiScaling = 150, Resolution = new Size(2560, 1440) }
        };
        
        await DpiTestHelper.ConfigureTestEnvironmentAsync(dpiConfig);

        // Move Visual Studio windows to different DPI monitors
        await _vsInstance.DistributeWindowsAcrossMonitorsAsync();

        // Act: Capture with DPI compensation
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_capture_full_ide",
            Parameters = new 
            { 
                enable_dpi_compensation = true,
                target_dpi = 96 // Standard DPI for consistent comparison
            }
        });

        // Assert: Validate DPI compensation
        Assert.IsTrue(response.IsSuccess, "DPI compensated capture should succeed");
        
        var capture = JsonConvert.DeserializeObject<FullIdeCapture>(response.Content);
        
        // Validate consistent scaling across monitors
        foreach (var componentCapture in capture.ComponentCaptures)
        {
            var metadata = componentCapture.ProcessingMetadata;
            Assert.AreEqual(96, metadata.TargetDpi, "All components should be normalized to target DPI");
            Assert.IsTrue(metadata.DpiCompensationApplied, "DPI compensation should be applied");
        }
        
        // Validate visual consistency
        var analysisResult = await DpiConsistencyAnalyzer.AnalyzeCaptureAsync(capture);
        Assert.IsTrue(analysisResult.IsConsistent, "DPI compensation should result in consistent scaling");
        Assert.IsTrue(analysisResult.TextSizeVariance < 0.1, "Text sizes should be consistent across monitors");
    }
}
```

---

## ⚡ Performance Integration Testing

### 1. Load Testing

#### Concurrent Capture Load Tests
```csharp
[TestClass]
[TestCategory("Performance")]
[TestCategory("LoadTesting")]
public class CaptureLoadTests
{
    [TestMethod]
    [TestCategory("HighLoad")]
    public async Task ConcurrentCaptures_HighLoad_SystemStability()
    {
        // Arrange: Configure high-load scenario
        var concurrencyLevel = Environment.ProcessorCount * 2;
        var iterationsPerThread = 20;
        var captureRequests = new List<Task<McpToolResponse>>();

        // Act: Execute concurrent capture operations
        for (int i = 0; i < concurrencyLevel; i++)
        {
            captureRequests.Add(ExecuteConcurrentCapturesAsync(iterationsPerThread));
        }

        var results = await Task.WhenAll(captureRequests);

        // Assert: Validate system stability under load
        var allResults = results.SelectMany(r => r).ToList();
        var successRate = allResults.Count(r => r.IsSuccess) / (double)allResults.Count;
        
        Assert.IsTrue(successRate >= 0.95, $"Success rate should be >= 95%, actual: {successRate:P}");
        
        // Validate performance under load
        var averageLatency = allResults.Where(r => r.IsSuccess)
            .Average(r => r.ExecutionTime.TotalMilliseconds);
        Assert.IsTrue(averageLatency < 5000, $"Average latency should be < 5s, actual: {averageLatency}ms");
        
        // Validate memory stability
        var memoryUsage = await PerformanceMonitor.GetCurrentMemoryUsageAsync();
        Assert.IsTrue(memoryUsage < 500_000_000, "Memory usage should remain under 500MB during load");
    }

    private async Task<List<McpToolResponse>> ExecuteConcurrentCapturesAsync(int iterations)
    {
        var results = new List<McpToolResponse>();
        
        for (int i = 0; i < iterations; i++)
        {
            var request = new McpToolRequest
            {
                ToolName = "vs_capture_window",
                Parameters = new { window_handle = _testWindowHandle }
            };
            
            var stopwatch = Stopwatch.StartNew();
            var result = await _mcpServer.InvokeToolAsync(request);
            stopwatch.Stop();
            
            result.ExecutionTime = stopwatch.Elapsed;
            results.Add(result);
            
            // Small delay to prevent overwhelming the system
            await Task.Delay(100);
        }
        
        return results;
    }
}
```

### 2. Stress Testing

#### Memory Pressure Stress Tests
```csharp
[TestClass]
[TestCategory("Performance")]
[TestCategory("StressTesting")]
public class MemoryPressureStressTests
{
    [TestMethod]
    [TestCategory("MemoryStress")]
    public async Task ExtremeLargeCaptureOperations_MemoryPressure_GracefulHandling()
    {
        // Arrange: Configure Visual Studio for maximum window content
        await _vsInstance.ConfigureForMaximumContentAsync();
        
        // Create memory pressure
        var memoryConsumers = new List<byte[]>();
        var targetMemoryPressure = 1_500_000_000; // 1.5GB
        
        while (GC.GetTotalMemory(false) < targetMemoryPressure)
        {
            memoryConsumers.Add(new byte[10_000_000]); // 10MB chunks
            await Task.Delay(10); // Allow GC opportunities
        }

        // Act: Attempt large capture operations under memory pressure
        var largeCaptureTasks = new List<Task<McpToolResponse>>();
        
        for (int i = 0; i < 5; i++)
        {
            var request = new McpToolRequest
            {
                ToolName = "vs_capture_full_ide",
                Parameters = new 
                { 
                    capture_quality = "ultra",
                    include_all_components = true
                }
            };
            
            largeCaptureTasks.Add(_mcpServer.InvokeToolAsync(request));
        }

        var results = await Task.WhenAll(largeCaptureTasks);

        // Assert: Validate graceful memory pressure handling
        foreach (var result in results)
        {
            if (!result.IsSuccess)
            {
                Assert.AreEqual("MEMORY_PRESSURE", result.ErrorCode, 
                    "Failed captures should indicate memory pressure");
            }
            else
            {
                // If successful, should have quality reduction
                var capture = JsonConvert.DeserializeObject<FullIdeCapture>(result.Content);
                Assert.IsTrue(capture.QualityReduced || capture.ComponentsReduced,
                    "Successful captures under pressure should show adaptation");
            }
        }
        
        // Validate system didn't crash
        var isSystemResponsive = await SystemHealthChecker.CheckResponsivenessAsync();
        Assert.IsTrue(isSystemResponsive, "System should remain responsive under memory pressure");
    }
}
```

### 3. Endurance Testing

#### Long-Running Operation Tests
```csharp
[TestClass]
[TestCategory("Performance")]
[TestCategory("EnduranceTesting")]
public class EnduranceTests
{
    [TestMethod]
    [TestCategory("LongRunning")]
    [Timeout(3600000)] // 1 hour timeout
    public async Task ContinuousCaptures_ExtendedPeriod_NoMemoryLeaks()
    {
        // Arrange: Configure for endurance testing
        var testDuration = TimeSpan.FromMinutes(30);
        var captureInterval = TimeSpan.FromSeconds(10);
        var startTime = DateTime.UtcNow;
        var memoryMeasurements = new List<MemoryMeasurement>();

        // Act: Run continuous captures for extended period
        while (DateTime.UtcNow - startTime < testDuration)
        {
            // Measure memory before capture
            var memoryBefore = GC.GetTotalMemory(false);
            
            // Perform capture
            var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
            {
                ToolName = "vs_capture_window",
                Parameters = new { window_handle = _testWindowHandle }
            });
            
            // Measure memory after capture
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryAfter = GC.GetTotalMemory(true);
            
            memoryMeasurements.Add(new MemoryMeasurement
            {
                Timestamp = DateTime.UtcNow,
                MemoryBefore = memoryBefore,
                MemoryAfter = memoryAfter,
                CaptureSuccessful = response.IsSuccess
            });

            await Task.Delay(captureInterval);
        }

        // Assert: Validate no memory leaks
        var memoryTrend = MemoryAnalyzer.AnalyzeTrend(memoryMeasurements);
        Assert.IsTrue(memoryTrend.Slope < 1000, // Less than 1KB per measurement increase
            $"Memory usage should not grow significantly over time. Slope: {memoryTrend.Slope} bytes/capture");
        
        // Validate capture success rate maintained
        var successRate = memoryMeasurements.Count(m => m.CaptureSuccessful) / 
                         (double)memoryMeasurements.Count;
        Assert.IsTrue(successRate >= 0.98, 
            $"Success rate should remain high during endurance test. Actual: {successRate:P}");
    }
}
```

---

## 🔒 Security Integration Testing

### 1. Process Access Security Testing

#### Cross-Process Security Validation
```csharp
[TestClass]
[TestCategory("Security")]
[TestCategory("ProcessAccess")]
public class ProcessAccessSecurityTests
{
    [TestMethod]
    [TestCategory("SecurityBoundary")]
    public async Task WindowEnumeration_RestrictedProcesses_SecurityRespected()
    {
        // Arrange: Start processes with different security contexts
        var restrictedProcesses = await SecurityTestHelper.StartRestrictedProcessesAsync();
        var elevatedProcesses = await SecurityTestHelper.StartElevatedProcessesAsync();

        // Act: Attempt to enumerate all windows
        var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
        {
            ToolName = "vs_list_windows",
            Parameters = new { include_all_processes = true }
        });

        // Assert: Validate security boundaries respected
        Assert.IsTrue(response.IsSuccess, "Window enumeration should succeed");
        
        var windows = JsonConvert.DeserializeObject<List<WindowInfo>>(response.Content);
        
        // Should not include restricted process windows
        var restrictedWindows = windows.Where(w => 
            restrictedProcesses.Any(p => p.Id == w.ProcessId)).ToList();
        Assert.AreEqual(0, restrictedWindows.Count, 
            "Should not enumerate windows from restricted processes");
        
        // Should handle access denied gracefully
        var errorLogs = await LogAnalyzer.GetSecurityErrorsAsync(TimeSpan.FromMinutes(1));
        Assert.IsTrue(errorLogs.All(log => log.Level <= LogLevel.Warning),
            "Security access denials should be handled at warning level or below");
    }

    [TestMethod]
    [TestCategory("PrivilegeEscalation")]
    public async Task CaptureOperations_PrivilegeEscalation_Prevented()
    {
        // Arrange: Attempt to capture system process windows
        var systemProcesses = Process.GetProcessesByName("winlogon")
            .Concat(Process.GetProcessesByName("csrss"))
            .Where(p => p.MainWindowHandle != IntPtr.Zero);

        // Act: Attempt to capture system process windows
        var captureResults = new List<McpToolResponse>();
        
        foreach (var process in systemProcesses)
        {
            var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
            {
                ToolName = "vs_capture_window",
                Parameters = new { window_handle = process.MainWindowHandle.ToInt64() }
            });
            
            captureResults.Add(response);
        }

        // Assert: Validate privilege escalation prevented
        Assert.IsTrue(captureResults.All(r => !r.IsSuccess), 
            "Should not be able to capture system process windows");
        
        Assert.IsTrue(captureResults.All(r => r.ErrorCode == "ACCESS_DENIED"), 
            "Should return ACCESS_DENIED for system processes");
    }
}
```

### 2. Memory Security Testing

#### Memory Protection Validation
```csharp
[TestClass]
[TestCategory("Security")]
[TestCategory("MemoryProtection")]
public class MemorySecurityTests
{
    [TestMethod]
    [TestCategory("MemoryIsolation")]
    public async Task CaptureOperations_MemoryIsolation_NoInformationLeakage()
    {
        // Arrange: Create sensitive content in separate process
        var sensitiveProcess = await SensitiveContentHelper.CreateProcessWithSensitiveDataAsync();
        
        // Perform multiple capture operations
        var captureResults = new List<SpecializedCapture>();
        
        for (int i = 0; i < 10; i++)
        {
            var response = await _mcpServer.InvokeToolAsync(new McpToolRequest
            {
                ToolName = "vs_capture_window",
                Parameters = new { window_handle = _testWindowHandle }
            });
            
            if (response.IsSuccess)
            {
                captureResults.Add(JsonConvert.DeserializeObject<SpecializedCapture>(response.Content));
            }
        }

        // Act: Analyze capture data for information leakage
        var memoryAnalysis = await MemorySecurityAnalyzer.AnalyzeCapturesAsync(captureResults);

        // Assert: Validate no sensitive information leaked
        Assert.IsFalse(memoryAnalysis.ContainsSensitiveData, 
            "Captures should not contain sensitive data from other processes");
        
        Assert.IsTrue(memoryAnalysis.MemoryIsolationScore >= 0.95, 
            "Memory isolation score should be high");
        
        Assert.IsEmpty(memoryAnalysis.SuspiciousPatterns, 
            "Should not detect suspicious memory patterns");
    }
}
```

### 3. COM Security Testing

#### COM Interop Security Validation
```csharp
[TestClass]
[TestCategory("Security")]
[TestCategory("ComSecurity")]
public class ComSecurityTests
{
    [TestMethod]
    [TestCategory("ComIsolation")]
    public async Task ComConnections_SecurityContext_ProperIsolation()
    {
        // Arrange: Monitor COM security context
        var securityMonitor = new ComSecurityMonitor();
        await securityMonitor.StartMonitoringAsync();

        // Act: Perform COM operations
        var comOperations = new[]
        {
            () => _mcpServer.InvokeToolAsync(new McpToolRequest { ToolName = "vs_list_windows" }),
            () => _mcpServer.InvokeToolAsync(new McpToolRequest { ToolName = "vs_capture_window", 
                Parameters = new { window_handle = _testWindowHandle } }),
            () => _mcpServer.InvokeToolAsync(new McpToolRequest { ToolName = "vs_capture_full_ide" })
        };

        foreach (var operation in comOperations)
        {
            await operation();
            await Task.Delay(100); // Allow security context changes to be detected
        }

        var securityReport = await securityMonitor.GetSecurityReportAsync();

        // Assert: Validate COM security
        Assert.IsTrue(securityReport.ProperSecurityContext, 
            "COM operations should maintain proper security context");
        
        Assert.IsEmpty(securityReport.SecurityViolations, 
            "Should not detect COM security violations");
        
        Assert.IsTrue(securityReport.ImpersonationLevel <= ImpersonationLevel.Identify, 
            "Should not use high impersonation levels");
    }
}
```

---

## 📊 Test Execution and Reporting

### Automated Test Execution

#### Continuous Integration Test Pipeline
```yaml
# Azure DevOps Pipeline for Integration Testing
trigger:
  branches:
    include:
      - main
      - feature/*

pool:
  vmImage: 'windows-2022'

variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

stages:
- stage: IntegrationTesting
  displayName: 'Integration Testing'
  jobs:
  - job: EndToEndTests
    displayName: 'End-to-End Integration Tests'
    steps:
    - task: UseDotNet@2
      inputs:
        version: '8.x'
        
    - task: VisualStudioTestPlatformInstaller@1
      inputs:
        packageFeedSelector: 'nugetOrg'
        versionSelector: 'latestStable'
        
    - task: VSTest@2
      displayName: 'Run End-to-End Tests'
      inputs:
        testSelector: 'testAssemblies'
        testAssemblyVer2: |
          **\*Integration*Tests.dll
          !**\*TestAdapter.dll
          !**\obj\**
        searchFolder: '$(System.DefaultWorkingDirectory)'
        testFiltercriteria: 'TestCategory=EndToEnd'
        runInParallel: false
        codeCoverageEnabled: true
        
  - job: PerformanceTests
    displayName: 'Performance Integration Tests'
    steps:
    - task: VSTest@2
      displayName: 'Run Performance Tests'
      inputs:
        testSelector: 'testAssemblies'
        testAssemblyVer2: |
          **\*Performance*Tests.dll
        testFiltercriteria: 'TestCategory=Performance'
        runInParallel: false
        
  - job: SecurityTests
    displayName: 'Security Integration Tests'
    steps:
    - task: VSTest@2
      displayName: 'Run Security Tests'
      inputs:
        testSelector: 'testAssemblies'
        testAssemblyVer2: |
          **\*Security*Tests.dll
        testFiltercriteria: 'TestCategory=Security'
        runInParallel: false
```

### Test Reporting and Analysis

#### Comprehensive Test Report Generation
```csharp
public class IntegrationTestReporter
{
    public async Task<TestReport> GenerateComprehensiveReportAsync(TestResults results)
    {
        var report = new TestReport
        {
            GeneratedAt = DateTime.UtcNow,
            TestSuiteName = "Phase 5 Integration Tests",
            OverallSummary = await GenerateOverallSummaryAsync(results),
            CategoryReports = new List<CategoryReport>()
        };

        // End-to-End Test Analysis
        report.CategoryReports.Add(await AnalyzeEndToEndTestsAsync(results.EndToEndResults));
        
        // Claude Code Integration Analysis
        report.CategoryReports.Add(await AnalyzeClaudeIntegrationAsync(results.ClaudeIntegrationResults));
        
        // Multi-Monitor Test Analysis
        report.CategoryReports.Add(await AnalyzeMultiMonitorTestsAsync(results.MultiMonitorResults));
        
        // Performance Test Analysis
        report.CategoryReports.Add(await AnalyzePerformanceTestsAsync(results.PerformanceResults));
        
        // Security Test Analysis
        report.CategoryReports.Add(await AnalyzeSecurityTestsAsync(results.SecurityResults));

        // Generate recommendations
        report.Recommendations = await GenerateRecommendationsAsync(results);
        
        return report;
    }

    private async Task<List<Recommendation>> GenerateRecommendationsAsync(TestResults results)
    {
        var recommendations = new List<Recommendation>();
        
        // Performance recommendations
        if (results.PerformanceResults.AverageLatency > 3000)
        {
            recommendations.Add(new Recommendation
            {
                Category = "Performance",
                Priority = Priority.High,
                Title = "Capture Latency Optimization",
                Description = "Average capture latency exceeds target threshold",
                SuggestedActions = new[]
                {
                    "Review memory management configuration",
                    "Consider parallel processing optimizations",
                    "Analyze graphics driver compatibility"
                }
            });
        }
        
        // Security recommendations
        if (results.SecurityResults.HasSecurityViolations)
        {
            recommendations.Add(new Recommendation
            {
                Category = "Security",
                Priority = Priority.Critical,
                Title = "Security Boundary Validation",
                Description = "Security violations detected during testing",
                SuggestedActions = new[]
                {
                    "Review process access validation logic",
                    "Strengthen COM security configuration",
                    "Implement additional privilege checks"
                }
            });
        }
        
        return recommendations;
    }
}
```

---

## 📚 Additional Resources

### Test Environment Setup
- [Integration Test Environment Setup Guide](../environment/integration-test-setup.md)
- [Multi-Monitor Test Configuration](../environment/multi-monitor-setup.md)
- [Claude Code Test Simulator](../tools/claude-simulator.md)

### Test Data and Utilities
- [Test Data Generation Tools](../tools/test-data-generation.md)
- [Visual Studio Test Automation Helper](../tools/vs-test-automation.md)
- [Performance Test Utilities](../tools/performance-testing.md)

### Continuous Integration
- [CI/CD Pipeline Configuration](../ci-cd/pipeline-setup.md)
- [Automated Test Execution](../ci-cd/test-automation.md)
- [Test Result Analysis](../ci-cd/result-analysis.md)

---

*This integration testing guide provides comprehensive coverage of all Phase 5 Advanced Visual Capture integration scenarios. Regular updates ensure alignment with evolving requirements and test methodologies.*