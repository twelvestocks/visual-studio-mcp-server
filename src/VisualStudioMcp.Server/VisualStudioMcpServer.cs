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
    private readonly ILogger<VisualStudioMcpServer> _logger;

    private readonly Dictionary<string, Func<JsonElement, Task<object>>> _tools = new();

    public VisualStudioMcpServer(
        IVisualStudioService vsService,
        IXamlDesignerService xamlService,
        IDebugService debugService,
        IImagingService imagingService,
        ILogger<VisualStudioMcpServer> logger)
    {
        _vsService = vsService;
        _xamlService = xamlService;
        _debugService = debugService;
        _imagingService = imagingService;
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
                            new { name = "vs_get_call_stack", description = "Get the current call stack" }
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

    #endregion
}