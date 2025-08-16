using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using VisualStudioMcp.Debug;
using VisualStudioMcp.Core;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Debug.Tests;

/// <summary>
/// Basic unit tests for DebugService to validate core functionality and compilation.
/// </summary>
[TestClass]
public class BasicDebugServiceTests
{
    private Mock<ILogger<DebugService>>? _mockLogger;
    private Mock<IVisualStudioService>? _mockVsService;
    private DebugService? _debugService;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<DebugService>>();
        _mockVsService = new Mock<IVisualStudioService>();
        _debugService = new DebugService(_mockLogger.Object, _mockVsService.Object);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _debugService?.Dispose();
    }

    [TestMethod]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act & Assert
        Assert.IsNotNull(_debugService);
        Assert.IsInstanceOfType(_debugService, typeof(IDebugService));
    }

    [TestMethod]
    public async Task GetCurrentStateAsync_WhenNoInstances_ReturnsDefaultState()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var state = await _debugService!.GetCurrentStateAsync();

        // Assert
        Assert.IsNotNull(state);
        Assert.IsFalse(state.IsDebugging);
        Assert.IsFalse(state.IsPaused);
        Assert.AreEqual("Design", state.Mode);
    }

    [TestMethod]
    public async Task GetBreakpointsAsync_WhenNoInstances_ReturnsEmptyArray()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var breakpoints = await _debugService!.GetBreakpointsAsync();

        // Assert
        Assert.IsNotNull(breakpoints);
        Assert.AreEqual(0, breakpoints.Length);
    }

    [TestMethod]
    public async Task GetLocalVariablesAsync_WhenNoInstances_ReturnsEmptyArray()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var variables = await _debugService!.GetLocalVariablesAsync();

        // Assert
        Assert.IsNotNull(variables);
        Assert.AreEqual(0, variables.Length);
    }

    [TestMethod]
    public async Task GetCallStackAsync_WhenNoInstances_ReturnsEmptyArray()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var callStack = await _debugService!.GetCallStackAsync();

        // Assert
        Assert.IsNotNull(callStack);
        Assert.AreEqual(0, callStack.Length);
    }

    [TestMethod]
    public async Task StartDebuggingAsync_WhenNoInstances_ThrowsException()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act & Assert
        try
        {
            await _debugService!.StartDebuggingAsync();
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task EvaluateExpressionAsync_WhenNoDebugger_ThrowsInvalidOperation()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act & Assert
        try
        {
            await _debugService!.EvaluateExpressionAsync("someExpression");
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            Assert.AreEqual("No active Visual Studio debugger found. Ensure Visual Studio is connected.", ex.Message);
        }
    }

    [TestMethod]
    public async Task ModifyVariableAsync_WhenNoDebugger_ThrowsInvalidOperation()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act & Assert
        try
        {
            await _debugService!.ModifyVariableAsync("varName", "newValue");
            Assert.Fail("Expected InvalidOperationException");
        }
        catch (InvalidOperationException ex)
        {
            Assert.AreEqual("No active Visual Studio debugger found. Ensure Visual Studio is connected.", ex.Message);
        }
    }

    [TestMethod]
    public void Dispose_CallMultipleTimes_DoesNotThrow()
    {
        // Act & Assert - Multiple dispose calls should not throw
        _debugService!.Dispose();
        _debugService.Dispose();
        
        // Test passes if no exception is thrown
    }

    [TestMethod]
    public async Task GetVariablesFromFrameAsync_WhenNoInstances_ReturnsEmptyArray()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var variables = await _debugService!.GetVariablesFromFrameAsync(0);

        // Assert
        Assert.IsNotNull(variables);
        Assert.AreEqual(0, variables.Length);
    }

    [TestMethod]
    public async Task GetStackFrameAsync_WhenNoInstances_ReturnsNull()
    {
        // Arrange
        _mockVsService!.Setup(vs => vs.GetRunningInstancesAsync())
            .ReturnsAsync(Array.Empty<VisualStudioInstance>());

        // Act
        var frame = await _debugService!.GetStackFrameAsync(0);

        // Assert
        Assert.IsNull(frame);
    }

    [TestMethod]
    public void DebugState_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var debugState = new DebugState();

        // Act
        debugState.IsDebugging = true;
        debugState.IsPaused = false;
        debugState.Mode = "Run";
        debugState.CurrentFile = "C:\\Test\\Program.cs";
        debugState.CurrentLine = 42;

        // Assert
        Assert.IsTrue(debugState.IsDebugging);
        Assert.IsFalse(debugState.IsPaused);
        Assert.AreEqual("Run", debugState.Mode);
        Assert.AreEqual("C:\\Test\\Program.cs", debugState.CurrentFile);
        Assert.AreEqual(42, debugState.CurrentLine);
    }

    [TestMethod]
    public void Breakpoint_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var breakpoint = new Breakpoint();

        // Act
        breakpoint.Id = "bp1";
        breakpoint.File = "C:\\Test\\Program.cs";
        breakpoint.Line = 42;
        breakpoint.Condition = "counter > 10";
        breakpoint.IsEnabled = true;

        // Assert
        Assert.AreEqual("bp1", breakpoint.Id);
        Assert.AreEqual("C:\\Test\\Program.cs", breakpoint.File);
        Assert.AreEqual(42, breakpoint.Line);
        Assert.AreEqual("counter > 10", breakpoint.Condition);
        Assert.IsTrue(breakpoint.IsEnabled);
    }

    [TestMethod]
    public void Variable_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var variable = new Variable();

        // Act
        variable.Name = "counter";
        variable.Value = "42";
        variable.Type = "int";
        variable.Scope = "Local";

        // Assert
        Assert.AreEqual("counter", variable.Name);
        Assert.AreEqual("42", variable.Value);
        Assert.AreEqual("int", variable.Type);
        Assert.AreEqual("Local", variable.Scope);
    }

    [TestMethod]
    public void CallStackFrame_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var frame = new CallStackFrame();

        // Act
        frame.Method = "Main";
        frame.File = "C:\\Test\\Program.cs";
        frame.Line = 42;
        frame.Module = "TestProject";

        // Assert
        Assert.AreEqual("Main", frame.Method);
        Assert.AreEqual("C:\\Test\\Program.cs", frame.File);
        Assert.AreEqual(42, frame.Line);
        Assert.AreEqual("TestProject", frame.Module);
    }
}