using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Core;

/// <summary>
/// Helper class for COM interop operations with proper lifecycle management.
/// </summary>
public static class ComInteropHelper
{
    /// <summary>
    /// Safely executes a COM operation with proper cleanup and exception handling.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The COM operation to execute.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <returns>The result of the operation.</returns>
    public static T SafeComOperation<T>(Func<T> operation, ILogger logger, string operationName)
    {
        try
        {
            logger.LogDebug("Starting COM operation: {OperationName}", operationName);
            var result = operation();
            logger.LogDebug("Completed COM operation: {OperationName}", operationName);
            return result;
        }
        catch (COMException comEx)
        {
            var friendlyMessage = GetComErrorDescription(comEx.HResult);
            logger.LogError(comEx, "COM exception in operation {OperationName}: HRESULT={HResult:X}, Description={Description}, Message={Message}",
                operationName, comEx.HResult, friendlyMessage, comEx.Message);
            throw new ComInteropException($"COM operation '{operationName}' failed: {friendlyMessage}", comEx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception in COM operation {OperationName}: {Message}",
                operationName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Safely executes a COM operation that doesn't return a value.
    /// </summary>
    /// <param name="operation">The COM operation to execute.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    public static void SafeComOperation(Action operation, ILogger logger, string operationName)
    {
        try
        {
            logger.LogDebug("Starting COM operation: {OperationName}", operationName);
            operation();
            logger.LogDebug("Completed COM operation: {OperationName}", operationName);
        }
        catch (COMException comEx)
        {
            var friendlyMessage = GetComErrorDescription(comEx.HResult);
            logger.LogError(comEx, "COM exception in operation {OperationName}: HRESULT={HResult:X}, Description={Description}, Message={Message}",
                operationName, comEx.HResult, friendlyMessage, comEx.Message);
            throw new ComInteropException($"COM operation '{operationName}' failed: {friendlyMessage}", comEx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected exception in COM operation {OperationName}: {Message}",
                operationName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Safely releases a COM object with proper logging.
    /// </summary>
    /// <param name="comObject">The COM object to release.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="objectName">Name of the object for logging.</param>
    public static void SafeReleaseComObject(object? comObject, ILogger logger, string objectName)
    {
        if (comObject == null)
        {
            return;
        }

        try
        {
            var refCount = Marshal.ReleaseComObject(comObject);
            logger.LogDebug("Released COM object {ObjectName}, remaining references: {RefCount}", objectName, refCount);
        }
        catch (ArgumentException)
        {
            // Object is not a COM object, ignore
            logger.LogDebug("Object {ObjectName} is not a COM object, no release needed", objectName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error releasing COM object {ObjectName}: {Message}", objectName, ex.Message);
        }
    }

    /// <summary>
    /// Executes an operation with automatic COM object cleanup.
    /// </summary>
    /// <typeparam name="TComObject">The type of COM object to manage.</typeparam>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    /// <param name="getComObject">Function to get the COM object.</param>
    /// <param name="operation">Operation to perform with the COM object.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <returns>The result of the operation.</returns>
    public static TResult WithComObject<TComObject, TResult>(
        Func<TComObject> getComObject,
        Func<TComObject, TResult> operation,
        ILogger logger,
        string operationName) where TComObject : class
    {
        TComObject? comObject = null;
        try
        {
            // Check memory pressure before expensive COM operations
            if (MemoryMonitor.IsMemoryPressureHigh(logger, 300))
            {
                logger.LogWarning("High memory pressure detected before COM operation {OperationName}, performing cleanup", operationName);
                MemoryMonitor.PerformMemoryCleanup(logger, forceFullCollection: false);
            }

            comObject = getComObject();
            return SafeComOperation(() => operation(comObject), logger, operationName);
        }
        finally
        {
            SafeReleaseComObject(comObject, logger, typeof(TComObject).Name);
        }
    }

    /// <summary>
    /// Executes an operation with automatic COM object cleanup (void return).
    /// </summary>
    /// <typeparam name="TComObject">The type of COM object to manage.</typeparam>
    /// <param name="getComObject">Function to get the COM object.</param>
    /// <param name="operation">Operation to perform with the COM object.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    public static void WithComObject<TComObject>(
        Func<TComObject> getComObject,
        Action<TComObject> operation,
        ILogger logger,
        string operationName) where TComObject : class
    {
        TComObject? comObject = null;
        try
        {
            // Check memory pressure before expensive COM operations
            if (MemoryMonitor.IsMemoryPressureHigh(logger, 300))
            {
                logger.LogWarning("High memory pressure detected before COM operation {OperationName}, performing cleanup", operationName);
                MemoryMonitor.PerformMemoryCleanup(logger, forceFullCollection: false);
            }

            comObject = getComObject();
            SafeComOperation(() => operation(comObject), logger, operationName);
        }
        finally
        {
            SafeReleaseComObject(comObject, logger, typeof(TComObject).Name);
        }
    }

    /// <summary>
    /// Determines if an HRESULT represents a retry-able error.
    /// </summary>
    /// <param name="hresult">The HRESULT to check.</param>
    /// <returns>True if the error is retry-able, false otherwise.</returns>
    public static bool IsRetryableComError(int hresult)
    {
        return hresult switch
        {
            unchecked((int)0x800401E3) => true, // MK_E_UNAVAILABLE
            unchecked((int)0x80010001) => true, // RPC_E_CALL_REJECTED
            unchecked((int)0x80010105) => true, // RPC_E_SERVERCALL_RETRYLATER
            unchecked((int)0x8001010A) => true, // RPC_E_SERVERCALL_REJECTED
            unchecked((int)0x80070005) => true, // E_ACCESSDENIED (sometimes transient)
            unchecked((int)0x80004005) => true, // E_FAIL (general failure, might be transient)
            _ => false
        };
    }

    /// <summary>
    /// Gets a user-friendly error message for common COM HRESULTs.
    /// </summary>
    /// <param name="hresult">The HRESULT to interpret.</param>
    /// <returns>A user-friendly error message.</returns>
    public static string GetComErrorDescription(int hresult)
    {
        return hresult switch
        {
            unchecked((int)0x800401E3) => "Visual Studio instance is temporarily unavailable",
            unchecked((int)0x80010001) => "Visual Studio rejected the call (may be busy)",
            unchecked((int)0x80010105) => "Visual Studio server requested retry later",
            unchecked((int)0x8001010A) => "Visual Studio server call was rejected",
            unchecked((int)0x80070005) => "Access denied to Visual Studio instance",
            unchecked((int)0x80004005) => "General COM failure",
            unchecked((int)0x80040154) => "Visual Studio COM class not registered",
            _ => $"COM error with HRESULT 0x{hresult:X8}"
        };
    }

    /// <summary>
    /// Executes a COM operation with retry logic for transient failures.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The COM operation to execute.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="maxRetries">Maximum number of retries (default: 3).</param>
    /// <param name="retryDelay">Delay between retries in milliseconds (default: 100).</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> RetryComOperationAsync<T>(
        Func<T> operation,
        ILogger logger,
        string operationName,
        int maxRetries = 3,
        int retryDelay = 100)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return SafeComOperation(operation, logger, operationName);
            }
            catch (ComInteropException ex) when (attempt < maxRetries && ex.InnerException is COMException comEx && IsRetryableComError(comEx.HResult))
            {
                attempt++;
                logger.LogWarning("COM operation {OperationName} failed with retryable error on attempt {Attempt}/{MaxRetries}: {Message}",
                    operationName, attempt, maxRetries + 1, ex.Message);

                if (attempt <= maxRetries)
                {
                    await Task.Delay(retryDelay).ConfigureAwait(false);
                    retryDelay *= 2; // Exponential backoff
                    continue;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Executes a COM operation with timeout support.
    /// </summary>
    /// <typeparam name="T">The return type of the operation.</typeparam>
    /// <param name="operation">The COM operation to execute.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 30 seconds).</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<T> SafeComOperationWithTimeoutAsync<T>(
        Func<T> operation,
        ILogger logger,
        string operationName,
        int timeoutMs = 30000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        
        try
        {
            return await Task.Run(() => SafeComOperation(operation, logger, operationName), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            logger.LogError("COM operation {OperationName} timed out after {TimeoutMs}ms", operationName, timeoutMs);
            throw new ComInteropException($"COM operation '{operationName}' timed out after {timeoutMs}ms");
        }
    }

    /// <summary>
    /// Executes a COM operation with automatic COM object cleanup and timeout.
    /// </summary>
    /// <typeparam name="TComObject">The type of COM object to manage.</typeparam>
    /// <typeparam name="TResult">The return type of the operation.</typeparam>
    /// <param name="getComObject">Function to get the COM object.</param>
    /// <param name="operation">Operation to perform with the COM object.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="operationName">Name of the operation for logging.</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default: 30 seconds).</param>
    /// <returns>The result of the operation.</returns>
    public static async Task<TResult> WithComObjectTimeoutAsync<TComObject, TResult>(
        Func<TComObject> getComObject,
        Func<TComObject, TResult> operation,
        ILogger logger,
        string operationName,
        int timeoutMs = 30000) where TComObject : class
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        
        try
        {
            return await Task.Run(() => WithComObject(getComObject, operation, logger, operationName), cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            logger.LogError("COM operation {OperationName} with object {ObjectType} timed out after {TimeoutMs}ms", 
                operationName, typeof(TComObject).Name, timeoutMs);
            throw new ComInteropException($"COM operation '{operationName}' with {typeof(TComObject).Name} timed out after {timeoutMs}ms");
        }
    }
}