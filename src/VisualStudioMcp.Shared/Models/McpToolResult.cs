namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents the result of an MCP tool execution.
/// </summary>
public class McpToolResult
{
    /// <summary>
    /// Whether the tool execution was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The result data from the tool execution.
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code for categorizing failures.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Additional context or details about the error.
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Creates a successful result with data.
    /// </summary>
    public static McpToolResult CreateSuccess(object? data = null)
    {
        return new McpToolResult
        {
            Success = true,
            Data = data
        };
    }

    /// <summary>
    /// Creates a failed result with error information.
    /// </summary>
    public static McpToolResult CreateError(string errorMessage, string? errorCode = null, string? errorDetails = null)
    {
        return new McpToolResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            ErrorDetails = errorDetails
        };
    }
}