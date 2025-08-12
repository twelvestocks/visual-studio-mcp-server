namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Information about a Visual Studio solution.
/// </summary>
public class SolutionInfo
{
    /// <summary>
    /// The full path to the solution file.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the solution.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// All projects contained in this solution.
    /// </summary>
    public ProjectInfo[] Projects { get; set; } = Array.Empty<ProjectInfo>();

    /// <summary>
    /// Whether the solution is currently open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// The number of projects in the solution.
    /// </summary>
    public int ProjectCount => Projects.Length;
}