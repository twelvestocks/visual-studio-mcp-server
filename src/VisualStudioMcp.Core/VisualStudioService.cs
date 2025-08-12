using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using EnvDTE;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core;

/// <summary>
/// Implementation of Visual Studio automation service using COM interop.
/// </summary>
public class VisualStudioService : IVisualStudioService
{
    private readonly ILogger<VisualStudioService> _logger;

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
        
        // TODO: Implement solution opening via COM
        await Task.Delay(10); // Placeholder
        
        throw new NotImplementedException("COM interop not yet implemented");
    }

    public async Task<BuildResult> BuildSolutionAsync(string configuration = "Debug")
    {
        _logger.LogInformation("Building solution with configuration: {Configuration}", configuration);
        
        // TODO: Implement solution building via COM
        await Task.Delay(10); // Placeholder
        
        throw new NotImplementedException("COM interop not yet implemented");
    }

    public async Task<ProjectInfo[]> GetProjectsAsync()
    {
        _logger.LogInformation("Getting projects from current solution...");
        
        // TODO: Implement project enumeration via COM
        await Task.Delay(10); // Placeholder
        
        return Array.Empty<ProjectInfo>();
    }

    public async Task ExecuteCommandAsync(string commandName, string args = "")
    {
        _logger.LogInformation("Executing VS command: {CommandName} with args: {Args}", commandName, args);
        
        // TODO: Implement command execution via COM
        await Task.Delay(10); // Placeholder
        
        throw new NotImplementedException("COM interop not yet implemented");
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
                // In COM interop, disconnection is mainly about releasing references
                // The actual cleanup happens when COM objects are released
                _logger.LogDebug("Connection cleanup completed for process {ProcessId}", processId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during disconnection from process {ProcessId}", processId);
            }
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
}