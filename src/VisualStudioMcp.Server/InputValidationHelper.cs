using System.Diagnostics;
using System.Security;

namespace VisualStudioMcp.Server;

/// <summary>
/// Provides comprehensive input validation for MCP tool parameters with security focus.
/// </summary>
public static class InputValidationHelper
{
    /// <summary>
    /// Validates a process ID ensuring it's within valid range and corresponds to an actual running process.
    /// </summary>
    /// <param name="processId">The process ID to validate</param>
    /// <param name="requireVisualStudio">If true, validates the process is actually Visual Studio</param>
    /// <returns>Validation result with error details if invalid</returns>
    public static ValidationResult ValidateProcessId(int processId, bool requireVisualStudio = true)
    {
        // Basic range validation - Windows process IDs are typically 0-65535
        if (processId <= 0)
        {
            return ValidationResult.Error(
                "INVALID_PROCESS_ID", 
                "Process ID must be a positive integer", 
                "processId must be greater than 0");
        }

        if (processId > 65535)
        {
            return ValidationResult.Error(
                "INVALID_PROCESS_ID", 
                "Process ID exceeds maximum valid range", 
                "Windows process IDs must be <= 65535");
        }

        // Verify the process actually exists
        try
        {
            using var process = Process.GetProcessById(processId);
            
            // If requireVisualStudio, verify it's actually a Visual Studio process
            if (requireVisualStudio)
            {
                var processName = process.ProcessName.ToLowerInvariant();
                if (!processName.Contains("devenv") && !processName.Contains("visualstudio"))
                {
                    return ValidationResult.Error(
                        "INVALID_PROCESS_TYPE",
                        "Specified process is not Visual Studio",
                        $"Process '{process.ProcessName}' (PID: {processId}) is not a Visual Studio instance");
                }
            }

            return ValidationResult.Success();
        }
        catch (ArgumentException)
        {
            return ValidationResult.Error(
                "PROCESS_NOT_FOUND",
                "No process found with the specified process ID",
                $"Process with ID {processId} is not running");
        }
        catch (Exception ex)
        {
            return ValidationResult.Error(
                "PROCESS_VALIDATION_FAILED",
                "Failed to validate process",
                $"Unable to validate process {processId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes and validates a file path for security, preventing path traversal and ensuring validity.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <param name="expectedExtension">Expected file extension (e.g., ".sln")</param>
    /// <returns>Validation result with sanitized path if valid</returns>
    public static ValidationResult ValidateAndSanitizePath(string? path, string? expectedExtension = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Error(
                "INVALID_PATH",
                "Path cannot be null or empty",
                "A valid file path is required");
        }

        try
        {
            // Normalize the path and resolve relative references
            var fullPath = Path.GetFullPath(path);
            
            // Security check: Ensure the path doesn't contain traversal attempts
            if (path.Contains("..") || path.Contains("~"))
            {
                return ValidationResult.Error(
                    "PATH_TRAVERSAL_DETECTED",
                    "Path contains potential traversal attempts",
                    "Paths cannot contain '..' or '~' references for security");
            }

            // Validate path characters
            var invalidChars = Path.GetInvalidPathChars();
            if (path.Any(c => invalidChars.Contains(c)))
            {
                return ValidationResult.Error(
                    "INVALID_PATH_CHARACTERS",
                    "Path contains invalid characters",
                    "Path contains characters not allowed in file paths");
            }

            // Check if path is rooted (absolute)
            if (!Path.IsPathRooted(fullPath))
            {
                return ValidationResult.Error(
                    "RELATIVE_PATH_NOT_ALLOWED",
                    "Relative paths are not allowed for security",
                    "Only absolute paths are permitted");
            }

            // Validate file extension if specified
            if (!string.IsNullOrEmpty(expectedExtension))
            {
                var actualExtension = Path.GetExtension(fullPath);
                if (!string.Equals(actualExtension, expectedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.Error(
                        "INVALID_FILE_EXTENSION",
                        $"File must have {expectedExtension} extension",
                        $"Expected {expectedExtension} but got {actualExtension}");
                }
            }

            // Security: Ensure the file exists (prevents information disclosure attacks)
            if (!File.Exists(fullPath))
            {
                return ValidationResult.Error(
                    "FILE_NOT_FOUND",
                    "Specified file does not exist",
                    $"File not found: {fullPath}");
            }

            return ValidationResult.Success(fullPath);
        }
        catch (SecurityException ex)
        {
            return ValidationResult.Error(
                "SECURITY_VIOLATION",
                "Path access denied for security reasons",
                $"Security error: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ValidationResult.Error(
                "ACCESS_DENIED",
                "Access denied to specified path",
                $"Access denied: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Error(
                "PATH_VALIDATION_FAILED",
                "Failed to validate path",
                $"Path validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates build configuration parameter.
    /// </summary>
    /// <param name="configuration">The configuration to validate</param>
    /// <returns>Validation result with sanitized configuration if valid</returns>
    public static ValidationResult ValidateBuildConfiguration(string? configuration)
    {
        // Allow null/empty - will default to "Debug"
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return ValidationResult.Success("Debug");
        }

        // Whitelist of allowed configurations
        var allowedConfigurations = new[] { "Debug", "Release", "DebugAnyCPU", "ReleaseAnyCPU" };
        
        if (allowedConfigurations.Any(c => string.Equals(c, configuration, StringComparison.OrdinalIgnoreCase)))
        {
            // Return properly cased version
            var validConfig = allowedConfigurations.First(c => 
                string.Equals(c, configuration, StringComparison.OrdinalIgnoreCase));
            return ValidationResult.Success(validConfig);
        }

        return ValidationResult.Error(
            "INVALID_CONFIGURATION",
            "Build configuration is not valid",
            $"Configuration '{configuration}' is not allowed. Valid options: {string.Join(", ", allowedConfigurations)}");
    }
}

/// <summary>
/// Represents the result of input validation.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? ErrorDetails { get; private set; }
    public object? ValidatedValue { get; private set; }

    private ValidationResult(bool isValid, string? errorCode = null, string? errorMessage = null, 
        string? errorDetails = null, object? validatedValue = null)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ErrorDetails = errorDetails;
        ValidatedValue = validatedValue;
    }

    public static ValidationResult Success(object? validatedValue = null)
    {
        return new ValidationResult(true, validatedValue: validatedValue);
    }

    public static ValidationResult Error(string errorCode, string errorMessage, string? errorDetails = null)
    {
        return new ValidationResult(false, errorCode, errorMessage, errorDetails);
    }

    /// <summary>
    /// Converts validation result to McpToolResult error format.
    /// </summary>
    public object ToMcpError()
    {
        if (IsValid)
            throw new InvalidOperationException("Cannot convert successful validation to error");

        return new
        {
            error = new
            {
                code = ErrorCode,
                message = ErrorMessage,
                data = ErrorDetails
            }
        };
    }
}