using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;
using VisualStudioMcp.Debug;
using VisualStudioMcp.Imaging;
using VisualStudioMcp.Shared.Models;
using VisualStudioMcp.Xaml;
using System.Text.Json;

namespace VisualStudioMcp.Server;

public class VisualStudioMcpServer
{
    private readonly IVisualStudioService _vsService;
    private readonly IXamlDesignerService _xamlService;
    private readonly IDebugService _debugService;
    private readonly IImagingService _imagingService;
    private readonly IWindowClassificationService _windowClassification;
    private readonly ILogger<VisualStudioMcpServer> _logger;

    private readonly Dictionary<string, Func<JsonElement, Task<object>>> _tools = new();

    public VisualStudioMcpServer(
        IVisualStudioService vsService,
        IXamlDesignerService xamlService,
        IDebugService debugService,
        IImagingService imagingService,
        IWindowClassificationService windowClassification,
        ILogger<VisualStudioMcpServer> logger)
    {
        _vsService = vsService;
        _xamlService = xamlService;
        _debugService = debugService;
        _imagingService = imagingService;
        _windowClassification = windowClassification;
        _logger = logger;

        // Register MCP tools
        RegisterMcpTools();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Visual Studio MCP Server starting...");
        
        try
        {
            // Simple MCP protocol implementation using stdio
            await ProcessMcpRequests(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Visual Studio MCP Server");
            throw;
        }
    }

    private async Task ProcessMcpRequests(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server ready - waiting for requests...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var input = await Console.In.ReadLineAsync();
                if (input == null) break;

                var request = JsonSerializer.Deserialize<JsonElement>(input);
                var response = await ProcessRequest(request);
                
                var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                Console.WriteLine(responseJson);
            }
            catch (Exception ex) when (!(ex is SystemException || ex is OutOfMemoryException || ex is StackOverflowException))
            {
                _logger.LogError(ex, "Error processing MCP request");
                
                var errorResponse = new
                {
                    error = new
                    {
                        code = "INTERNAL_ERROR",
                        message = "Internal server error",
                        data = ex.Message
                    }
                };
                
                Console.WriteLine(JsonSerializer.Serialize(errorResponse));
            }
            // Let critical system exceptions terminate the process
        }
    }

    private async Task<object> ProcessRequest(JsonElement request)
    {
        if (!request.TryGetProperty("method", out var methodElement))
        {
            return new { error = new { code = "INVALID_REQUEST", message = "Missing method" } };
        }

        var method = methodElement.GetString();
        
        switch (method)
        {
            case "tools/list":
                return new
                {
                    result = new
                    {
                        tools = new[]
                        {
                            new { name = "vs_list_instances", description = "List all running Visual Studio instances" },
                            new { name = "vs_connect_instance", description = "Connect to a specific Visual Studio instance" },
                            new { name = "vs_open_solution", description = "Open a solution in the connected Visual Studio instance" },
                            new { name = "vs_build_solution", description = "Build the currently open solution" },
                            new { name = "vs_get_projects", description = "Get all projects in the currently open solution" },
                            new { name = "vs_start_debugging", description = "Start debugging the specified project or startup project" },
                            new { name = "vs_stop_debugging", description = "Stop the current debugging session" },
                            new { name = "vs_get_debug_state", description = "Get the current debugging state" },
                            new { name = "vs_set_breakpoint", description = "Set a breakpoint at the specified location" },
                            new { name = "vs_get_breakpoints", description = "Get all breakpoints in the current session" },
                            new { name = "vs_get_local_variables", description = "Get local variables in the current debugging context" },
                            new { name = "vs_get_call_stack", description = "Get the current call stack" },
                            new { name = "vs_step_debug", description = "Step through code execution (into, over, out)" },
                            new { name = "vs_evaluate_expression", description = "Evaluate an expression in the current debugging context" },
                            new { name = "vs_capture_window", description = "Capture a specific Visual Studio window with intelligent annotation" },
                            new { name = "vs_capture_full_ide", description = "Capture the complete Visual Studio IDE with comprehensive layout" },
                            new { name = "vs_analyse_visual_state", description = "Analyse and compare Visual Studio visual states" }
                        }
                    }
                };
                
            case "tools/call":
                if (!request.TryGetProperty("params", out var paramsElement) ||
                    !paramsElement.TryGetProperty("name", out var nameElement))
                {
                    return new { error = new { code = "INVALID_REQUEST", message = "Missing tool name" } };
                }
                
                var toolName = nameElement.GetString();
                var arguments = new JsonElement();
                
                if (paramsElement.TryGetProperty("arguments", out var argsElement))
                {
                    arguments = argsElement;
                }
                
                return await ExecuteTool(toolName, arguments);
                
            default:
                return new { error = new { code = "METHOD_NOT_FOUND", message = $"Unknown method: {method}" } };
        }
    }

    private void RegisterMcpTools()
    {
        _logger.LogDebug("Registering MCP tools...");

        _tools["vs_list_instances"] = HandleListInstancesAsync;
        _tools["vs_connect_instance"] = HandleConnectInstanceAsync;
        _tools["vs_open_solution"] = HandleOpenSolutionAsync;
        _tools["vs_build_solution"] = HandleBuildSolutionAsync;
        _tools["vs_get_projects"] = HandleGetProjectsAsync;
        
        // Debug tools
        _tools["vs_start_debugging"] = HandleStartDebuggingAsync;
        _tools["vs_stop_debugging"] = HandleStopDebuggingAsync;
        _tools["vs_get_debug_state"] = HandleGetDebugStateAsync;
        _tools["vs_set_breakpoint"] = HandleSetBreakpointAsync;
        _tools["vs_get_breakpoints"] = HandleGetBreakpointsAsync;
        _tools["vs_get_local_variables"] = HandleGetLocalVariablesAsync;
        _tools["vs_get_call_stack"] = HandleGetCallStackAsync;
        _tools["vs_step_debug"] = HandleStepDebugAsync;
        _tools["vs_evaluate_expression"] = HandleEvaluateExpressionAsync;
        
        // Visual capture tools
        _tools["vs_capture_window"] = HandleCaptureWindowAsync;
        _tools["vs_capture_full_ide"] = HandleCaptureFullIdeAsync;
        _tools["vs_analyse_visual_state"] = HandleAnalyseVisualStateAsync;

        _logger.LogInformation("Registered {Count} MCP tools", _tools.Count);
    }

    private async Task<object> ExecuteTool(string? toolName, JsonElement arguments)
    {
        if (string.IsNullOrEmpty(toolName) || !_tools.TryGetValue(toolName, out var handler))
        {
            return new { error = new { code = "TOOL_NOT_FOUND", message = $"Unknown tool: {toolName}" } };
        }

        try
        {
            var result = await handler(arguments);
            return new { result = result };
        }
        catch (Exception ex) when (!(ex is SystemException || ex is OutOfMemoryException || ex is StackOverflowException))
        {
            _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
            return new { error = new { code = "TOOL_ERROR", message = ex.Message } };
        }
        // Let critical system exceptions bubble up unhandled
    }

    private async Task<object> HandleListInstancesAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_list_instances tool");

            var instances = await _vsService.GetRunningInstancesAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                instances = instances,
                count = instances.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_list_instances tool");
            return McpToolResult.CreateError(
                "Failed to list Visual Studio instances",
                "VS_LIST_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleConnectInstanceAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_connect_instance tool");

            if (!arguments.TryGetProperty("processId", out var processIdElement) || 
                !processIdElement.TryGetInt32(out var processId))
            {
                return McpToolResult.CreateError(
                    "Invalid or missing processId parameter",
                    "INVALID_PARAMETER",
                    "processId must be a valid integer");
            }

            // Enhanced security validation
            var validationResult = InputValidationHelper.ValidateProcessId(processId, requireVisualStudio: true);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Process ID validation failed: {ErrorCode} - {ErrorMessage}", 
                    validationResult.ErrorCode, validationResult.ErrorMessage);
                return McpToolResult.CreateError(
                    validationResult.ErrorMessage!, 
                    validationResult.ErrorCode!, 
                    validationResult.ErrorDetails);
            }

            var instance = await _vsService.ConnectToInstanceAsync(processId);
            
            return McpToolResult.CreateSuccess(new
            {
                instance = instance,
                connected = true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_connect_instance tool");
            return McpToolResult.CreateError(
                "Failed to connect to Visual Studio instance",
                "VS_CONNECT_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleOpenSolutionAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_open_solution tool");

            if (!arguments.TryGetProperty("solutionPath", out var solutionPathElement))
            {
                return McpToolResult.CreateError(
                    "Missing solutionPath parameter",
                    "INVALID_PARAMETER",
                    "solutionPath is required");
            }

            var solutionPath = solutionPathElement.GetString();
            
            // Enhanced path validation and sanitization
            var pathValidation = InputValidationHelper.ValidateAndSanitizePath(solutionPath, ".sln");
            if (!pathValidation.IsValid)
            {
                _logger.LogWarning("Solution path validation failed: {ErrorCode} - {ErrorMessage}", 
                    pathValidation.ErrorCode, pathValidation.ErrorMessage);
                return McpToolResult.CreateError(
                    pathValidation.ErrorMessage!, 
                    pathValidation.ErrorCode!, 
                    pathValidation.ErrorDetails);
            }

            // Use the sanitized path
            solutionPath = (string)pathValidation.ValidatedValue!;

            var solutionInfo = await _vsService.OpenSolutionAsync(solutionPath);
            
            return McpToolResult.CreateSuccess(new
            {
                solution = solutionInfo,
                opened = true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_open_solution tool");
            return McpToolResult.CreateError(
                "Failed to open solution",
                "VS_OPEN_SOLUTION_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleBuildSolutionAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_build_solution tool");

            var configuration = "Debug"; // Default
            if (arguments.TryGetProperty("configuration", out var configElement))
            {
                configuration = configElement.GetString() ?? "Debug";
            }

            // Enhanced configuration validation
            var configValidation = InputValidationHelper.ValidateBuildConfiguration(configuration);
            if (!configValidation.IsValid)
            {
                _logger.LogWarning("Build configuration validation failed: {ErrorCode} - {ErrorMessage}", 
                    configValidation.ErrorCode, configValidation.ErrorMessage);
                return McpToolResult.CreateError(
                    configValidation.ErrorMessage!, 
                    configValidation.ErrorCode!, 
                    configValidation.ErrorDetails);
            }

            configuration = (string)configValidation.ValidatedValue!;

            var buildResult = await _vsService.BuildSolutionAsync(configuration);
            
            return McpToolResult.CreateSuccess(new
            {
                buildResult = buildResult,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_build_solution tool");
            return McpToolResult.CreateError(
                "Failed to build solution",
                "VS_BUILD_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleGetProjectsAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_get_projects tool");

            var projects = await _vsService.GetProjectsAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                projects = projects,
                count = projects.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_get_projects tool");
            return McpToolResult.CreateError(
                "Failed to get projects",
                "VS_GET_PROJECTS_FAILED",
                ex.Message);
        }
    }

    #region Debug Tool Handlers

    private async Task<object> HandleStartDebuggingAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_start_debugging tool");

            string? projectName = null;
            if (arguments.TryGetProperty("projectName", out var projectElement))
            {
                projectName = projectElement.GetString();
            }

            var debugState = await _debugService.StartDebuggingAsync(projectName);
            
            return McpToolResult.CreateSuccess(new
            {
                debugState = debugState,
                projectName = projectName ?? "startup project",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_start_debugging tool");
            return McpToolResult.CreateError(
                "Failed to start debugging",
                "DEBUG_START_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleStopDebuggingAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_stop_debugging tool");

            await _debugService.StopDebuggingAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                stopped = true,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_stop_debugging tool");
            return McpToolResult.CreateError(
                "Failed to stop debugging",
                "DEBUG_STOP_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleGetDebugStateAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_get_debug_state tool");

            var debugState = await _debugService.GetCurrentStateAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                debugState = debugState,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_get_debug_state tool");
            return McpToolResult.CreateError(
                "Failed to get debug state",
                "DEBUG_STATE_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleSetBreakpointAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_set_breakpoint tool");

            if (!arguments.TryGetProperty("file", out var fileElement))
            {
                return McpToolResult.CreateError(
                    "Missing file parameter",
                    "INVALID_PARAMETER",
                    "file is required");
            }

            if (!arguments.TryGetProperty("line", out var lineElement) || 
                !lineElement.TryGetInt32(out var line))
            {
                return McpToolResult.CreateError(
                    "Invalid or missing line parameter",
                    "INVALID_PARAMETER",
                    "line must be a valid integer");
            }

            var file = fileElement.GetString();
            if (string.IsNullOrWhiteSpace(file))
            {
                return McpToolResult.CreateError(
                    "Invalid file parameter",
                    "INVALID_PARAMETER",
                    "file cannot be null or empty");
            }

            string? condition = null;
            if (arguments.TryGetProperty("condition", out var conditionElement))
            {
                condition = conditionElement.GetString();
            }

            var breakpoint = await _debugService.AddBreakpointAsync(file, line, condition);
            
            return McpToolResult.CreateSuccess(new
            {
                breakpoint = breakpoint,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_set_breakpoint tool");
            return McpToolResult.CreateError(
                "Failed to set breakpoint",
                "DEBUG_BREAKPOINT_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleGetBreakpointsAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_get_breakpoints tool");

            var breakpoints = await _debugService.GetBreakpointsAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                breakpoints = breakpoints,
                count = breakpoints.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_get_breakpoints tool");
            return McpToolResult.CreateError(
                "Failed to get breakpoints",
                "DEBUG_BREAKPOINTS_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleGetLocalVariablesAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_get_local_variables tool");

            var variables = await _debugService.GetLocalVariablesAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                variables = variables,
                count = variables.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_get_local_variables tool");
            return McpToolResult.CreateError(
                "Failed to get local variables",
                "DEBUG_VARIABLES_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleGetCallStackAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_get_call_stack tool");

            var callStack = await _debugService.GetCallStackAsync();
            
            return McpToolResult.CreateSuccess(new
            {
                callStack = callStack,
                count = callStack.Length,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_get_call_stack tool");
            return McpToolResult.CreateError(
                "Failed to get call stack",
                "DEBUG_CALLSTACK_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleStepDebugAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_step_debug tool");

            // Get the step type (into, over, out)
            string stepType = "into"; // Default
            if (arguments.TryGetProperty("type", out var typeElement))
            {
                stepType = typeElement.GetString() ?? "into";
            }

            DebugState debugState;
            switch (stepType.ToLowerInvariant())
            {
                case "into":
                    debugState = await _debugService.StepIntoAsync();
                    break;
                case "over":
                    debugState = await _debugService.StepOverAsync();
                    break;
                case "out":
                    debugState = await _debugService.StepOutAsync();
                    break;
                default:
                    return McpToolResult.CreateError(
                        "Invalid step type",
                        "DEBUG_STEP_INVALID_TYPE",
                        $"Step type '{stepType}' is not valid. Valid options are: into, over, out");
            }

            return McpToolResult.CreateSuccess(new
            {
                debugState = debugState,
                stepType = stepType,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_step_debug tool");
            return McpToolResult.CreateError(
                "Failed to step debug",
                "DEBUG_STEP_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleEvaluateExpressionAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_evaluate_expression tool");

            // Get the expression to evaluate
            if (!arguments.TryGetProperty("expression", out var expressionElement))
            {
                return McpToolResult.CreateError(
                    "Missing expression parameter",
                    "DEBUG_EVALUATE_MISSING_EXPRESSION",
                    "The 'expression' parameter is required");
            }

            var expression = expressionElement.GetString();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return McpToolResult.CreateError(
                    "Invalid expression parameter",
                    "DEBUG_EVALUATE_INVALID_EXPRESSION",
                    "The 'expression' parameter cannot be null or empty");
            }

            var result = await _debugService.EvaluateExpressionAsync(expression);

            return McpToolResult.CreateSuccess(new
            {
                result = result,
                expression = expression,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_evaluate_expression tool");
            return McpToolResult.CreateError(
                "Failed to evaluate expression",
                "DEBUG_EVALUATE_FAILED",
                ex.Message);
        }
    }

    #endregion

    #region Visual Capture Tool Handlers

    private async Task<object> HandleCaptureWindowAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_capture_window tool");

            // Get window type parameter
            VisualStudioWindowType windowType = VisualStudioWindowType.Unknown;
            if (arguments.TryGetProperty("windowType", out var windowTypeElement))
            {
                if (Enum.TryParse<VisualStudioWindowType>(windowTypeElement.GetString(), ignoreCase: true, out var parsedType))
                {
                    windowType = parsedType;
                }
            }

            // Get optional window handle
            IntPtr? windowHandle = null;
            if (arguments.TryGetProperty("windowHandle", out var handleElement) && 
                handleElement.TryGetInt64(out var handleValue))
            {
                windowHandle = new IntPtr(handleValue);
            }

            // Get optional save path
            string? savePath = null;
            if (arguments.TryGetProperty("savePath", out var pathElement))
            {
                savePath = pathElement.GetString();
            }

            SpecializedCapture capture;

            // If specific window handle provided, use it
            if (windowHandle.HasValue && windowHandle.Value != IntPtr.Zero)
            {
                capture = await _imagingService.CaptureWindowWithAnnotationAsync(windowHandle.Value, windowType);
            }
            else
            {
                // Capture based on window type
                capture = windowType switch
                {
                    VisualStudioWindowType.SolutionExplorer => await _imagingService.CaptureSolutionExplorerAsync(),
                    VisualStudioWindowType.PropertiesWindow => await _imagingService.CapturePropertiesWindowAsync(),
                    VisualStudioWindowType.ErrorList => await _imagingService.CaptureErrorListAndOutputAsync(),
                    VisualStudioWindowType.CodeEditor => await _imagingService.CaptureCodeEditorAsync(),
                    _ => await CaptureGenericWindowTypeAsync(windowType)
                };
            }

            // Save to file if path provided
            if (!string.IsNullOrEmpty(savePath) && capture.ImageData.Length > 0)
            {
                await _imagingService.SaveCaptureAsync(capture, savePath);
            }

            return McpToolResult.CreateSuccess(new
            {
                capture = new
                {
                    windowType = capture.WindowType.ToString(),
                    width = capture.Width,
                    height = capture.Height,
                    annotationCount = capture.Annotations.Count,
                    hasImageData = capture.ImageData.Length > 0,
                    captureTime = capture.CaptureTime,
                    extractedText = capture.ExtractedText,
                    uiElementCount = capture.UiElements.Count,
                    metadata = capture.WindowMetadata
                },
                savedToFile = !string.IsNullOrEmpty(savePath),
                savePath = savePath,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_capture_window tool");
            return McpToolResult.CreateError(
                "Failed to capture window",
                "VISUAL_CAPTURE_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleCaptureFullIdeAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_capture_full_ide tool");

            // Get optional save path for composite image
            string? savePath = null;
            if (arguments.TryGetProperty("savePath", out var pathElement))
            {
                savePath = pathElement.GetString();
            }

            // Get optional flag to save individual windows
            bool saveIndividualWindows = false;
            if (arguments.TryGetProperty("saveIndividualWindows", out var saveIndividualElement))
            {
                saveIndividualWindows = saveIndividualElement.GetBoolean();
            }

            // Capture full IDE with layout
            var fullCapture = await _imagingService.CaptureFullIdeWithLayoutAsync();

            // Save composite image if path provided
            if (!string.IsNullOrEmpty(savePath) && fullCapture.CompositeImage.ImageData.Length > 0)
            {
                await _imagingService.SaveCaptureAsync(fullCapture.CompositeImage, savePath);
            }

            // Save individual windows if requested
            var individualSavePaths = new List<string>();
            if (saveIndividualWindows && !string.IsNullOrEmpty(savePath))
            {
                var basePath = Path.GetFileNameWithoutExtension(savePath);
                var extension = Path.GetExtension(savePath);
                var directory = Path.GetDirectoryName(savePath) ?? "";

                for (int i = 0; i < fullCapture.WindowCaptures.Count; i++)
                {
                    var windowCapture = fullCapture.WindowCaptures[i];
                    var individualPath = Path.Combine(directory, $"{basePath}_{windowCapture.WindowType}_{i}{extension}");
                    
                    if (windowCapture.ImageData.Length > 0)
                    {
                        await _imagingService.SaveCaptureAsync(windowCapture, individualPath);
                        individualSavePaths.Add(individualPath);
                    }
                }
            }

            return McpToolResult.CreateSuccess(new
            {
                fullCapture = new
                {
                    compositeImage = new
                    {
                        width = fullCapture.CompositeImage.Width,
                        height = fullCapture.CompositeImage.Height,
                        hasImageData = fullCapture.CompositeImage.ImageData.Length > 0
                    },
                    windowCaptures = fullCapture.WindowCaptures.Select(wc => new
                    {
                        windowType = wc.WindowType.ToString(),
                        width = wc.Width,
                        height = wc.Height,
                        annotationCount = wc.Annotations.Count,
                        hasImageData = wc.ImageData.Length > 0
                    }).ToArray(),
                    layout = new
                    {
                        windowCount = fullCapture.Layout.AllWindows.Count,
                        windowTypes = fullCapture.Layout.WindowsByType.Keys.Select(k => k.ToString()).ToArray(),
                        activeWindow = fullCapture.Layout.ActiveWindow?.WindowType.ToString(),
                        analysisTime = fullCapture.Layout.AnalysisTime
                    },
                    captureTime = fullCapture.CaptureTime,
                    ideMetadata = fullCapture.IdeMetadata
                },
                savedToFile = !string.IsNullOrEmpty(savePath),
                savePath = savePath,
                individualSavePaths = individualSavePaths,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_capture_full_ide tool");
            return McpToolResult.CreateError(
                "Failed to capture full IDE",
                "FULL_IDE_CAPTURE_FAILED",
                ex.Message);
        }
    }

    private async Task<object> HandleAnalyseVisualStateAsync(JsonElement arguments)
    {
        try
        {
            _logger.LogDebug("Executing vs_analyse_visual_state tool");

            // Get the current layout analysis
            var currentLayout = await _windowClassification.AnalyzeLayoutAsync();

            // Optional: compare with previous state if provided
            WindowLayout? previousLayout = null;
            if (arguments.TryGetProperty("previousStateFile", out var previousFileElement))
            {
                var previousFile = previousFileElement.GetString();
                if (!string.IsNullOrEmpty(previousFile) && File.Exists(previousFile))
                {
                    try
                    {
                        var jsonContent = await File.ReadAllTextAsync(previousFile);
                        previousLayout = JsonSerializer.Deserialize<WindowLayout>(jsonContent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load previous layout from file: {File}", previousFile);
                    }
                }
            }

            // Generate visual state analysis
            var analysis = GenerateVisualStateAnalysis(currentLayout, previousLayout);

            // Save current state if requested
            string? saveCurrentStatePath = null;
            if (arguments.TryGetProperty("saveCurrentState", out var saveStateElement))
            {
                saveCurrentStatePath = saveStateElement.GetString();
            }

            if (!string.IsNullOrEmpty(saveCurrentStatePath))
            {
                var currentStateJson = JsonSerializer.Serialize(currentLayout, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(saveCurrentStatePath, currentStateJson);
            }

            return McpToolResult.CreateSuccess(new
            {
                analysis = analysis,
                currentLayout = new
                {
                    windowCount = currentLayout.AllWindows.Count,
                    windowTypes = currentLayout.WindowsByType.Keys.Select(k => k.ToString()).ToArray(),
                    activeWindow = currentLayout.ActiveWindow?.WindowType.ToString(),
                    mainWindow = currentLayout.MainWindow != null ? new
                    {
                        title = currentLayout.MainWindow.Title,
                        bounds = currentLayout.MainWindow.Bounds
                    } : null,
                    dockingLayout = new
                    {
                        leftPanelCount = currentLayout.DockingLayout.LeftDockedPanels.Count,
                        rightPanelCount = currentLayout.DockingLayout.RightDockedPanels.Count,
                        topPanelCount = currentLayout.DockingLayout.TopDockedPanels.Count,
                        bottomPanelCount = currentLayout.DockingLayout.BottomDockedPanels.Count,
                        floatingPanelCount = currentLayout.DockingLayout.FloatingPanels.Count
                    },
                    analysisTime = currentLayout.AnalysisTime
                },
                savedCurrentState = !string.IsNullOrEmpty(saveCurrentStatePath),
                saveCurrentStatePath = saveCurrentStatePath,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in vs_analyse_visual_state tool");
            return McpToolResult.CreateError(
                "Failed to analyse visual state",
                "VISUAL_STATE_ANALYSIS_FAILED",
                ex.Message);
        }
    }

    private async Task<SpecializedCapture> CaptureGenericWindowTypeAsync(VisualStudioWindowType windowType)
    {
        // Find windows of the specified type and capture the first one
        var windows = await _windowClassification.FindWindowsByTypeAsync(windowType);
        var targetWindow = windows.FirstOrDefault();

        if (targetWindow != null)
        {
            return await _imagingService.CaptureWindowWithAnnotationAsync(targetWindow.Handle, windowType);
        }

        return new SpecializedCapture
        {
            ImageData = Array.Empty<byte>(),
            ImageFormat = "PNG",
            Width = 0,
            Height = 0,
            CaptureTime = DateTime.UtcNow,
            WindowType = windowType
        };
    }

    private object GenerateVisualStateAnalysis(WindowLayout currentLayout, WindowLayout? previousLayout)
    {
        var analysis = new
        {
            summary = new
            {
                totalWindows = currentLayout.AllWindows.Count,
                visibleWindowTypes = currentLayout.WindowsByType.Keys.Count,
                activeWindowType = currentLayout.ActiveWindow?.WindowType.ToString() ?? "None",
                layoutComplexity = CalculateLayoutComplexity(currentLayout)
            },
            windowDistribution = currentLayout.WindowsByType.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value.Count
            ),
            dockingAnalysis = new
            {
                totalDockedPanels = currentLayout.DockingLayout.LeftDockedPanels.Count +
                                  currentLayout.DockingLayout.RightDockedPanels.Count +
                                  currentLayout.DockingLayout.TopDockedPanels.Count +
                                  currentLayout.DockingLayout.BottomDockedPanels.Count,
                floatingPanels = currentLayout.DockingLayout.FloatingPanels.Count,
                dockingSides = new
                {
                    left = currentLayout.DockingLayout.LeftDockedPanels.Select(p => p.WindowType.ToString()).ToArray(),
                    right = currentLayout.DockingLayout.RightDockedPanels.Select(p => p.WindowType.ToString()).ToArray(),
                    top = currentLayout.DockingLayout.TopDockedPanels.Select(p => p.WindowType.ToString()).ToArray(),
                    bottom = currentLayout.DockingLayout.BottomDockedPanels.Select(p => p.WindowType.ToString()).ToArray()
                }
            },
            comparison = previousLayout != null ? GenerateLayoutComparison(currentLayout, previousLayout) : null
        };

        return analysis;
    }

    private int CalculateLayoutComplexity(WindowLayout layout)
    {
        // Simple complexity calculation based on window count and types
        var baseComplexity = layout.AllWindows.Count;
        var typeVariety = layout.WindowsByType.Keys.Count;
        var dockingComplexity = layout.DockingLayout.LeftDockedPanels.Count +
                               layout.DockingLayout.RightDockedPanels.Count +
                               layout.DockingLayout.TopDockedPanels.Count +
                               layout.DockingLayout.BottomDockedPanels.Count;

        return baseComplexity + (typeVariety * 2) + dockingComplexity;
    }

    private object GenerateLayoutComparison(WindowLayout current, WindowLayout previous)
    {
        var currentTypes = current.WindowsByType.Keys.ToHashSet();
        var previousTypes = previous.WindowsByType.Keys.ToHashSet();

        return new
        {
            windowCountChange = current.AllWindows.Count - previous.AllWindows.Count,
            newWindowTypes = currentTypes.Except(previousTypes).Select(t => t.ToString()).ToArray(),
            removedWindowTypes = previousTypes.Except(currentTypes).Select(t => t.ToString()).ToArray(),
            activeWindowChanged = current.ActiveWindow?.WindowType != previous.ActiveWindow?.WindowType,
            layoutStabilityScore = CalculateLayoutStability(current, previous)
        };
    }

    private double CalculateLayoutStability(WindowLayout current, WindowLayout previous)
    {
        // Simple stability calculation (0.0 = completely different, 1.0 = identical)
        var currentTypes = current.WindowsByType.Keys.ToHashSet();
        var previousTypes = previous.WindowsByType.Keys.ToHashSet();
        
        var intersection = currentTypes.Intersect(previousTypes).Count();
        var union = currentTypes.Union(previousTypes).Count();
        
        return union > 0 ? (double)intersection / union : 0.0;
    }

    #endregion
}