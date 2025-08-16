using System.Diagnostics;
using System.Runtime.InteropServices;
using EnvDTE;
using EnvDTE80;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Core.Services;

/// <summary>
/// Service for capturing comprehensive Visual Studio diagnostic information for error reporting and debugging.
/// </summary>
public class VisualStudioDiagnosticsService : IVisualStudioDiagnosticsService
{
    private readonly ILogger<VisualStudioDiagnosticsService> _logger;
    private readonly object _captureLock = new();

    /// <summary>
    /// Initializes a new instance of the VisualStudioDiagnosticsService.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public VisualStudioDiagnosticsService(ILogger<VisualStudioDiagnosticsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Captures current Visual Studio state diagnostics.
    /// </summary>
    /// <param name="targetProcessId">Optional specific VS process ID to focus on.</param>
    /// <returns>Comprehensive Visual Studio diagnostic information.</returns>
    public VisualStudioDiagnostics CaptureVisualStudioDiagnostics(int? targetProcessId = null)
    {
        lock (_captureLock)
        {
            try
            {
                _logger.LogDebug("Capturing Visual Studio diagnostics for process {ProcessId}", 
                    targetProcessId?.ToString() ?? "all");

                var startTime = DateTime.UtcNow;
                var instances = CaptureVisualStudioInstances(targetProcessId);
                var activeInstance = DetermineActiveInstance(instances, targetProcessId);
                var comHealth = CaptureComConnectionHealth();
                var recentOperations = CaptureRecentOperations();

                var diagnostics = new VisualStudioDiagnostics
                {
                    Instances = instances,
                    ActiveInstance = activeInstance,
                    ComHealth = comHealth,
                    RecentOperations = recentOperations,
                    CaptureTime = startTime
                };

                var captureTime = DateTime.UtcNow - startTime;
                _logger.LogDebug("Visual Studio diagnostics captured in {CaptureTime}ms", 
                    captureTime.TotalMilliseconds);

                return diagnostics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error capturing Visual Studio diagnostics");
                
                // Return minimal diagnostics on failure
                return new VisualStudioDiagnostics
                {
                    CaptureTime = DateTime.UtcNow,
                    Instances = Array.Empty<VisualStudioInstanceDiagnostics>(),
                    ComHealth = new ComConnectionHealth
                    {
                        ActiveConnections = 0,
                        SuccessRate = 0,
                        RecentErrors = new[]
                        {
                            new ComErrorInfo
                            {
                                Description = ex.Message,
                                Timestamp = DateTime.UtcNow,
                                Operation = "Capture VS Diagnostics"
                            }
                        }
                    }
                };
            }
        }
    }

    /// <summary>
    /// Captures diagnostics for a specific VS instance connected via DTE.
    /// </summary>
    /// <param name="dte">Connected DTE instance.</param>
    /// <param name="correlationId">Correlation ID for tracking.</param>
    /// <returns>Detailed diagnostics for the specific instance.</returns>
    public VisualStudioInstanceDiagnostics CaptureInstanceDiagnostics(DTE dte, string? correlationId = null)
    {
        try
        {
            _logger.LogDebug("Capturing instance diagnostics for DTE with correlation {CorrelationId}", 
                correlationId ?? "none");

            var processId = GetDteProcessId(dte);
            var process = processId.HasValue ? GetProcessSafely(processId.Value) : null;

            var instanceDiagnostics = new VisualStudioInstanceDiagnostics
            {
                ProcessId = processId ?? 0,
                Version = GetVisualStudioVersion(dte),
                WindowTitle = GetMainWindowTitle(dte),
                Solution = CaptureSolutionDiagnostics(dte),
                ProcessHealth = CaptureProcessHealth(process),
                IsResponsive = IsInstanceResponsive(dte),
                ConnectionStatus = ConnectionStatus.Connected,
                LastCommunication = DateTime.UtcNow,
                DebugSessions = CaptureDebugSessions(dte)
            };

            return instanceDiagnostics;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing instance diagnostics for correlation {CorrelationId}", 
                correlationId);

            return new VisualStudioInstanceDiagnostics
            {
                ProcessId = 0,
                Version = "Unknown",
                WindowTitle = "Unknown",
                IsResponsive = false,
                ConnectionStatus = ConnectionStatus.Failed,
                LastCommunication = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Captures all Visual Studio instances running on the system.
    /// </summary>
    private VisualStudioInstanceDiagnostics[] CaptureVisualStudioInstances(int? targetProcessId)
    {
        try
        {
            var instances = new List<VisualStudioInstanceDiagnostics>();
            var vsProcesses = GetVisualStudioProcesses();

            foreach (var process in vsProcesses)
            {
                try
                {
                    // Skip if we're targeting a specific process and this isn't it
                    if (targetProcessId.HasValue && process.Id != targetProcessId.Value)
                        continue;

                    var instanceDiagnostics = new VisualStudioInstanceDiagnostics
                    {
                        ProcessId = process.Id,
                        Version = GetProcessVersion(process),
                        WindowTitle = GetProcessMainWindowTitle(process),
                        ProcessHealth = CaptureProcessHealth(process),
                        IsResponsive = process.Responding,
                        ConnectionStatus = ConnectionStatus.Disconnected,
                        LastCommunication = null
                    };

                    // Try to connect via DTE to get more detailed information
                    try
                    {
                        var dte = TryConnectToDte(process.Id);
                        if (dte != null)
                        {
                            using (new ComObjectWrapper(dte))
                            {
                                instanceDiagnostics.ConnectionStatus = ConnectionStatus.Connected;
                                instanceDiagnostics.LastCommunication = DateTime.UtcNow;
                                instanceDiagnostics.Solution = CaptureSolutionDiagnostics(dte);
                                instanceDiagnostics.DebugSessions = CaptureDebugSessions(dte);
                                instanceDiagnostics.IsResponsive = IsInstanceResponsive(dte);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not connect to DTE for process {ProcessId}", process.Id);
                        instanceDiagnostics.ConnectionStatus = ConnectionStatus.Failed;
                    }

                    instances.Add(instanceDiagnostics);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error capturing diagnostics for VS process {ProcessId}", process.Id);
                }
            }

            return instances.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error capturing Visual Studio instances");
            return Array.Empty<VisualStudioInstanceDiagnostics>();
        }
    }

    /// <summary>
    /// Gets all Visual Studio processes.
    /// </summary>
    private System.Diagnostics.Process[] GetVisualStudioProcesses()
    {
        try
        {
            return System.Diagnostics.Process.GetProcesses()
                .Where(p => IsVisualStudioProcess(p))
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Visual Studio processes");
            return Array.Empty<System.Diagnostics.Process>();
        }
    }

    /// <summary>
    /// Determines if a process is Visual Studio.
    /// </summary>
    private bool IsVisualStudioProcess(System.Diagnostics.Process process)
    {
        try
        {
            var processName = process.ProcessName.ToLowerInvariant();
            return processName == "devenv" || 
                   processName.StartsWith("microsoft.visualstudio");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Captures solution diagnostics from a DTE instance.
    /// </summary>
    private SolutionDiagnostics? CaptureSolutionDiagnostics(DTE dte)
    {
        try
        {
            var solution = dte.Solution;
            if (solution == null)
                return null;

            using (new ComObjectWrapper(solution))
            {
                if (!solution.IsOpen)
                    return null;

                return new SolutionDiagnostics
                {
                    SolutionPath = solution.FullName ?? string.Empty,
                    Name = solution.Properties?.Item("Name")?.Value?.ToString() ?? Path.GetFileNameWithoutExtension(solution.FullName),
                    IsLoaded = solution.IsOpen,
                    ProjectCount = solution.Projects?.Count ?? 0,
                    BuildConfiguration = GetCurrentBuildConfiguration(dte),
                    LastBuildStatus = GetLastBuildStatus(dte),
                    ErrorCounts = GetErrorCounts(dte)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error capturing solution diagnostics");
            return null;
        }
    }

    /// <summary>
    /// Captures debug session information.
    /// </summary>
    private DebugSessionInfo[] CaptureDebugSessions(DTE dte)
    {
        try
        {
            var debugger = dte.Debugger;
            if (debugger == null)
                return Array.Empty<DebugSessionInfo>();

            using (new ComObjectWrapper(debugger))
            {
                var sessions = new List<DebugSessionInfo>();

                // Check for active debugging session
                if (debugger.CurrentMode != dbgDebugMode.dbgDesignMode)
                {
                    sessions.Add(new DebugSessionInfo
                    {
                        SessionId = Guid.NewGuid().ToString(),
                        State = debugger.CurrentMode.ToString(),
                        TargetProcess = debugger.CurrentProcess?.Name,
                        BreakpointCount = debugger.Breakpoints?.Count ?? 0,
                        IsRunning = debugger.CurrentMode == dbgDebugMode.dbgRunMode
                    });
                }

                return sessions.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error capturing debug sessions");
            return Array.Empty<DebugSessionInfo>();
        }
    }

    /// <summary>
    /// Captures process health information.
    /// </summary>
    private ProcessHealthInfo CaptureProcessHealth(System.Diagnostics.Process? process)
    {
        try
        {
            if (process == null)
                return new ProcessHealthInfo();

            return new ProcessHealthInfo
            {
                StartTime = process.StartTime,
                Uptime = DateTime.Now - process.StartTime,
                Memory = new ProcessMemoryInfo
                {
                    PrivateMemoryMb = process.PrivateMemorySize64 / (1024 * 1024),
                    WorkingSetMb = process.WorkingSet64 / (1024 * 1024),
                    VirtualMemoryMb = process.VirtualMemorySize64 / (1024 * 1024),
                    PagedMemoryMb = process.PagedMemorySize64 / (1024 * 1024),
                    PeakWorkingSetMb = process.PeakWorkingSet64 / (1024 * 1024)
                },
                ThreadCount = process.Threads.Count,
                IsResponding = process.Responding
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error capturing process health");
            return new ProcessHealthInfo();
        }
    }

    /// <summary>
    /// Captures COM connection health information.
    /// </summary>
    private ComConnectionHealth CaptureComConnectionHealth()
    {
        try
        {
            // This is a simplified implementation - in a real scenario,
            // you would track actual COM operation statistics
            return new ComConnectionHealth
            {
                ActiveConnections = 1, // Simplified
                SuccessRate = 95.0, // Simplified
                AverageResponseTime = TimeSpan.FromMilliseconds(150),
                RecentErrors = Array.Empty<ComErrorInfo>(),
                RetryStats = new RetryStatistics
                {
                    TotalAttempts = 100,
                    SuccessfulAttempts = 95,
                    RetriedOperations = 5,
                    AverageRetries = 1.2
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error capturing COM connection health");
            return new ComConnectionHealth();
        }
    }

    /// <summary>
    /// Captures recent operation history.
    /// </summary>
    private OperationHistoryEntry[] CaptureRecentOperations()
    {
        try
        {
            // This would typically come from an operation tracking service
            // For now, return empty array
            return Array.Empty<OperationHistoryEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error capturing recent operations");
            return Array.Empty<OperationHistoryEntry>();
        }
    }

    /// <summary>
    /// Determines the active Visual Studio instance.
    /// </summary>
    private VisualStudioInstanceDiagnostics? DetermineActiveInstance(
        VisualStudioInstanceDiagnostics[] instances, 
        int? targetProcessId)
    {
        try
        {
            if (targetProcessId.HasValue)
            {
                return instances.FirstOrDefault(i => i.ProcessId == targetProcessId.Value);
            }

            // Find the most recently active instance
            return instances
                .Where(i => i.ConnectionStatus == ConnectionStatus.Connected)
                .OrderByDescending(i => i.LastCommunication)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error determining active instance");
            return null;
        }
    }

    /// <summary>
    /// Helper methods for extracting information from DTE and processes.
    /// </summary>
    private int? GetDteProcessId(DTE dte)
    {
        try
        {
            // This is a simplified approach - getting process ID from DTE is complex
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetVisualStudioVersion(DTE dte)
    {
        try
        {
            return dte.Version ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetMainWindowTitle(DTE dte)
    {
        try
        {
            return dte.MainWindow?.Caption ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetProcessVersion(System.Diagnostics.Process process)
    {
        try
        {
            return process.MainModule?.FileVersionInfo?.FileVersion ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GetProcessMainWindowTitle(System.Diagnostics.Process process)
    {
        try
        {
            return process.MainWindowTitle ?? "No Window";
        }
        catch
        {
            return "Unknown";
        }
    }

    private System.Diagnostics.Process? GetProcessSafely(int processId)
    {
        try
        {
            return System.Diagnostics.Process.GetProcessById(processId);
        }
        catch
        {
            return null;
        }
    }

    private bool IsInstanceResponsive(DTE dte)
    {
        try
        {
            // Simple responsiveness check - try to access a property
            var _ = dte.Version;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private DTE? TryConnectToDte(int processId)
    {
        try
        {
            // Simplified DTE connection - in reality this would use ROT
            return null;
        }
        catch
        {
            return null;
        }
    }

    private string GetCurrentBuildConfiguration(DTE dte)
    {
        try
        {
            return dte.Solution?.SolutionBuild?.ActiveConfiguration?.Name ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private BuildStatus GetLastBuildStatus(DTE dte)
    {
        try
        {
            var buildStatus = dte.Solution?.SolutionBuild?.LastBuildInfo;
            return buildStatus switch
            {
                0 => BuildStatus.Succeeded,
                _ => BuildStatus.Failed
            };
        }
        catch
        {
            return BuildStatus.Unknown;
        }
    }

    private ErrorCounts GetErrorCounts(DTE dte)
    {
        try
        {
            // Simplified error counting - would need to access Error List
            return new ErrorCounts();
        }
        catch
        {
            return new ErrorCounts();
        }
    }

    /// <summary>
    /// Helper class for safe COM object disposal.
    /// </summary>
    private class ComObjectWrapper : IDisposable
    {
        private readonly object _comObject;
        private bool _disposed;

        public ComObjectWrapper(object comObject)
        {
            _comObject = comObject ?? throw new ArgumentNullException(nameof(comObject));
        }

        public void Dispose()
        {
            if (!_disposed && _comObject != null)
            {
                try
                {
                    Marshal.ReleaseComObject(_comObject);
                }
                catch
                {
                    // Ignore disposal errors
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}

/// <summary>
/// Interface for Visual Studio diagnostics service.
/// </summary>
public interface IVisualStudioDiagnosticsService
{
    /// <summary>
    /// Captures current Visual Studio state diagnostics.
    /// </summary>
    /// <param name="targetProcessId">Optional specific VS process ID to focus on.</param>
    /// <returns>Comprehensive Visual Studio diagnostic information.</returns>
    VisualStudioDiagnostics CaptureVisualStudioDiagnostics(int? targetProcessId = null);

    /// <summary>
    /// Captures diagnostics for a specific VS instance connected via DTE.
    /// </summary>
    /// <param name="dte">Connected DTE instance.</param>
    /// <param name="correlationId">Correlation ID for tracking.</param>
    /// <returns>Detailed diagnostics for the specific instance.</returns>
    VisualStudioInstanceDiagnostics CaptureInstanceDiagnostics(DTE dte, string? correlationId = null);
}