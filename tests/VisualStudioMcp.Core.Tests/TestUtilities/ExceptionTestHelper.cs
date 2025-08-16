using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VisualStudioMcp.Core.Tests.TestUtilities;

/// <summary>
/// Standardized exception testing utilities for consistent test patterns.
/// </summary>
public static class ExceptionTestHelper
{
    /// <summary>
    /// Tests that an action throws a specific exception type with optional message validation.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action that should throw the exception.</param>
    /// <param name="expectedMessageContains">Optional partial message content to validate.</param>
    /// <returns>The caught exception for further validation.</returns>
    public static TException AssertThrows<TException>(Action action, string? expectedMessageContains = null)
        where TException : Exception
    {
        try
        {
            action();
            Assert.Fail($"Expected {typeof(TException).Name} was not thrown");
            return null!; // This line will never be reached
        }
        catch (TException ex)
        {
            if (expectedMessageContains != null)
            {
                Assert.IsTrue(ex.Message.Contains(expectedMessageContains, StringComparison.OrdinalIgnoreCase),
                    $"Exception message '{ex.Message}' does not contain expected text '{expectedMessageContains}'");
            }
            return ex;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
            return null!; // This line will never be reached
        }
    }

    /// <summary>
    /// Tests that an async function throws a specific exception type with optional message validation.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="asyncAction">The async action that should throw the exception.</param>
    /// <param name="expectedMessageContains">Optional partial message content to validate.</param>
    /// <returns>The caught exception for further validation.</returns>
    public static async Task<TException> AssertThrowsAsync<TException>(Func<Task> asyncAction, string? expectedMessageContains = null)
        where TException : Exception
    {
        try
        {
            await asyncAction();
            Assert.Fail($"Expected {typeof(TException).Name} was not thrown");
            return null!; // This line will never be reached
        }
        catch (TException ex)
        {
            if (expectedMessageContains != null)
            {
                Assert.IsTrue(ex.Message.Contains(expectedMessageContains, StringComparison.OrdinalIgnoreCase),
                    $"Exception message '{ex.Message}' does not contain expected text '{expectedMessageContains}'");
            }
            return ex;
        }
        catch (Exception ex)
        {
            Assert.Fail($"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}");
            return null!; // This line will never be reached
        }
    }

    /// <summary>
    /// Tests that an action throws a specific exception type and validates specific properties.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="action">The action that should throw the exception.</param>
    /// <param name="exceptionValidator">Action to validate specific exception properties.</param>
    /// <returns>The caught exception for further validation.</returns>
    public static TException AssertThrowsWithValidation<TException>(Action action, Action<TException> exceptionValidator)
        where TException : Exception
    {
        var exception = AssertThrows<TException>(action);
        exceptionValidator(exception);
        return exception;
    }

    /// <summary>
    /// Tests that an async function throws a specific exception type and validates specific properties.
    /// </summary>
    /// <typeparam name="TException">The expected exception type.</typeparam>
    /// <param name="asyncAction">The async action that should throw the exception.</param>
    /// <param name="exceptionValidator">Action to validate specific exception properties.</param>
    /// <returns>The caught exception for further validation.</returns>
    public static async Task<TException> AssertThrowsWithValidationAsync<TException>(Func<Task> asyncAction, Action<TException> exceptionValidator)
        where TException : Exception
    {
        var exception = await AssertThrowsAsync<TException>(asyncAction);
        exceptionValidator(exception);
        return exception;
    }
}