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
            catch (Exception ex)
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
                            new { name = "vs_get_projects", description = "Get all projects in the currently open solution" }
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool: {ToolName}", toolName);
            return new { error = new { code = "TOOL_ERROR", message = ex.Message } };
        }
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

            if (processId <= 0)
            {
                return McpToolResult.CreateError(
                    "Invalid processId parameter",
                    "INVALID_PARAMETER",
                    "processId must be a positive integer");
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
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                return McpToolResult.CreateError(
                    "Invalid solutionPath parameter",
                    "INVALID_PARAMETER",
                    "solutionPath cannot be null or empty");
            }

            // Validate path format and security
            if (solutionPath.Contains("..") || solutionPath.Any(c => Path.GetInvalidPathChars().Contains(c)))
            {
                return McpToolResult.CreateError(
                    "Invalid solutionPath parameter",
                    "INVALID_PATH",
                    "solutionPath contains invalid characters or path traversal attempts");
            }

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

            // Validate configuration
            if (string.IsNullOrWhiteSpace(configuration))
            {
                configuration = "Debug";
            }

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
}