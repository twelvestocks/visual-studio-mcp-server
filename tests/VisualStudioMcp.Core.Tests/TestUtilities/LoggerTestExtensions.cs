using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace VisualStudioMcp.Core.Tests.TestUtilities;

/// <summary>
/// Extension methods for verifying logger interactions in tests.
/// </summary>
public static class LoggerTestExtensions
{
    /// <summary>
    /// Verifies that a log message was written at the specified level containing the expected text.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="mockLogger">The mock logger to verify.</param>
    /// <param name="expectedLevel">The expected log level.</param>
    /// <param name="expectedMessage">Text that should be contained in the log message.</param>
    /// <param name="times">How many times the log should have been called (default: AtLeastOnce).</param>
    public static void VerifyLogMessage<T>(this Mock<ILogger<T>> mockLogger, LogLevel expectedLevel, string expectedMessage, Times? times = null)
    {
        mockLogger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times ?? Times.AtLeastOnce());
    }

    /// <summary>
    /// Verifies that a log message was written at the specified level with an exception.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="mockLogger">The mock logger to verify.</param>
    /// <param name="expectedLevel">The expected log level.</param>
    /// <param name="expectedMessage">Text that should be contained in the log message.</param>
    /// <param name="expectedException">The type of exception that should have been logged.</param>
    /// <param name="times">How many times the log should have been called (default: AtLeastOnce).</param>
    public static void VerifyLogMessageWithException<T>(this Mock<ILogger<T>> mockLogger, LogLevel expectedLevel, string expectedMessage, Type expectedException, Times? times = null)
    {
        mockLogger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase)),
                It.Is<Exception?>(ex => ex != null && expectedException.IsAssignableFrom(ex.GetType())),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times ?? Times.AtLeastOnce());
    }

    /// <summary>
    /// Verifies that no log messages were written at or above the specified level.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="mockLogger">The mock logger to verify.</param>
    /// <param name="minimumLevel">The minimum log level that should not have been logged.</param>
    public static void VerifyNoLogsAtOrAboveLevel<T>(this Mock<ILogger<T>> mockLogger, LogLevel minimumLevel)
    {
        var levelsToCheck = new[]
        {
            LogLevel.Critical,
            LogLevel.Error,
            LogLevel.Warning,
            LogLevel.Information,
            LogLevel.Debug,
            LogLevel.Trace
        }.Where(level => level >= minimumLevel);

        foreach (var level in levelsToCheck)
        {
            mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never,
                $"No logs should have been written at {level} level");
        }
    }

    /// <summary>
    /// Verifies that a specific number of log entries were made at the specified level.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="mockLogger">The mock logger to verify.</param>
    /// <param name="expectedLevel">The expected log level.</param>
    /// <param name="expectedCount">The expected number of log entries.</param>
    public static void VerifyLogCount<T>(this Mock<ILogger<T>> mockLogger, LogLevel expectedLevel, int expectedCount)
    {
        mockLogger.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(expectedCount),
            $"Expected exactly {expectedCount} log entries at {expectedLevel} level");
    }

    /// <summary>
    /// Creates a fluent assertion builder for more complex log verification scenarios.
    /// </summary>
    /// <typeparam name="T">The logger category type.</typeparam>
    /// <param name="mockLogger">The mock logger to verify.</param>
    /// <returns>A fluent log assertion builder.</returns>
    public static LogAssertionBuilder<T> ShouldHaveLogged<T>(this Mock<ILogger<T>> mockLogger)
    {
        return new LogAssertionBuilder<T>(mockLogger);
    }
}

/// <summary>
/// Fluent builder for complex log assertions.
/// </summary>
/// <typeparam name="T">The logger category type.</typeparam>
public class LogAssertionBuilder<T>
{
    private readonly Mock<ILogger<T>> _mockLogger;
    private LogLevel? _level;
    private string? _messageContains;
    private Type? _exceptionType;
    private Times? _times;

    internal LogAssertionBuilder(Mock<ILogger<T>> mockLogger)
    {
        _mockLogger = mockLogger;
    }

    /// <summary>
    /// Specifies the expected log level.
    /// </summary>
    public LogAssertionBuilder<T> AtLevel(LogLevel level)
    {
        _level = level;
        return this;
    }

    /// <summary>
    /// Specifies text that should be contained in the log message.
    /// </summary>
    public LogAssertionBuilder<T> WithMessage(string messageContains)
    {
        _messageContains = messageContains;
        return this;
    }

    /// <summary>
    /// Specifies the type of exception that should be logged.
    /// </summary>
    public LogAssertionBuilder<T> WithException<TException>() where TException : Exception
    {
        _exceptionType = typeof(TException);
        return this;
    }

    /// <summary>
    /// Specifies how many times the log should have occurred.
    /// </summary>
    public LogAssertionBuilder<T> Times(Times times)
    {
        _times = times;
        return this;
    }

    /// <summary>
    /// Performs the log verification.
    /// </summary>
    public void Verify()
    {
        if (_level == null)
            throw new InvalidOperationException("Log level must be specified using AtLevel()");

        if (_exceptionType != null)
        {
            _mockLogger.VerifyLogMessageWithException(_level.Value, _messageContains ?? string.Empty, _exceptionType, _times);
        }
        else
        {
            _mockLogger.VerifyLogMessage(_level.Value, _messageContains ?? string.Empty, _times);
        }
    }
}