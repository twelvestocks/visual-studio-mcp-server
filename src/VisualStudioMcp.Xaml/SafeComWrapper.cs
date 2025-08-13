using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using EnvDTE;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// RAII wrapper for COM objects that ensures proper cleanup via Marshal.ReleaseComObject.
/// Prevents COM object leaks and ensures deterministic resource management.
/// </summary>
/// <typeparam name="T">Type of COM object to wrap (must be a COM interface)</typeparam>
public sealed class SafeComWrapper<T> : IDisposable where T : class
{
    private T? _comObject;
    private readonly ILogger? _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the wrapped COM object. Throws if disposed.
    /// </summary>
    public T ComObject
    {
        get
        {
            ThrowIfDisposed();
            return _comObject!;
        }
    }

    /// <summary>
    /// Gets whether the wrapper has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes a new SafeComWrapper with the specified COM object.
    /// </summary>
    /// <param name="comObject">The COM object to wrap.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public SafeComWrapper(T comObject, ILogger? logger = null)
    {
        _comObject = comObject ?? throw new ArgumentNullException(nameof(comObject));
        _logger = logger;
    }

    /// <summary>
    /// Creates a SafeComWrapper from a COM object, with null safety.
    /// </summary>
    /// <param name="comObject">The COM object to wrap, may be null.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A SafeComWrapper if comObject is not null, otherwise null.</returns>
    public static SafeComWrapper<T>? Create(T? comObject, ILogger? logger = null)
    {
        return comObject != null ? new SafeComWrapper<T>(comObject, logger) : null;
    }

    /// <summary>
    /// Executes an action with the COM object, ensuring proper exception handling.
    /// </summary>
    /// <param name="action">Action to execute with the COM object.</param>
    public void Execute(Action<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        ThrowIfDisposed();
        
        try
        {
            action(_comObject!);
        }
        catch (COMException ex)
        {
            _logger?.LogWarning(ex, "COM exception during operation on {ComObjectType}", typeof(T).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected exception during operation on {ComObjectType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Executes a function with the COM object and returns the result.
    /// </summary>
    /// <typeparam name="TResult">Type of the result.</typeparam>
    /// <param name="func">Function to execute with the COM object.</param>
    /// <returns>The result of the function.</returns>
    public TResult Execute<TResult>(Func<T, TResult> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        ThrowIfDisposed();

        try
        {
            return func(_comObject!);
        }
        catch (COMException ex)
        {
            _logger?.LogWarning(ex, "COM exception during operation on {ComObjectType}", typeof(T).Name);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected exception during operation on {ComObjectType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Disposes the COM object and releases the COM reference.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && _comObject != null)
        {
            try
            {
                // Release the COM object reference
                var releaseCount = Marshal.ReleaseComObject(_comObject);
                _logger?.LogDebug("Released COM object {ComObjectType}, final reference count: {ReferenceCount}",
                    typeof(T).Name, releaseCount);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error releasing COM object {ComObjectType}", typeof(T).Name);
            }
            finally
            {
                _comObject = null;
                _disposed = true;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SafeComWrapper<T>), 
                $"COM object {typeof(T).Name} has been disposed");
        }
    }

    /// <summary>
    /// Finalizer to ensure COM object is released even if Dispose is not called.
    /// </summary>
    ~SafeComWrapper()
    {
        Dispose(false);
    }
}

/// <summary>
/// Specialized safe wrapper for DTE objects with additional convenience methods.
/// </summary>
public sealed class SafeDteWrapper : IDisposable
{
    private readonly SafeComWrapper<DTE> _dteWrapper;
    private readonly ILogger? _logger;

    /// <summary>
    /// Gets the wrapped DTE object.
    /// </summary>
    public DTE Dte => _dteWrapper.ComObject;

    /// <summary>
    /// Gets whether the wrapper has been disposed.
    /// </summary>
    public bool IsDisposed => _dteWrapper.IsDisposed;

    /// <summary>
    /// Initializes a new SafeDteWrapper.
    /// </summary>
    /// <param name="dte">The DTE object to wrap.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public SafeDteWrapper(DTE dte, ILogger? logger = null)
    {
        _dteWrapper = new SafeComWrapper<DTE>(dte, logger);
        _logger = logger;
    }

    /// <summary>
    /// Creates a SafeDteWrapper from a DTE object, with null safety.
    /// </summary>
    /// <param name="dte">The DTE object to wrap, may be null.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>A SafeDteWrapper if dte is not null, otherwise null.</returns>
    public static SafeDteWrapper? Create(DTE? dte, ILogger? logger = null)
    {
        return dte != null ? new SafeDteWrapper(dte, logger) : null;
    }

    /// <summary>
    /// Safely gets the solution from the DTE object.
    /// </summary>
    /// <returns>A safe wrapper around the Solution object, or null if not available.</returns>
    public SafeComWrapper<Solution>? GetSolution()
    {
        try
        {
            var solution = _dteWrapper.Execute(dte => dte.Solution);
            return SafeComWrapper<Solution>.Create(solution, _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting Solution from DTE");
            return null;
        }
    }

    /// <summary>
    /// Safely gets the active document from the DTE object.
    /// </summary>
    /// <returns>A safe wrapper around the Document object, or null if not available.</returns>
    public SafeComWrapper<Document>? GetActiveDocument()
    {
        try
        {
            var document = _dteWrapper.Execute(dte => dte.ActiveDocument);
            return SafeComWrapper<Document>.Create(document, _logger);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error getting ActiveDocument from DTE");
            return null;
        }
    }

    /// <summary>
    /// Executes an action with the DTE object.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    public void Execute(Action<DTE> action) => _dteWrapper.Execute(action);

    /// <summary>
    /// Executes a function with the DTE object and returns the result.
    /// </summary>
    /// <typeparam name="TResult">Type of the result.</typeparam>
    /// <param name="func">Function to execute.</param>
    /// <returns>The result of the function.</returns>
    public TResult Execute<TResult>(Func<DTE, TResult> func) => _dteWrapper.Execute(func);

    /// <summary>
    /// Disposes the DTE wrapper.
    /// </summary>
    public void Dispose()
    {
        _dteWrapper?.Dispose();
    }
}