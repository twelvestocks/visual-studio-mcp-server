namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents the result of an MCP tool execution with comprehensive diagnostic information.
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
    /// Unique correlation ID for tracking this operation across components.
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp when the operation was executed.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of the operation execution.
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Name of the tool that was executed.
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Category of the error for better classification.
    /// </summary>
    public ErrorCategory ErrorCategory { get; set; } = ErrorCategory.Unknown;

    /// <summary>
    /// Severity level of the error.
    /// </summary>
    public ErrorSeverity ErrorSeverity { get; set; } = ErrorSeverity.Error;

    /// <summary>
    /// User-friendly error message suitable for display.
    /// </summary>
    public string? UserFriendlyMessage { get; set; }

    /// <summary>
    /// Suggested recovery actions for the user.
    /// </summary>
    public string[]? SuggestedActions { get; set; }

    /// <summary>
    /// Additional context information about the operation.
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Environment diagnostic information captured during the operation.
    /// </summary>
    public EnvironmentDiagnostics? Environment { get; set; }

    /// <summary>
    /// Visual Studio specific diagnostic information.
    /// </summary>
    public VisualStudioDiagnostics? VisualStudioState { get; set; }

    /// <summary>
    /// Performance metrics for the operation.
    /// </summary>
    public PerformanceMetrics? Performance { get; set; }

    /// <summary>
    /// Related operations or dependencies that were involved.
    /// </summary>
    public string[]? RelatedOperations { get; set; }

    /// <summary>
    /// Whether this operation can be retried safely.
    /// </summary>
    public bool IsRetryable { get; set; }

    /// <summary>
    /// Number of retry attempts made (if any).
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Creates a successful result with data and optional context.
    /// </summary>
    /// <param name="data">The result data.</param>
    /// <param name="toolName">Name of the tool that executed.</param>
    /// <param name="context">Additional context information.</param>
    /// <param name="performance">Performance metrics.</param>
    /// <returns>A successful MCP tool result.</returns>
    public static McpToolResult CreateSuccess(
        object? data = null, 
        string? toolName = null,
        Dictionary<string, object>? context = null,
        PerformanceMetrics? performance = null)
    {
        return new McpToolResult
        {
            Success = true,
            Data = data,
            ToolName = toolName,
            Context = context ?? new Dictionary<string, object>(),
            Performance = performance,
            ErrorSeverity = ErrorSeverity.None
        };
    }

    /// <summary>
    /// Creates a failed result with comprehensive error information.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="errorCode">The error code for categorization.</param>
    /// <param name="errorDetails">Additional error details for debugging.</param>
    /// <param name="toolName">Name of the tool that failed.</param>
    /// <param name="category">Error category.</param>
    /// <param name="severity">Error severity level.</param>
    /// <param name="context">Additional context information.</param>
    /// <param name="suggestedActions">Suggested recovery actions.</param>
    /// <param name="isRetryable">Whether the operation can be retried.</param>
    /// <returns>A failed MCP tool result with comprehensive diagnostics.</returns>
    public static McpToolResult CreateError(
        string errorMessage, 
        string? errorCode = null, 
        string? errorDetails = null,
        string? toolName = null,
        ErrorCategory category = ErrorCategory.Unknown,
        ErrorSeverity severity = ErrorSeverity.Error,
        Dictionary<string, object>? context = null,
        string[]? suggestedActions = null,
        bool isRetryable = false)
    {
        return new McpToolResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode,
            ErrorDetails = errorDetails,
            ToolName = toolName,
            ErrorCategory = category,
            ErrorSeverity = severity,
            Context = context ?? new Dictionary<string, object>(),
            SuggestedActions = suggestedActions,
            IsRetryable = isRetryable,
            UserFriendlyMessage = GenerateUserFriendlyMessage(errorMessage, category)
        };
    }

    /// <summary>
    /// Creates a result from an exception with full diagnostic context.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="toolName">Name of the tool that failed.</param>
    /// <param name="operation">The operation that was being performed.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>A failed MCP tool result based on the exception.</returns>
    public static McpToolResult CreateFromException(
        Exception exception,
        string? toolName = null,
        string? operation = null,
        Dictionary<string, object>? context = null)
    {
        var resultContext = context ?? new Dictionary<string, object>();
        resultContext["exception"] = exception.Message;
        resultContext["exceptionType"] = exception.GetType().Name;
        resultContext["stackTrace"] = exception.StackTrace;
        
        if (!string.IsNullOrEmpty(operation))
        {
            resultContext["operation"] = operation;
        }

        var category = ClassifyException(exception);
        var isRetryable = IsRetryableException(exception);

        return new McpToolResult
        {
            Success = false,
            ErrorMessage = $"Tool execution failed: {exception.Message}",
            ErrorCode = "TOOL_EXECUTION_EXCEPTION",
            ErrorDetails = exception.ToString(),
            ToolName = toolName,
            ErrorCategory = category,
            ErrorSeverity = ErrorSeverity.Error,
            Context = resultContext,
            IsRetryable = isRetryable,
            SuggestedActions = GenerateSuggestedActions(exception, category),
            UserFriendlyMessage = GenerateUserFriendlyMessage(exception.Message, category)
        };
    }

    /// <summary>
    /// Adds environment diagnostic information to the result.
    /// </summary>
    /// <param name="environment">Environment diagnostics to add.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    public McpToolResult WithEnvironmentDiagnostics(EnvironmentDiagnostics environment)
    {
        Environment = environment;
        return this;
    }

    /// <summary>
    /// Adds Visual Studio diagnostic information to the result.
    /// </summary>
    /// <param name="vsState">Visual Studio diagnostics to add.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    public McpToolResult WithVisualStudioDiagnostics(VisualStudioDiagnostics vsState)
    {
        VisualStudioState = vsState;
        return this;
    }

    /// <summary>
    /// Adds performance metrics to the result.
    /// </summary>
    /// <param name="performance">Performance metrics to add.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    public McpToolResult WithPerformanceMetrics(PerformanceMetrics performance)
    {
        Performance = performance;
        return this;
    }

    /// <summary>
    /// Adds or updates context information.
    /// </summary>
    /// <param name="key">Context key.</param>
    /// <param name="value">Context value.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    public McpToolResult WithContext(string key, object value)
    {
        Context[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the operation duration.
    /// </summary>
    /// <param name="duration">Duration of the operation.</param>
    /// <returns>This result instance for fluent chaining.</returns>
    public McpToolResult WithDuration(TimeSpan duration)
    {
        Duration = duration;
        return this;
    }

    /// <summary>
    /// Generates a user-friendly error message based on the category.
    /// </summary>
    private static string GenerateUserFriendlyMessage(string originalMessage, ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Connection => "Unable to connect to Visual Studio. Please ensure Visual Studio is running and accessible.",
            ErrorCategory.Authentication => "Access denied. Please check permissions and try again.",
            ErrorCategory.Validation => "Invalid input provided. Please check your parameters and try again.",
            ErrorCategory.FileSystem => "File or directory operation failed. Please check the path and permissions.",
            ErrorCategory.Memory => "System is low on memory. Please close unnecessary applications and try again.",
            ErrorCategory.Network => "Network operation failed. Please check your connection and try again.",
            ErrorCategory.Timeout => "Operation timed out. The system may be busy, please try again later.",
            ErrorCategory.Configuration => "Configuration error detected. Please check your settings.",
            _ => originalMessage
        };
    }

    /// <summary>
    /// Classifies an exception into an appropriate error category.
    /// </summary>
    private static ErrorCategory ClassifyException(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => ErrorCategory.Authentication,
            DirectoryNotFoundException or FileNotFoundException => ErrorCategory.FileSystem,
            TimeoutException => ErrorCategory.Timeout,
            OutOfMemoryException => ErrorCategory.Memory,
            ArgumentException or ArgumentNullException => ErrorCategory.Validation,
            System.Runtime.InteropServices.COMException => ErrorCategory.Connection,
            System.Net.NetworkInformation.NetworkInformationException => ErrorCategory.Network,
            InvalidOperationException when exception.Message.Contains("Visual Studio") => ErrorCategory.Connection,
            _ => ErrorCategory.Unknown
        };
    }

    /// <summary>
    /// Determines if an exception represents a retryable condition.
    /// </summary>
    private static bool IsRetryableException(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            System.Runtime.InteropServices.COMException comEx => IsRetryableComError(comEx.HResult),
            InvalidOperationException when exception.Message.Contains("busy") => true,
            OutOfMemoryException => true,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a COM HRESULT represents a retryable error.
    /// </summary>
    private static bool IsRetryableComError(int hresult)
    {
        return hresult switch
        {
            unchecked((int)0x800401E3) => true, // MK_E_UNAVAILABLE
            unchecked((int)0x80010001) => true, // RPC_E_CALL_REJECTED
            unchecked((int)0x80010105) => true, // RPC_E_SERVERCALL_RETRYLATER
            unchecked((int)0x8001010A) => true, // RPC_E_SERVERCALL_REJECTED
            _ => false
        };
    }

    /// <summary>
    /// Generates suggested actions based on the exception and category.
    /// </summary>
    private static string[] GenerateSuggestedActions(Exception exception, ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Connection => new[]
            {
                "Ensure Visual Studio is running",
                "Check that Visual Studio is not busy with another operation",
                "Restart Visual Studio if the problem persists",
                "Verify that Visual Studio extensions are not interfering"
            },
            ErrorCategory.Authentication => new[]
            {
                "Run as administrator if required",
                "Check file and directory permissions",
                "Verify user account has necessary privileges"
            },
            ErrorCategory.Validation => new[]
            {
                "Check input parameter formats",
                "Verify file paths are correct and accessible",
                "Ensure required parameters are provided"
            },
            ErrorCategory.FileSystem => new[]
            {
                "Verify the file or directory exists",
                "Check file permissions and access rights",
                "Ensure the path is not too long (Windows limit: 260 characters)"
            },
            ErrorCategory.Memory => new[]
            {
                "Close unnecessary applications to free memory",
                "Restart Visual Studio to clear memory usage",
                "Check for memory leaks in COM operations"
            },
            ErrorCategory.Timeout => new[]
            {
                "Wait for Visual Studio to complete current operations",
                "Try the operation again with a longer timeout",
                "Check system performance and resource usage"
            },
            _ => new[]
            {
                "Review the error details for specific guidance",
                "Check system logs for additional information",
                "Contact support if the issue persists"
            }
        };
    }
}

/// <summary>
/// Error categories for better classification and handling.
/// </summary>
public enum ErrorCategory
{
    Unknown,
    Connection,
    Authentication,
    Validation,
    FileSystem,
    Memory,
    Network,
    Timeout,
    Configuration,
    Hardware,
    UserInterface
}

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    None,
    Information,
    Warning,
    Error,
    Critical
}