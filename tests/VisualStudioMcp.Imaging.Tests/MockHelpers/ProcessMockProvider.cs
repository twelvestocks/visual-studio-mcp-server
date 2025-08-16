using System.Diagnostics;
using System.Security.Cryptography;

namespace VisualStudioMcp.Imaging.Tests.MockHelpers;

/// <summary>
/// Provides utilities for mocking process access scenarios in tests.
/// </summary>
public static class ProcessMockProvider
{
    /// <summary>
    /// Process IDs that will simulate process not found scenarios.
    /// Generated using cryptographically secure random numbers to prevent predictable attacks.
    /// </summary>
    public static readonly uint[] NonExistentProcessIds;

    /// <summary>
    /// Process IDs that will simulate access denied scenarios.
    /// Generated using cryptographically secure random numbers in the restricted range.
    /// </summary>
    public static readonly uint[] AccessDeniedProcessIds;

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
        // Generate cryptographically secure random process IDs
        NonExistentProcessIds = GenerateSecureRandomProcessIds(3, minValue: 100000, maxValue: 999999);
        
        // Generate secure random IDs in the system process range (avoiding actual system processes)
        AccessDeniedProcessIds = GenerateSecureRandomProcessIds(3, minValue: 1000, maxValue: 9999)
            .Where(pid => !IsActualSystemProcess(pid))
            .ToArray();

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
    /// Generates cryptographically secure random process IDs within the specified range.
    /// </summary>
    private static uint[] GenerateSecureRandomProcessIds(int count, uint minValue, uint maxValue)
    {
        using var rng = RandomNumberGenerator.Create();
        var processIds = new uint[count];
        var range = maxValue - minValue;
        
        for (int i = 0; i < count; i++)
        {
            var randomBytes = new byte[4];
            rng.GetBytes(randomBytes);
            var randomValue = BitConverter.ToUInt32(randomBytes, 0);
            processIds[i] = (randomValue % range) + minValue;
        }
        
        return processIds;
    }

    /// <summary>
    /// Checks if a process ID corresponds to an actual system process that should not be used in testing.
    /// </summary>
    private static bool IsActualSystemProcess(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            // Check if it's a critical system process
            var criticalProcessNames = new[] { "System", "smss", "csrss", "winlogon", "services", "lsass" };
            return criticalProcessNames.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            // Process doesn't exist, safe to use
            return false;
        }
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