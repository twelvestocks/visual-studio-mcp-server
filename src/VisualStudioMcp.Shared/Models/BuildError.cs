namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents a build error.
/// </summary>
public class BuildError
{
    /// <summary>
    /// The file where the error occurred.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// The line number where the error occurred.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The column number where the error occurred.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// The error code (e.g., CS0103).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The project where the error occurred.
    /// </summary>
    public string Project { get; set; } = string.Empty;
}