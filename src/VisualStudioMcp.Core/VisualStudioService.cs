using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using EnvDTE80;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core;

/// <summary>
/// Implementation of Visual Studio automation service using COM interop.
/// </summary>
public class VisualStudioService : IVisualStudioService
{
    private readonly ILogger<VisualStudioService> _logger;
    private readonly Dictionary<int, DTE> _connectedInstances = new();
    private readonly Dictionary<int, DateTime> _lastHealthCheck = new();
    private readonly System.Threading.Timer _healthCheckTimer;

    // COM interop declarations
    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    /// <summary>
    /// Initializes a new instance of the VisualStudioService class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostics.</param>
    public VisualStudioService(ILogger<VisualStudioService> logger)
    {
        _logger = logger;
        
        // Start health check timer - check every 30 seconds
        _healthCheckTimer = new System.Threading.Timer(PerformHealthChecks, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Gets all currently running Visual Studio instances.
    /// </summary>
    /// <returns>An array of VisualStudioInstance objects representing running instances.</returns>
    public async Task<VisualStudioInstance[]> GetRunningInstancesAsync()
    {
        _logger.LogInformation("Getting running Visual Studio instances...");

        return await Task.Run(() =>
        {
            return ComInteropHelper.WithComObject(
                () => {
                    var hr = GetRunningObjectTable(0, out var rot);
                    if (hr != 0)
                    {
                        throw new COMException($"Failed to get Running Object Table. HRESULT: {hr:X}");
                    }
                    return rot;
                },
                rot => EnumerateVisualStudioInstances(rot),
                _logger,
                "GetRunningVisualStudioInstances");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Enumerates Visual Studio instances from the Running Object Table.
    /// </summary>
    private VisualStudioInstance[] EnumerateVisualStudioInstances(IRunningObjectTable rot)
    {
        var instances = new List<VisualStudioInstance>();

        return ComInteropHelper.WithComObject(
            () => {
                rot.EnumRunning(out var enumMoniker);
                return enumMoniker;
            },
            enumMoniker => {
                var monikers = new IMoniker[1];
                var fetched = IntPtr.Zero;

                while (enumMoniker.Next(1, monikers, fetched) == 0)
                {
                    var moniker = monikers[0];
                    if (moniker == null) continue;

                    try
                    {
                        var instance = ProcessMoniker(moniker);
                        if (instance != null)
                        {
                            instances.Add(instance);
                            _logger.LogDebug("Found VS instance: PID={ProcessId}, Version={Version}", 
                                instance.ProcessId, instance.Version);
                        }
                    }
                    finally
                    {
                        ComInteropHelper.SafeReleaseComObject(moniker, _logger, "IMoniker");
                    }
                }

                _logger.LogInformation("Found {Count} Visual Studio instances", instances.Count);
                return instances.ToArray();
            },
            _logger,
            "EnumerateROMEntries");
    }

    /// <summary>
    /// Processes a single moniker to extract Visual Studio instance information.
    /// </summary>
    private VisualStudioInstance? ProcessMoniker(IMoniker moniker)
    {
        return ComInteropHelper.WithComObject(
            () => {
                CreateBindCtx(0, out var bindCtx);
                return bindCtx;
            },
            bindCtx => {
                moniker.GetDisplayName(bindCtx, null, out var displayName);
                
                // Check if this is a Visual Studio DTE object
                if (displayName != null && displayName.StartsWith("!VisualStudio.DTE"))
                {
                    // Extract process ID from display name
                    var processId = ExtractProcessIdFromDisplayName(displayName);
                    if (processId > 0)
                    {
                        // Get the actual DTE object from ROT
                        return ComInteropHelper.WithComObject(
                            () => {
                                GetRunningObjectTable(0, out var rot);
                                return rot;
                            },
                            rot => {
                                rot.GetObject(moniker, out var obj);
                                if (obj is DTE dte)
                                {
                                    return ComInteropHelper.WithComObject(
                                        () => dte,
                                        dteObj => CreateVisualStudioInstance(dteObj, processId),
                                        _logger,
                                        "ExtractInstanceInfo");
                                }
                                return null;
                            },
                            _logger,
                            "GetDTEFromROT");
                    }
                }
                return null;
            },
            _logger,
            "ProcessMoniker");
    }

    public async Task<VisualStudioInstance> ConnectToInstanceAsync(int processId)
    {
        _logger.LogInformation("Connecting to Visual Studio instance with PID: {ProcessId}", processId);

        return await Task.Run(() =>
        {
            return ComInteropHelper.WithComObject(
                () => {
                    var hr = GetRunningObjectTable(0, out var rot);
                    if (hr != 0)
                    {
                        throw new COMException($"Failed to get Running Object Table. HRESULT: {hr:X}");
                    }
                    return rot;
                },
                rot => FindAndConnectToInstance(rot, processId),
                _logger,
                "ConnectToVisualStudioInstance");
        }).ConfigureAwait(false);
    }

    private VisualStudioInstance FindAndConnectToInstance(IRunningObjectTable rot, int processId)
    {
        return ComInteropHelper.WithComObject(
            () => {
                rot.EnumRunning(out var enumMoniker);
                return enumMoniker;
            },
            enumMoniker => {
                var monikers = new IMoniker[1];
                var fetched = IntPtr.Zero;

                while (enumMoniker.Next(1, monikers, fetched) == 0)
                {
                    var moniker = monikers[0];
                    if (moniker == null) continue;

                    try
                    {
                        var instance = TryConnectToMoniker(rot, moniker, processId);
                        if (instance != null)
                        {
                            _logger.LogInformation("Successfully connected to VS instance: PID={ProcessId}, Version={Version}",
                                instance.ProcessId, instance.Version);
                            return instance;
                        }
                    }
                    finally
                    {
                        ComInteropHelper.SafeReleaseComObject(moniker, _logger, "IMoniker");
                    }
                }

                throw new InvalidOperationException($"Visual Studio instance with process ID {processId} not found or not accessible");
            },
            _logger,
            "EnumerateMonikers");
    }

    private VisualStudioInstance? TryConnectToMoniker(IRunningObjectTable rot, IMoniker moniker, int processId)
    {
        return ComInteropHelper.WithComObject(
            () => {
                CreateBindCtx(0, out var bindCtx);
                return bindCtx;
            },
            bindCtx => {
                moniker.GetDisplayName(bindCtx, null, out var displayName);
                
                if (displayName != null && displayName.StartsWith("!VisualStudio.DTE"))
                {
                    var foundProcessId = ExtractProcessIdFromDisplayName(displayName);
                    if (foundProcessId == processId)
                    {
                        rot.GetObject(moniker, out var obj);
                        if (obj is DTE dte)
                        {
                            // Store the connected instance for future use
                            _connectedInstances[processId] = dte;
                            
                            return ComInteropHelper.WithComObject(
                                () => dte,
                                dteInstance => CreateVisualStudioInstance(dteInstance, processId),
                                _logger,
                                "ExtractInstanceInfo");
                        }
                    }
                }
                return null;
            },
            _logger,
            "ProcessMonikerForConnection");
    }

    public async Task<SolutionInfo> OpenSolutionAsync(string solutionPath)
    {
        _logger.LogInformation("Opening solution: {SolutionPath}", solutionPath);
        
        if (string.IsNullOrWhiteSpace(solutionPath))
            throw new ArgumentException("Solution path cannot be null or empty", nameof(solutionPath));
        
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        return await Task.Run(() =>
        {
            // Get the first available connected instance, or throw if none available
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            return ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    // Open the solution
                    dte.Solution.Open(solutionPath);
                    
                    // Wait for solution to fully load
                    while (!dte.Solution.IsOpen)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    
                    return CreateSolutionInfo(dte);
                },
                _logger,
                "OpenSolution");
        }).ConfigureAwait(false);
    }

    public async Task<BuildResult> BuildSolutionAsync(string configuration = "Debug")
    {
        _logger.LogInformation("Building solution with configuration: {Configuration}", configuration);
        
        if (string.IsNullOrWhiteSpace(configuration))
            throw new ArgumentException("Configuration cannot be null or empty", nameof(configuration));

        return await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            return ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    if (!dte.Solution.IsOpen)
                    {
                        throw new InvalidOperationException("No solution is currently open. Open a solution first.");
                    }

                    var startTime = DateTime.UtcNow;
                    var buildResult = new BuildResult
                    {
                        Configuration = configuration
                    };

                    try
                    {
                        // Set the solution configuration
                        var solutionBuild = dte.Solution.SolutionBuild;
                        
                        // Find the matching configuration
                        var foundConfig = false;
                        foreach (SolutionConfiguration config in solutionBuild.SolutionConfigurations)
                        {
                            if (config.Name.Equals(configuration, StringComparison.OrdinalIgnoreCase))
                            {
                                config.Activate();
                                foundConfig = true;
                                break;
                            }
                        }

                        if (!foundConfig)
                        {
                            _logger.LogWarning("Configuration '{Configuration}' not found, using current active configuration", configuration);
                        }

                        // Clear error list before building
                        dte.ExecuteCommand("Build.ClearErrorList");

                        // Start the build
                        solutionBuild.Build(true); // true = wait for completion

                        // Calculate duration
                        buildResult.Duration = DateTime.UtcNow - startTime;

                        // Check build result
                        buildResult.Success = solutionBuild.LastBuildInfo == 0; // 0 = success

                        // Capture build output and errors
                        CaptureBuildOutput(dte, buildResult);

                        _logger.LogInformation("Build completed. Success: {Success}, Errors: {ErrorCount}, Warnings: {WarningCount}", 
                            buildResult.Success, buildResult.ErrorCount, buildResult.WarningCount);

                        return buildResult;
                    }
                    catch (Exception ex)
                    {
                        buildResult.Success = false;
                        buildResult.Duration = DateTime.UtcNow - startTime;
                        buildResult.Output = $"Build failed with exception: {ex.Message}";
                        
                        _logger.LogError(ex, "Build operation failed");
                        return buildResult;
                    }
                },
                _logger,
                "BuildSolution");
        }).ConfigureAwait(false);
    }

    public async Task<ProjectInfo[]> GetProjectsAsync()
    {
        _logger.LogInformation("Getting projects from current solution...");
        
        return await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            return ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    if (!dte.Solution.IsOpen)
                    {
                        _logger.LogWarning("No solution is currently open");
                        return Array.Empty<ProjectInfo>();
                    }
                    
                    var projects = new List<ProjectInfo>();
                    
                    foreach (EnvDTE.Project project in dte.Solution.Projects)
                    {
                        if (project != null)
                        {
                            try
                            {
                                var projectInfo = CreateProjectInfo(project);
                                if (projectInfo != null)
                                {
                                    projects.Add(projectInfo);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to process project: {ProjectName}", 
                                    project.Name ?? "Unknown");
                            }
                        }
                    }
                    
                    _logger.LogInformation("Found {Count} projects in solution", projects.Count);
                    return projects.ToArray();
                },
                _logger,
                "GetProjects");
        }).ConfigureAwait(false);
    }

    public async Task ExecuteCommandAsync(string commandName, string args = "")
    {
        _logger.LogInformation("Executing VS command: {CommandName} with args: {Args}", commandName, args);
        
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("Command name cannot be null or empty", nameof(commandName));

        await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    try
                    {
                        if (string.IsNullOrEmpty(args))
                        {
                            dte.ExecuteCommand(commandName);
                        }
                        else
                        {
                            dte.ExecuteCommand(commandName, args);
                        }
                        
                        _logger.LogDebug("Command executed successfully: {CommandName}", commandName);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to execute command: {CommandName}", commandName);
                        throw new InvalidOperationException($"Failed to execute command '{commandName}': {ex.Message}", ex);
                    }
                },
                _logger,
                "ExecuteCommand");
        }).ConfigureAwait(false);
    }

    public async Task<bool> IsConnectionHealthyAsync(int processId)
    {
        _logger.LogDebug("Checking connection health for Visual Studio instance with PID: {ProcessId}", processId);
        
        return await Task.Run(() =>
        {
            try
            {
                // Check if process is still running
                using var process = System.Diagnostics.Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    _logger.LogDebug("Process {ProcessId} has exited, connection unhealthy", processId);
                    return false;
                }

                // Try to access the DTE object to verify COM connection health
                return ComInteropHelper.WithComObject(
                    () => {
                        var hr = GetRunningObjectTable(0, out var rot);
                        if (hr != 0)
                        {
                            throw new COMException($"Failed to get Running Object Table. HRESULT: {hr:X}");
                        }
                        return rot;
                    },
                    rot => CheckDteConnectionHealth(rot, processId),
                    _logger,
                    "CheckConnectionHealth");
            }
            catch (ArgumentException)
            {
                // Process not found
                _logger.LogDebug("Process {ProcessId} not found, connection unhealthy", processId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking connection health for process {ProcessId}", processId);
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task DisconnectFromInstanceAsync(int processId)
    {
        _logger.LogInformation("Gracefully disconnecting from Visual Studio instance with PID: {ProcessId}", processId);
        
        await Task.Run(() =>
        {
            try
            {
                if (_connectedInstances.TryGetValue(processId, out var dte))
                {
                    // Remove from connected instances
                    _connectedInstances.Remove(processId);
                    
                    // Release the COM object
                    ComInteropHelper.SafeReleaseComObject(dte, _logger, "DTE");
                    
                    _logger.LogDebug("Successfully disconnected from Visual Studio instance with PID: {ProcessId}", processId);
                }
                else
                {
                    _logger.LogDebug("Visual Studio instance with PID {ProcessId} was not connected", processId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during disconnection from process {ProcessId}", processId);
            }
        }).ConfigureAwait(false);
    }

    public async Task<string[]> GetOutputPanesAsync()
    {
        _logger.LogInformation("Getting available output window panes...");
        
        return await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            return ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    var paneNames = new List<string>();
                    
                    try
                    {
                        if (!(dte is DTE2 dte2))
                        {
                            _logger.LogError("DTE object is not DTE2, cannot access ToolWindows");
                            return new[] { "Build", "Debug", "General" };
                        }
                        
                        var outputWindow = dte2.ToolWindows.OutputWindow;
                        foreach (OutputWindowPane pane in outputWindow.OutputWindowPanes)
                        {
                            if (pane != null && !string.IsNullOrEmpty(pane.Name))
                            {
                                paneNames.Add(pane.Name);
                            }
                        }
                        
                        _logger.LogDebug("Found {Count} output panes", paneNames.Count);
                        return paneNames.ToArray();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error enumerating output panes");
                        return new[] { "Build", "Debug", "General" }; // Default panes
                    }
                },
                _logger,
                "GetOutputPanes");
        }).ConfigureAwait(false);
    }

    public async Task<string> GetOutputPaneContentAsync(string paneName)
    {
        _logger.LogInformation("Getting content from output pane: {PaneName}", paneName);
        
        if (string.IsNullOrWhiteSpace(paneName))
            throw new ArgumentException("Pane name cannot be null or empty", nameof(paneName));

        return await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            return ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    try
                    {
                        if (!(dte is DTE2 dte2))
                        {
                            _logger.LogError("DTE object is not DTE2, cannot access ToolWindows");
                            return string.Empty;
                        }
                        
                        var outputWindow = dte2.ToolWindows.OutputWindow;
                        var pane = outputWindow.OutputWindowPanes.Item(paneName);
                        
                        if (pane?.TextDocument != null)
                        {
                            var editPoint = pane.TextDocument.StartPoint.CreateEditPoint();
                            var content = editPoint.GetText(pane.TextDocument.EndPoint);
                            
                            _logger.LogDebug("Retrieved {Length} characters from pane {PaneName}", 
                                content.Length, paneName);
                            return content;
                        }
                        
                        _logger.LogWarning("Output pane '{PaneName}' not found or has no content", paneName);
                        return string.Empty;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error getting content from output pane: {PaneName}", paneName);
                        return $"Error accessing pane '{paneName}': {ex.Message}";
                    }
                },
                _logger,
                "GetOutputPaneContent");
        }).ConfigureAwait(false);
    }

    public async Task ClearOutputPaneAsync(string paneName)
    {
        _logger.LogInformation("Clearing output pane: {PaneName}", paneName);
        
        if (string.IsNullOrWhiteSpace(paneName))
            throw new ArgumentException("Pane name cannot be null or empty", nameof(paneName));

        await Task.Run(() =>
        {
            var connectedInstance = _connectedInstances.Values.FirstOrDefault();
            if (connectedInstance == null)
            {
                throw new InvalidOperationException("No Visual Studio instance is connected. Use ConnectToInstanceAsync first.");
            }

            ComInteropHelper.WithComObject(
                () => connectedInstance,
                dte => {
                    try
                    {
                        if (!(dte is DTE2 dte2))
                        {
                            _logger.LogError("DTE object is not DTE2, cannot access ToolWindows");
                            return true;
                        }
                        
                        var outputWindow = dte2.ToolWindows.OutputWindow;
                        var pane = outputWindow.OutputWindowPanes.Item(paneName);
                        
                        if (pane != null)
                        {
                            pane.Clear();
                            _logger.LogDebug("Successfully cleared output pane: {PaneName}", paneName);
                        }
                        else
                        {
                            _logger.LogWarning("Output pane '{PaneName}' not found", paneName);
                        }
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error clearing output pane: {PaneName}", paneName);
                        throw new InvalidOperationException($"Failed to clear pane '{paneName}': {ex.Message}", ex);
                    }
                },
                _logger,
                "ClearOutputPane");
        }).ConfigureAwait(false);
    }

    private bool CheckDteConnectionHealth(IRunningObjectTable rot, int processId)
    {
        return ComInteropHelper.WithComObject(
            () => {
                rot.EnumRunning(out var enumMoniker);
                return enumMoniker;
            },
            enumMoniker => {
                var monikers = new IMoniker[1];
                var fetched = IntPtr.Zero;

                while (enumMoniker.Next(1, monikers, fetched) == 0)
                {
                    var moniker = monikers[0];
                    if (moniker == null) continue;

                    try
                    {
                        var isHealthy = ComInteropHelper.WithComObject(
                            () => {
                                CreateBindCtx(0, out var bindCtx);
                                return bindCtx;
                            },
                            bindCtx => {
                                moniker.GetDisplayName(bindCtx, null, out var displayName);
                                
                                if (displayName != null && displayName.StartsWith("!VisualStudio.DTE"))
                                {
                                    var foundProcessId = ExtractProcessIdFromDisplayName(displayName);
                                    if (foundProcessId == processId)
                                    {
                                        // Try to get the DTE object to test connection health
                                        try
                                        {
                                            rot.GetObject(moniker, out var obj);
                                            if (obj is DTE dte)
                                            {
                                                // Try to access a basic property to verify the connection works
                                                ComInteropHelper.WithComObject(
                                                    () => dte,
                                                    dteInstance => {
                                                        _ = dteInstance.Version; // Simple property access test
                                                        return true;
                                                    },
                                                    _logger,
                                                    "HealthCheckPropertyAccess");
                                                return true;
                                            }
                                        }
                                        catch
                                        {
                                            return false;
                                        }
                                    }
                                }
                                return false;
                            },
                            _logger,
                            "CheckMonikerHealth");

                        if (isHealthy)
                        {
                            return true;
                        }
                    }
                    finally
                    {
                        ComInteropHelper.SafeReleaseComObject(moniker, _logger, "IMoniker");
                    }
                }

                return false; // DTE object not found in ROT
            },
            _logger,
            "EnumerateForHealthCheck");
    }

    /// <summary>
    /// Extracts the process ID from a Visual Studio DTE display name.
    /// Display name format: "!VisualStudio.DTE.17.0:{ProcessId}"
    /// </summary>
    private int ExtractProcessIdFromDisplayName(string displayName)
    {
        try
        {
            // Display name format: "!VisualStudio.DTE.17.0:{ProcessId}"
            var lastColonIndex = displayName.LastIndexOf(':');
            if (lastColonIndex >= 0 && lastColonIndex < displayName.Length - 1)
            {
                var processIdString = displayName.Substring(lastColonIndex + 1);
                if (int.TryParse(processIdString, out var processId))
                {
                    return processId;
                }
            }

            _logger.LogWarning("Could not extract process ID from display name: {DisplayName}", displayName);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting process ID from display name: {DisplayName}", displayName);
            return 0;
        }
    }

    /// <summary>
    /// Creates a VisualStudioInstance from a DTE object and process ID.
    /// </summary>
    private VisualStudioInstance? CreateVisualStudioInstance(DTE dte, int processId)
    {
        try
        {
            var instance = new VisualStudioInstance
            {
                ProcessId = processId,
                IsConnected = true
            };

            // Get Visual Studio version
            try
            {
                instance.Version = dte.Version ?? "Unknown";
            }
            catch (COMException)
            {
                instance.Version = "Unknown";
            }

            // Get solution name
            try
            {
                var solutionFullName = dte.Solution?.FullName;
                if (!string.IsNullOrEmpty(solutionFullName))
                {
                    instance.SolutionName = Path.GetFileNameWithoutExtension(solutionFullName);
                }
                else
                {
                    instance.SolutionName = "No solution";
                }
            }
            catch (COMException)
            {
                instance.SolutionName = "Unknown";
            }

            // Get start time from process
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(processId);
                instance.StartTime = process.StartTime;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not get start time for process {ProcessId}", processId);
                instance.StartTime = DateTime.MinValue;
            }

            return instance;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error creating VisualStudioInstance for process {ProcessId}", processId);
            return null;
        }
    }

    /// <summary>
    /// Captures build output and errors from the Visual Studio error list and output window.
    /// </summary>
    private void CaptureBuildOutput(DTE dte, BuildResult buildResult)
    {
        try
        {
            var output = new System.Text.StringBuilder();
            var errors = new List<BuildError>();
            var warnings = new List<BuildWarning>();

            // Get output from Build output pane
            try
            {
                if (!(dte is DTE2 dte2))
                {
                    _logger.LogDebug("DTE object is not DTE2, cannot access output window");
                    output.AppendLine("Build output not available - DTE2 required");
                }
                else
                {
                    var outputWindow = dte2.ToolWindows.OutputWindow;
                    var buildPane = outputWindow.OutputWindowPanes.Item("Build");
                    if (buildPane != null)
                    {
                        var textDocument = buildPane.TextDocument;
                        if (textDocument != null)
                        {
                            var editPoint = textDocument.StartPoint.CreateEditPoint();
                            var buildOutput = editPoint.GetText(textDocument.EndPoint);
                            output.AppendLine(buildOutput);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not capture build output from output window");
                output.AppendLine("Build output not available");
            }

            // Get errors and warnings from Error List
            try
            {
                if (!(dte is DTE2 dte2))
                {
                    _logger.LogDebug("DTE object is not DTE2, cannot access error list");
                }
                else
                {
                    var errorList = dte2.ToolWindows.ErrorList;
                    if (errorList != null)
                    {
                        for (int i = 1; i <= errorList.ErrorItems.Count; i++)
                        {
                            var errorItem = errorList.ErrorItems.Item(i);
                            if (errorItem != null)
                            {
                                if (errorItem.ErrorLevel == vsBuildErrorLevel.vsBuildErrorLevelHigh)
                                {
                                    errors.Add(new BuildError
                                    {
                                        File = errorItem.FileName ?? string.Empty,
                                        Line = errorItem.Line,
                                        Column = errorItem.Column,
                                        Code = errorItem.ErrorLevel.ToString(),
                                        Message = errorItem.Description ?? string.Empty,
                                        Project = errorItem.Project ?? string.Empty
                                    });
                                }
                                else if (errorItem.ErrorLevel == vsBuildErrorLevel.vsBuildErrorLevelMedium)
                                {
                                    warnings.Add(new BuildWarning
                                    {
                                        File = errorItem.FileName ?? string.Empty,
                                        Line = errorItem.Line,
                                        Column = errorItem.Column,
                                        Code = errorItem.ErrorLevel.ToString(),
                                        Message = errorItem.Description ?? string.Empty,
                                        Project = errorItem.Project ?? string.Empty
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not capture errors from error list");
            }

            buildResult.Output = output.ToString();
            buildResult.Errors = errors.ToArray();
            buildResult.Warnings = warnings.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing build output");
            buildResult.Output = "Error capturing build output";
        }
    }

    /// <summary>
    /// Creates a SolutionInfo object from a DTE solution.
    /// </summary>
    private SolutionInfo CreateSolutionInfo(DTE dte)
    {
        try
        {
            var solution = dte.Solution;
            var solutionInfo = new SolutionInfo
            {
                FullPath = solution.FullName ?? string.Empty,
                Name = Path.GetFileNameWithoutExtension(solution.FullName) ?? "Unknown",
                IsOpen = solution.IsOpen
            };

            var projects = new List<ProjectInfo>();
            foreach (EnvDTE.Project project in solution.Projects)
            {
                if (project != null)
                {
                    try
                    {
                        var projectInfo = CreateProjectInfo(project);
                        if (projectInfo != null)
                        {
                            projects.Add(projectInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to process project: {ProjectName}", 
                            project.Name ?? "Unknown");
                    }
                }
            }

            solutionInfo.Projects = projects.ToArray();
            return solutionInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SolutionInfo");
            throw;
        }
    }

    /// <summary>
    /// Creates a ProjectInfo object from a DTE project.
    /// </summary>
    private ProjectInfo? CreateProjectInfo(EnvDTE.Project project)
    {
        try
        {
            var projectInfo = new ProjectInfo
            {
                FullPath = project.FullName ?? string.Empty,
                Name = project.Name ?? "Unknown",
                IsLoaded = true
            };

            // Get project type from Kind
            try
            {
                projectInfo.ProjectType = GetProjectTypeFromKind(project.Kind);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not determine project type for {ProjectName}", project.Name);
                projectInfo.ProjectType = "Unknown";
            }

            // Get target frameworks (this is more complex and may require accessing project properties)
            try
            {
                var targetFramework = GetProjectTargetFramework(project);
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    projectInfo.TargetFrameworks = new[] { targetFramework };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not determine target framework for {ProjectName}", project.Name);
            }

            return projectInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ProjectInfo for project: {ProjectName}", 
                project?.Name ?? "Unknown");
            return null;
        }
    }

    /// <summary>
    /// Gets a human-readable project type from the project kind GUID.
    /// </summary>
    private string GetProjectTypeFromKind(string kind)
    {
        return kind?.ToUpperInvariant() switch
        {
            "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}" => "C#",
            "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}" => "VB.NET",
            "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}" => "C++",
            "{A9ACE9BB-CECE-4E62-9AA4-C7E7C5BD2124}" => "Database",
            "{54435603-DBB4-11D2-8724-00A0C9A8B90C}" => "Setup",
            "{2150E333-8FDC-42A3-9474-1A3956D46DE8}" => "Solution Folder",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Gets the target framework for a project.
    /// </summary>
    private string GetProjectTargetFramework(EnvDTE.Project project)
    {
        try
        {
            // Try to get TargetFramework property
            foreach (EnvDTE.Property prop in project.Properties)
            {
                if (prop.Name == "TargetFramework" || prop.Name == "TargetFrameworkMoniker")
                {
                    return prop.Value?.ToString() ?? string.Empty;
                }
            }
            
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Performs health checks on all connected instances and handles crash recovery.
    /// </summary>
    private void PerformHealthChecks(object? state)
    {
        try
        {
            var disconnectedInstances = new List<int>();
            
            foreach (var kvp in _connectedInstances.ToArray())
            {
                var processId = kvp.Key;
                var dte = kvp.Value;
                
                try
                {
                    // Check if process still exists
                    using var process = System.Diagnostics.Process.GetProcessById(processId);
                    if (process.HasExited)
                    {
                        _logger.LogWarning("Visual Studio instance {ProcessId} has crashed or been closed", processId);
                        disconnectedInstances.Add(processId);
                        continue;
                    }

                    // Try to access a simple DTE property to verify COM connection
                    _ = dte.Version;
                    
                    // Update last successful health check
                    _lastHealthCheck[processId] = DateTime.UtcNow;
                    
                    _logger.LogDebug("Health check passed for VS instance {ProcessId}", processId);
                }
                catch (ArgumentException)
                {
                    // Process not found
                    _logger.LogWarning("Visual Studio instance {ProcessId} process not found", processId);
                    disconnectedInstances.Add(processId);
                }
                catch (COMException comEx)
                {
                    _logger.LogWarning(comEx, "COM connection lost for VS instance {ProcessId}", processId);
                    disconnectedInstances.Add(processId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Health check failed for VS instance {ProcessId}", processId);
                    disconnectedInstances.Add(processId);
                }
            }

            // Clean up disconnected instances
            foreach (var processId in disconnectedInstances)
            {
                if (_connectedInstances.TryGetValue(processId, out var dte))
                {
                    try
                    {
                        ComInteropHelper.SafeReleaseComObject(dte, _logger, "DTE");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error releasing COM object for crashed instance {ProcessId}", processId);
                    }
                    
                    _connectedInstances.Remove(processId);
                    _lastHealthCheck.Remove(processId);
                }
                
                _logger.LogInformation("Cleaned up crashed/disconnected VS instance {ProcessId}", processId);
            }

            if (disconnectedInstances.Count > 0)
            {
                _logger.LogWarning("Detected and cleaned up {Count} crashed Visual Studio instances", 
                    disconnectedInstances.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check operation");
        }
    }

    /// <summary>
    /// Disposes resources and stops health monitoring.
    /// </summary>
    public void Dispose()
    {
        try
        {
            _healthCheckTimer?.Dispose();
            
            // Disconnect from all instances
            foreach (var kvp in _connectedInstances.ToArray())
            {
                try
                {
                    ComInteropHelper.SafeReleaseComObject(kvp.Value, _logger, "DTE");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error releasing COM object during disposal");
                }
            }
            
            _connectedInstances.Clear();
            _lastHealthCheck.Clear();
            
            _logger.LogDebug("VisualStudioService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during VisualStudioService disposal");
        }
    }
}