namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Represents a running Visual Studio instance.
/// </summary>
public class VisualStudioInstance
{
    /// <summary>
    /// The process ID of the Visual Studio instance.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// The version of Visual Studio (e.g., "17.8").
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The name of the currently open solution, or "No solution" if none is open.
    /// </summary>
    public string SolutionName { get; set; } = string.Empty;

    /// <summary>
    /// The time when Visual Studio was started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Whether this instance is currently connected.
    /// </summary>
    public bool IsConnected { get; set; }
}