namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Result of a Visual Studio build operation.
/// </summary>
public class BuildResult
{
    /// <summary>
    /// Whether the build was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The build output text.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Build errors that occurred.
    /// </summary>
    public BuildError[] Errors { get; set; } = Array.Empty<BuildError>();

    /// <summary>
    /// Build warnings that occurred.
    /// </summary>
    public BuildWarning[] Warnings { get; set; } = Array.Empty<BuildWarning>();

    /// <summary>
    /// The duration of the build operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// The build configuration used.
    /// </summary>
    public string Configuration { get; set; } = string.Empty;

    /// <summary>
    /// The number of errors that occurred.
    /// </summary>
    public int ErrorCount => Errors.Length;

    /// <summary>
    /// The number of warnings that occurred.
    /// </summary>
    public int WarningCount => Warnings.Length;
}