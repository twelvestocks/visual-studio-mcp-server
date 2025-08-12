using System.Runtime.Serialization;

namespace VisualStudioMcp.Core;

/// <summary>
/// Exception thrown when COM interop operations fail.
/// </summary>
[Serializable]
public class ComInteropException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ComInteropException class.
    /// </summary>
    public ComInteropException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ComInteropException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ComInteropException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the ComInteropException class with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ComInteropException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets the HRESULT from the inner COM exception, if available.
    /// </summary>
    public int? HResult => InnerException?.HResult;

    /// <summary>
    /// Gets whether this exception represents a retry-able COM error.
    /// </summary>
    public bool IsRetryable => HResult.HasValue && ComInteropHelper.IsRetryableComError(HResult.Value);
}