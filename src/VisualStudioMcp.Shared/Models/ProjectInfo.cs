namespace VisualStudioMcp.Shared.Models;

/// <summary>
/// Information about a Visual Studio project.
/// </summary>
public class ProjectInfo
{
    /// <summary>
    /// The full path to the project file.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the project.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The project type (e.g., C#, VB.NET, etc.).
    /// </summary>
    public string ProjectType { get; set; } = string.Empty;

    /// <summary>
    /// The target framework(s) for this project.
    /// </summary>
    public string[] TargetFrameworks { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this project is currently loaded.
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Referenced projects.
    /// </summary>
    public string[] ProjectReferences { get; set; } = Array.Empty<string>();
}