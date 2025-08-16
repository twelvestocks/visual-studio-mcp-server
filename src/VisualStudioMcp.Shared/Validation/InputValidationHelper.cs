using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Shared.Validation;

/// <summary>
/// Comprehensive input validation and sanitization helper with detailed error reporting.
/// </summary>
public static class InputValidationHelper
{
    private static readonly Regex WindowsPathRegex = new(@"^[a-zA-Z]:[\\\/](?:[^<>:""|*?\r\n]+[\\\/])*[^<>:""|*?\r\n]*$", RegexOptions.Compiled);
    private static readonly Regex SafeFileNameRegex = new(@"^[a-zA-Z0-9._\-\s]+$", RegexOptions.Compiled);
    private static readonly string[] DangerousPathElements = { "..", "~", "$", "%", "&", "|", ";", "`", "<", ">", "\"", "'", "\\\\.\\" };
    private static readonly string[] AllowedExtensions = { ".sln", ".csproj", ".vbproj", ".cs", ".vb", ".xaml", ".xml", ".json", ".config" };
    private static readonly string[] DangerousCommands = { "cmd", "powershell", "bash", "sh", "bat", "com", "exe", "msi", "scr" };

    /// <summary>
    /// Validates a process ID with enhanced security and VS-specific checks.
    /// </summary>
    /// <param name="processId">The process ID to validate.</param>
    /// <param name="requireVisualStudio">Whether to verify the process is actually Visual Studio.</param>
    /// <returns>Detailed validation result with context.</returns>
    public static ValidationResult ValidateProcessId(int processId, bool requireVisualStudio = false)
    {
        var context = new Dictionary<string, object>
        {
            ["processId"] = processId,
            ["requireVisualStudio"] = requireVisualStudio,
            ["validationTime"] = DateTime.UtcNow
        };

        try
        {
            // Basic range validation
            if (processId <= 0)
            {
                return ValidationResult.CreateError(
                    "INVALID_PROCESS_ID_RANGE",
                    "Process ID must be a positive integer",
                    $"Process ID {processId} is not in valid range (1-{int.MaxValue})",
                    context);
            }

            if (processId > int.MaxValue - 1000) // Reserve some high values
            {
                return ValidationResult.CreateError(
                    "INVALID_PROCESS_ID_RANGE",
                    "Process ID is too large",
                    $"Process ID {processId} exceeds maximum safe value",
                    context);
            }

            // Check if process exists
            System.Diagnostics.Process? process = null;
            try
            {
                process = System.Diagnostics.Process.GetProcessById(processId);
                context["processName"] = process.ProcessName;
                context["processStartTime"] = process.StartTime;
                context["processFound"] = true;
            }
            catch (ArgumentException)
            {
                context["processFound"] = false;
                return ValidationResult.CreateError(
                    "PROCESS_NOT_FOUND",
                    "Process with specified ID does not exist",
                    $"No process found with ID {processId}. The process may have exited or never existed.",
                    context);
            }

            using (process)
            {
                // Check if process has exited
                if (process.HasExited)
                {
                    context["processExited"] = true;
                    context["processExitTime"] = process.ExitTime;
                    return ValidationResult.CreateError(
                        "PROCESS_EXITED",
                        "Process has already exited",
                        $"Process {processId} ({process.ProcessName}) exited at {process.ExitTime}",
                        context);
                }

                // Visual Studio specific validation
                if (requireVisualStudio)
                {
                    var isVisualStudio = IsVisualStudioProcess(process);
                    context["isVisualStudio"] = isVisualStudio;
                    
                    if (!isVisualStudio)
                    {
                        context["actualProcessName"] = process.ProcessName;
                        return ValidationResult.CreateError(
                            "NOT_VISUAL_STUDIO_PROCESS",
                            "Process is not a Visual Studio instance",
                            $"Process {processId} is '{process.ProcessName}', not Visual Studio (devenv.exe expected)",
                            context);
                    }

                    // Additional VS-specific checks
                    try
                    {
                        context["processTitle"] = process.MainWindowTitle;
                        context["hasMainWindow"] = !string.IsNullOrEmpty(process.MainWindowTitle);
                        
                        if (string.IsNullOrEmpty(process.MainWindowTitle))
                        {
                            return ValidationResult.CreateWarning(
                                "VS_NO_MAIN_WINDOW",
                                "Visual Studio process has no main window",
                                $"Visual Studio process {processId} may be starting up or shutting down",
                                context);
                        }
                    }
                    catch (Exception ex)
                    {
                        context["windowAccessError"] = ex.Message;
                        return ValidationResult.CreateWarning(
                            "VS_WINDOW_ACCESS_LIMITED",
                            "Cannot access Visual Studio window information",
                            $"Process {processId} exists but window access is limited: {ex.Message}",
                            context);
                    }
                }

                context["validatedProcessId"] = processId;
                return ValidationResult.CreateSuccess(processId, context);
            }
        }
        catch (Exception ex)
        {
            context["validationException"] = ex.Message;
            context["validationExceptionType"] = ex.GetType().Name;
            
            return ValidationResult.CreateError(
                "VALIDATION_EXCEPTION",
                "Unexpected error during process ID validation",
                $"Failed to validate process ID {processId}: {ex.Message}",
                context);
        }
    }

    /// <summary>
    /// Validates and sanitizes file paths with comprehensive security checks.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="expectedExtension">Expected file extension (optional).</param>
    /// <param name="mustExist">Whether the file must exist.</param>
    /// <param name="allowedDirectories">List of allowed base directories (optional).</param>
    /// <returns>Detailed validation result with sanitized path.</returns>
    public static ValidationResult ValidateAndSanitizePath(
        string? path, 
        string? expectedExtension = null, 
        bool mustExist = true,
        string[]? allowedDirectories = null)
    {
        var context = new Dictionary<string, object>
        {
            ["originalPath"] = path ?? "null",
            ["expectedExtension"] = expectedExtension ?? "any",
            ["mustExist"] = mustExist,
            ["allowedDirectories"] = allowedDirectories ?? Array.Empty<string>(),
            ["validationTime"] = DateTime.UtcNow
        };

        try
        {
            // Null/empty validation
            if (string.IsNullOrWhiteSpace(path))
            {
                return ValidationResult.CreateError(
                    "PATH_NULL_OR_EMPTY",
                    "Path cannot be null or empty",
                    "A valid file path is required",
                    context);
            }

            // Length validation
            if (path.Length > 260) // Windows MAX_PATH limitation
            {
                context["pathLength"] = path.Length;
                return ValidationResult.CreateError(
                    "PATH_TOO_LONG",
                    "Path exceeds maximum length",
                    $"Path length {path.Length} exceeds Windows limit of 260 characters",
                    context);
            }

            // Security validation - check for dangerous patterns
            foreach (var dangerous in DangerousPathElements)
            {
                if (path.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    context["dangerousElement"] = dangerous;
                    return ValidationResult.CreateError(
                        "PATH_SECURITY_VIOLATION",
                        "Path contains potentially dangerous elements",
                        $"Path contains '{dangerous}' which could indicate a path traversal attack",
                        context);
                }
            }

            // Basic format validation
            if (!WindowsPathRegex.IsMatch(path))
            {
                return ValidationResult.CreateError(
                    "PATH_INVALID_FORMAT",
                    "Path format is invalid",
                    $"Path '{path}' does not match expected Windows path format",
                    context);
            }

            // Normalize and sanitize the path
            string sanitizedPath;
            try
            {
                sanitizedPath = Path.GetFullPath(path);
                context["sanitizedPath"] = sanitizedPath;
            }
            catch (Exception ex)
            {
                context["sanitizationError"] = ex.Message;
                return ValidationResult.CreateError(
                    "PATH_SANITIZATION_FAILED",
                    "Failed to sanitize path",
                    $"Path sanitization failed: {ex.Message}",
                    context);
            }

            // Extension validation
            if (!string.IsNullOrEmpty(expectedExtension))
            {
                var actualExtension = Path.GetExtension(sanitizedPath);
                context["actualExtension"] = actualExtension;
                
                if (!actualExtension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return ValidationResult.CreateError(
                        "PATH_WRONG_EXTENSION",
                        "File has incorrect extension",
                        $"Expected '{expectedExtension}' but found '{actualExtension}'",
                        context);
                }
            }

            // Check if extension is in allowed list
            var fileExtension = Path.GetExtension(sanitizedPath);
            if (!string.IsNullOrEmpty(fileExtension) && 
                !AllowedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            {
                context["fileExtension"] = fileExtension;
                context["allowedExtensions"] = AllowedExtensions;
                return ValidationResult.CreateWarning(
                    "PATH_UNUSUAL_EXTENSION",
                    "File extension is not commonly used",
                    $"Extension '{fileExtension}' is not in the standard allowed list",
                    context);
            }

            // Directory restriction validation
            if (allowedDirectories != null && allowedDirectories.Length > 0)
            {
                var directory = Path.GetDirectoryName(sanitizedPath);
                context["fileDirectory"] = directory;
                
                bool isInAllowedDirectory = allowedDirectories.Any(allowed => 
                    sanitizedPath.StartsWith(Path.GetFullPath(allowed), StringComparison.OrdinalIgnoreCase));
                
                if (!isInAllowedDirectory)
                {
                    return ValidationResult.CreateError(
                        "PATH_DIRECTORY_RESTRICTED",
                        "File is not in an allowed directory",
                        $"Path '{sanitizedPath}' is not within allowed directories",
                        context);
                }
            }

            // Existence validation
            if (mustExist)
            {
                var exists = File.Exists(sanitizedPath);
                context["fileExists"] = exists;
                
                if (!exists)
                {
                    // Check if directory exists for better error message
                    var directory = Path.GetDirectoryName(sanitizedPath);
                    var directoryExists = Directory.Exists(directory);
                    context["directoryExists"] = directoryExists;
                    
                    var errorDetails = directoryExists 
                        ? $"Directory exists but file '{Path.GetFileName(sanitizedPath)}' not found"
                        : $"Directory '{directory}' does not exist";
                    
                    return ValidationResult.CreateError(
                        "PATH_FILE_NOT_FOUND",
                        "Specified file does not exist",
                        errorDetails,
                        context);
                }

                // Additional file checks
                try
                {
                    var fileInfo = new FileInfo(sanitizedPath);
                    context["fileSize"] = fileInfo.Length;
                    context["fileLastModified"] = fileInfo.LastWriteTime;
                    context["fileAttributes"] = fileInfo.Attributes.ToString();
                    
                    // Check for read access
                    using var stream = File.OpenRead(sanitizedPath);
                    context["fileReadable"] = true;
                }
                catch (UnauthorizedAccessException)
                {
                    return ValidationResult.CreateError(
                        "PATH_ACCESS_DENIED",
                        "Access denied to file",
                        $"Insufficient permissions to read file '{sanitizedPath}'",
                        context);
                }
                catch (Exception ex)
                {
                    context["fileAccessError"] = ex.Message;
                    return ValidationResult.CreateWarning(
                        "PATH_FILE_ACCESS_LIMITED",
                        "Limited access to file",
                        $"File exists but access is limited: {ex.Message}",
                        context);
                }
            }

            context["validatedPath"] = sanitizedPath;
            return ValidationResult.CreateSuccess(sanitizedPath, context);
        }
        catch (Exception ex)
        {
            context["validationException"] = ex.Message;
            context["validationExceptionType"] = ex.GetType().Name;
            
            return ValidationResult.CreateError(
                "PATH_VALIDATION_EXCEPTION",
                "Unexpected error during path validation",
                $"Failed to validate path: {ex.Message}",
                context);
        }
    }

    /// <summary>
    /// Validates build configuration with security and correctness checks.
    /// </summary>
    /// <param name="configuration">The build configuration to validate.</param>
    /// <returns>Detailed validation result with sanitized configuration.</returns>
    public static ValidationResult ValidateBuildConfiguration(string? configuration)
    {
        var context = new Dictionary<string, object>
        {
            ["originalConfiguration"] = configuration ?? "null",
            ["validationTime"] = DateTime.UtcNow
        };

        try
        {
            // Null/empty validation
            if (string.IsNullOrWhiteSpace(configuration))
            {
                context["defaultUsed"] = "Debug";
                return ValidationResult.CreateSuccess("Debug", context);
            }

            // Length validation
            if (configuration.Length > 50)
            {
                context["configurationLength"] = configuration.Length;
                return ValidationResult.CreateError(
                    "CONFIG_TOO_LONG",
                    "Configuration name is too long",
                    $"Configuration name length {configuration.Length} exceeds maximum of 50 characters",
                    context);
            }

            // Security validation - check for command injection
            foreach (var dangerous in DangerousCommands)
            {
                if (configuration.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    context["dangerousCommand"] = dangerous;
                    return ValidationResult.CreateError(
                        "CONFIG_SECURITY_VIOLATION",
                        "Configuration contains potentially dangerous content",
                        $"Configuration contains '{dangerous}' which could indicate command injection",
                        context);
                }
            }

            // Character validation
            if (!SafeFileNameRegex.IsMatch(configuration))
            {
                return ValidationResult.CreateError(
                    "CONFIG_INVALID_CHARACTERS",
                    "Configuration contains invalid characters",
                    $"Configuration '{configuration}' contains characters not allowed in build configurations",
                    context);
            }

            // Standard configuration validation
            var standardConfigs = new[] { "Debug", "Release", "Test", "Staging", "Production" };
            var sanitizedConfig = configuration.Trim();
            context["sanitizedConfiguration"] = sanitizedConfig;
            
            var isStandard = standardConfigs.Contains(sanitizedConfig, StringComparer.OrdinalIgnoreCase);
            context["isStandardConfiguration"] = isStandard;
            
            if (!isStandard)
            {
                context["standardConfigurations"] = standardConfigs;
                return ValidationResult.CreateWarning(
                    "CONFIG_NON_STANDARD",
                    "Configuration is not a standard build configuration",
                    $"'{sanitizedConfig}' is not a recognized standard configuration",
                    context);
            }

            // Normalize to proper case
            var normalizedConfig = standardConfigs.First(c => 
                c.Equals(sanitizedConfig, StringComparison.OrdinalIgnoreCase));
            
            context["normalizedConfiguration"] = normalizedConfig;
            return ValidationResult.CreateSuccess(normalizedConfig, context);
        }
        catch (Exception ex)
        {
            context["validationException"] = ex.Message;
            context["validationExceptionType"] = ex.GetType().Name;
            
            return ValidationResult.CreateError(
                "CONFIG_VALIDATION_EXCEPTION",
                "Unexpected error during configuration validation",
                $"Failed to validate configuration: {ex.Message}",
                context);
        }
    }

    /// <summary>
    /// Validates project or solution names with comprehensive checks.
    /// </summary>
    /// <param name="name">The name to validate.</param>
    /// <param name="allowEmpty">Whether empty names are allowed.</param>
    /// <returns>Detailed validation result.</returns>
    public static ValidationResult ValidateProjectName(string? name, bool allowEmpty = false)
    {
        var context = new Dictionary<string, object>
        {
            ["originalName"] = name ?? "null",
            ["allowEmpty"] = allowEmpty,
            ["validationTime"] = DateTime.UtcNow
        };

        try
        {
            // Null/empty validation
            if (string.IsNullOrWhiteSpace(name))
            {
                if (allowEmpty)
                {
                    return ValidationResult.CreateSuccess(string.Empty, context);
                }
                
                return ValidationResult.CreateError(
                    "PROJECT_NAME_REQUIRED",
                    "Project name cannot be null or empty",
                    "A valid project name is required",
                    context);
            }

            // Length validation
            if (name.Length > 100)
            {
                context["nameLength"] = name.Length;
                return ValidationResult.CreateError(
                    "PROJECT_NAME_TOO_LONG",
                    "Project name is too long",
                    $"Project name length {name.Length} exceeds maximum of 100 characters",
                    context);
            }

            // Character validation
            var invalidChars = Path.GetInvalidFileNameChars();
            var hasInvalidChars = name.Any(c => invalidChars.Contains(c));
            
            if (hasInvalidChars)
            {
                var foundInvalid = name.Where(c => invalidChars.Contains(c)).Distinct().ToArray();
                context["invalidCharacters"] = foundInvalid;
                return ValidationResult.CreateError(
                    "PROJECT_NAME_INVALID_CHARACTERS",
                    "Project name contains invalid characters",
                    $"Project name contains invalid characters: {string.Join(", ", foundInvalid)}",
                    context);
            }

            // Additional security checks
            if (name.StartsWith(".") || name.EndsWith("."))
            {
                return ValidationResult.CreateError(
                    "PROJECT_NAME_INVALID_FORMAT",
                    "Project name cannot start or end with a period",
                    $"Project name '{name}' has invalid format",
                    context);
            }

            var sanitizedName = name.Trim();
            context["sanitizedName"] = sanitizedName;
            return ValidationResult.CreateSuccess(sanitizedName, context);
        }
        catch (Exception ex)
        {
            context["validationException"] = ex.Message;
            context["validationExceptionType"] = ex.GetType().Name;
            
            return ValidationResult.CreateError(
                "PROJECT_NAME_VALIDATION_EXCEPTION",
                "Unexpected error during project name validation",
                $"Failed to validate project name: {ex.Message}",
                context);
        }
    }

    /// <summary>
    /// Validates debug expressions for safety and correctness.
    /// </summary>
    /// <param name="expression">The expression to validate.</param>
    /// <returns>Detailed validation result.</returns>
    public static ValidationResult ValidateDebugExpression(string? expression)
    {
        var context = new Dictionary<string, object>
        {
            ["originalExpression"] = expression ?? "null",
            ["validationTime"] = DateTime.UtcNow
        };

        try
        {
            // Null/empty validation
            if (string.IsNullOrWhiteSpace(expression))
            {
                return ValidationResult.CreateError(
                    "EXPRESSION_REQUIRED",
                    "Debug expression cannot be null or empty",
                    "A valid expression is required for evaluation",
                    context);
            }

            // Length validation
            if (expression.Length > 1000)
            {
                context["expressionLength"] = expression.Length;
                return ValidationResult.CreateError(
                    "EXPRESSION_TOO_LONG",
                    "Debug expression is too long",
                    $"Expression length {expression.Length} exceeds maximum of 1000 characters",
                    context);
            }

            // Security validation - check for dangerous patterns
            var dangerousPatterns = new[]
            {
                "System.Diagnostics.Process",
                "File.Delete",
                "Directory.Delete",
                "Registry.",
                "Environment.Exit",
                "Assembly.Load",
                "Activator.CreateInstance",
                "Marshal.",
                "unsafe"
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (expression.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    context["dangerousPattern"] = pattern;
                    return ValidationResult.CreateError(
                        "EXPRESSION_SECURITY_VIOLATION",
                        "Expression contains potentially dangerous operations",
                        $"Expression contains '{pattern}' which could be dangerous to evaluate",
                        context);
                }
            }

            // Basic syntax validation (simplified)
            var balancedParentheses = ValidateBalancedParentheses(expression);
            if (!balancedParentheses)
            {
                return ValidationResult.CreateError(
                    "EXPRESSION_SYNTAX_ERROR",
                    "Expression has unbalanced parentheses",
                    "Check that all parentheses are properly paired",
                    context);
            }

            var sanitizedExpression = expression.Trim();
            context["sanitizedExpression"] = sanitizedExpression;
            context["expressionComplexity"] = CalculateExpressionComplexity(sanitizedExpression);
            
            return ValidationResult.CreateSuccess(sanitizedExpression, context);
        }
        catch (Exception ex)
        {
            context["validationException"] = ex.Message;
            context["validationExceptionType"] = ex.GetType().Name;
            
            return ValidationResult.CreateError(
                "EXPRESSION_VALIDATION_EXCEPTION",
                "Unexpected error during expression validation",
                $"Failed to validate expression: {ex.Message}",
                context);
        }
    }

    /// <summary>
    /// Determines if a process is a Visual Studio instance.
    /// </summary>
    private static bool IsVisualStudioProcess(System.Diagnostics.Process process)
    {
        try
        {
            var processName = process.ProcessName.ToLowerInvariant();
            return processName == "devenv" || 
                   processName == "devenv.exe" ||
                   processName.StartsWith("microsoft.visualstudio");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that parentheses in an expression are balanced.
    /// </summary>
    private static bool ValidateBalancedParentheses(string expression)
    {
        var stack = new Stack<char>();
        var pairs = new Dictionary<char, char> { ['('] = ')', ['['] = ']', ['{'] = '}' };

        foreach (var ch in expression)
        {
            if (pairs.ContainsKey(ch))
            {
                stack.Push(ch);
            }
            else if (pairs.ContainsValue(ch))
            {
                if (stack.Count == 0)
                    return false;
                
                var last = stack.Pop();
                if (pairs[last] != ch)
                    return false;
            }
        }

        return stack.Count == 0;
    }

    /// <summary>
    /// Calculates a simple complexity score for debug expressions.
    /// </summary>
    private static int CalculateExpressionComplexity(string expression)
    {
        var complexity = 0;
        complexity += expression.Count(c => c == '(' || c == ')'); // Nesting
        complexity += expression.Count(c => c == '.'); // Member access
        complexity += expression.Split(new[] { "&&", "||" }, StringSplitOptions.None).Length - 1; // Logical operators
        complexity += expression.Split(new[] { "==", "!=", "<", ">", "<=", ">=" }, StringSplitOptions.None).Length - 1; // Comparisons
        
        return complexity;
    }
}