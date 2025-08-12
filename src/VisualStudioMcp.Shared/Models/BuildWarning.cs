namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents a build warning.
/// </summary>
public class BuildWarning
{
    /// <summary>
    /// The file where the warning occurred.
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// The line number where the warning occurred.
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// The column number where the warning occurred.
    /// </summary>
    public int Column { get; set; }

    /// <summary>
    /// The warning code (e.g., CS0168).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// The warning message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The project where the warning occurred.
    /// </summary>
    public string Project { get; set; } = string.Empty;
}