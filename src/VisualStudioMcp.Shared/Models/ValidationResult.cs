namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents the result of an input validation operation with detailed context.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets whether the validation passed with warnings.
    /// </summary>
    public bool HasWarnings { get; init; }

    /// <summary>
    /// Gets the validation error code (null if successful).
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the user-friendly error message (null if successful).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets detailed error information for debugging.
    /// </summary>
    public string? ErrorDetails { get; init; }

    /// <summary>
    /// Gets the validated and potentially sanitized value.
    /// </summary>
    public object? ValidatedValue { get; init; }

    /// <summary>
    /// Gets additional context information about the validation.
    /// </summary>
    public Dictionary<string, object> Context { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when validation occurred.
    /// </summary>
    public DateTime ValidationTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets suggested recovery actions (if applicable).
    /// </summary>
    public string[]? SuggestedActions { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="validatedValue">The validated value.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult CreateSuccess(object validatedValue, Dictionary<string, object>? context = null)
    {
        return new ValidationResult
        {
            IsValid = true,
            HasWarnings = false,
            ValidatedValue = validatedValue,
            Context = context ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a validation result with warnings.
    /// </summary>
    /// <param name="warningCode">The warning code.</param>
    /// <param name="warningMessage">The warning message.</param>
    /// <param name="warningDetails">Detailed warning information.</param>
    /// <param name="validatedValue">The validated value (if partially successful).</param>
    /// <param name="context">Additional context information.</param>
    /// <param name="suggestedActions">Suggested actions to address the warning.</param>
    /// <returns>A validation result with warnings.</returns>
    public static ValidationResult CreateWarning(
        string warningCode,
        string warningMessage,
        string? warningDetails = null,
        object? validatedValue = null,
        Dictionary<string, object>? context = null,
        string[]? suggestedActions = null)
    {
        return new ValidationResult
        {
            IsValid = true,
            HasWarnings = true,
            ErrorCode = warningCode,
            ErrorMessage = warningMessage,
            ErrorDetails = warningDetails,
            ValidatedValue = validatedValue,
            Context = context ?? new Dictionary<string, object>(),
            SuggestedActions = suggestedActions
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="errorMessage">The user-friendly error message.</param>
    /// <param name="errorDetails">Detailed error information for debugging.</param>
    /// <param name="context">Additional context information.</param>
    /// <param name="suggestedActions">Suggested actions to fix the error.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult CreateError(
        string errorCode,
        string errorMessage,
        string? errorDetails = null,
        Dictionary<string, object>? context = null,
        string[]? suggestedActions = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            HasWarnings = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            ErrorDetails = errorDetails,
            Context = context ?? new Dictionary<string, object>(),
            SuggestedActions = suggestedActions
        };
    }

    /// <summary>
    /// Creates a validation result from an exception.
    /// </summary>
    /// <param name="ex">The exception that occurred during validation.</param>
    /// <param name="operation">The operation that was being performed.</param>
    /// <param name="context">Additional context information.</param>
    /// <returns>A failed validation result based on the exception.</returns>
    public static ValidationResult CreateFromException(Exception ex, string operation, Dictionary<string, object>? context = null)
    {
        var resultContext = context ?? new Dictionary<string, object>();
        resultContext["exception"] = ex.Message;
        resultContext["exceptionType"] = ex.GetType().Name;
        resultContext["stackTrace"] = ex.StackTrace;
        resultContext["operation"] = operation;

        return new ValidationResult
        {
            IsValid = false,
            HasWarnings = false,
            ErrorCode = "VALIDATION_EXCEPTION",
            ErrorMessage = $"Validation failed due to unexpected error in {operation}",
            ErrorDetails = ex.Message,
            Context = resultContext,
            SuggestedActions = new[]
            {
                "Check the input value format",
                "Verify system permissions",
                "Review system logs for additional details",
                "Contact support if the issue persists"
            }
        };
    }

    /// <summary>
    /// Returns a string representation of the validation result.
    /// </summary>
    public override string ToString()
    {
        if (IsValid && !HasWarnings)
        {
            return $"Validation successful: {ValidatedValue}";
        }
        
        if (IsValid && HasWarnings)
        {
            return $"Validation successful with warnings: {ErrorCode} - {ErrorMessage}";
        }
        
        return $"Validation failed: {ErrorCode} - {ErrorMessage}";
    }
}