using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core;

/// <summary>
/// Core Visual Studio automation service interface.
/// </summary>
public interface IVisualStudioService
{
    /// <summary>
    /// Gets all currently running Visual Studio instances.
    /// </summary>
    /// <returns>Array of Visual Studio instances with metadata.</returns>
    Task<VisualStudioInstance[]> GetRunningInstancesAsync();

    /// <summary>
    /// Connects to a specific Visual Studio instance by process ID.
    /// </summary>
    /// <param name="processId">The process ID of the Visual Studio instance.</param>
    /// <returns>The connected Visual Studio instance.</returns>
    Task<VisualStudioInstance> ConnectToInstanceAsync(int processId);

    /// <summary>
    /// Opens a solution in the connected Visual Studio instance.
    /// </summary>
    /// <param name="solutionPath">The full path to the solution file.</param>
    /// <returns>Information about the opened solution.</returns>
    Task<SolutionInfo> OpenSolutionAsync(string solutionPath);

    /// <summary>
    /// Builds the currently open solution.
    /// </summary>
    /// <param name="configuration">The build configuration (Debug, Release, etc.).</param>
    /// <returns>The build result with success status and error information.</returns>
    Task<BuildResult> BuildSolutionAsync(string configuration = "Debug");

    /// <summary>
    /// Gets all projects in the currently open solution.
    /// </summary>
    /// <returns>Array of project information.</returns>
    Task<ProjectInfo[]> GetProjectsAsync();

    /// <summary>
    /// Executes a Visual Studio command.
    /// </summary>
    /// <param name="commandName">The name of the command to execute.</param>
    /// <param name="args">Optional command arguments.</param>
    Task ExecuteCommandAsync(string commandName, string args = "");

    /// <summary>
    /// Checks the health of a connection to a Visual Studio instance.
    /// </summary>
    /// <param name="processId">The process ID of the Visual Studio instance to check.</param>
    /// <returns>True if the connection is healthy, false otherwise.</returns>
    Task<bool> IsConnectionHealthyAsync(int processId);

    /// <summary>
    /// Gracefully disconnects from a Visual Studio instance.
    /// </summary>
    /// <param name="processId">The process ID of the Visual Studio instance to disconnect from.</param>
    Task DisconnectFromInstanceAsync(int processId);

    /// <summary>
    /// Gets available output window panes.
    /// </summary>
    /// <returns>Array of output pane names.</returns>
    Task<string[]> GetOutputPanesAsync();

    /// <summary>
    /// Gets content from a specific output window pane.
    /// </summary>
    /// <param name="paneName">The name of the output pane (e.g., "Build", "Debug", "General").</param>
    /// <returns>The current content of the output pane.</returns>
    Task<string> GetOutputPaneContentAsync(string paneName);

    /// <summary>
    /// Clears content from a specific output window pane.
    /// </summary>
    /// <param name="paneName">The name of the output pane to clear.</param>
    Task ClearOutputPaneAsync(string paneName);
}