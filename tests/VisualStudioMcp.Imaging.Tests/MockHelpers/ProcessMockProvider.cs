using System.Diagnostics;

namespace VisualStudioMcp.Imaging.Tests.MockHelpers;

/// <summary>
/// Provides utilities for mocking process access scenarios in tests.
/// </summary>
public static class ProcessMockProvider
{
    /// <summary>
    /// Process IDs that will simulate process not found scenarios.
    /// </summary>
    public static readonly uint[] NonExistentProcessIds = { 99999, 88888, 77777 };

    /// <summary>
    /// Process IDs that will simulate access denied scenarios.
    /// </summary>
    public static readonly uint[] AccessDeniedProcessIds = { 4, 8, 12 }; // System processes

    /// <summary>
    /// Process IDs that represent valid Visual Studio processes.
    /// </summary>
    public static readonly uint[] ValidVSProcessIds;

    /// <summary>
    /// Process names that should be recognized as Visual Studio.
    /// </summary>
    public static readonly HashSet<string> ValidVSProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "devenv",
        "visualstudio", 
        "code"
    };

    static ProcessMockProvider()
    {
        // Get actual running processes that match VS patterns for realistic testing
        var currentProcesses = Process.GetProcesses()
            .Where(p => ValidVSProcessNames.Any(vsName => 
                p.ProcessName.Contains(vsName, StringComparison.OrdinalIgnoreCase)))
            .Select(p => (uint)p.Id)
            .ToArray();

        ValidVSProcessIds = currentProcesses.Length > 0 
            ? currentProcesses 
            : new uint[] { (uint)Process.GetCurrentProcess().Id }; // Fallback to current process
    }

    /// <summary>
    /// Determines if a process ID should throw ArgumentException (process not found).
    /// </summary>
    public static bool ShouldThrowArgumentException(uint processId)
    {
        return NonExistentProcessIds.Contains(processId);
    }

    /// <summary>
    /// Determines if a process ID should throw InvalidOperationException (access denied).
    /// </summary>
    public static bool ShouldThrowInvalidOperationException(uint processId)
    {
        return AccessDeniedProcessIds.Contains(processId);
    }

    /// <summary>
    /// Determines if a process ID represents a valid Visual Studio process.
    /// </summary>
    public static bool IsValidVSProcess(uint processId)
    {
        return ValidVSProcessIds.Contains(processId);
    }

    /// <summary>
    /// Gets a process name for a given process ID (for mocking).
    /// </summary>
    public static string GetMockProcessName(uint processId)
    {
        if (ShouldThrowArgumentException(processId))
            throw new ArgumentException($"Process {processId} not found");
            
        if (ShouldThrowInvalidOperationException(processId))
            throw new InvalidOperationException($"Process {processId} has exited");

        if (IsValidVSProcess(processId))
        {
            // Return a valid VS process name
            var validNames = new[] { "devenv", "visualstudio", "code" };
            return validNames[processId % (uint)validNames.Length];
        }

        return "notepad"; // Non-VS process
    }

    /// <summary>
    /// Creates test data for process access scenarios.
    /// </summary>
    public static IEnumerable<object[]> GetProcessAccessTestData()
    {
        return new[]
        {
            new object[] { NonExistentProcessIds[0], typeof(ArgumentException), false },
            new object[] { AccessDeniedProcessIds[0], typeof(InvalidOperationException), false },
            new object[] { ValidVSProcessIds[0], null, true }
        };
    }
}