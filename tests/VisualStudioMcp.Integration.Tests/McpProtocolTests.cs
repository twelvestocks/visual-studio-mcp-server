using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VisualStudioMcp.Shared.Models;

namespace VisualStudioMcp.Integration.Tests;

[TestClass]
public class McpProtocolTests
{
    [TestMethod]
    public void McpToolResult_CreateSuccess_ReturnsValidResult()
    {
        // Arrange
        var data = new { message = "test", count = 42 };

        // Act
        var result = McpToolResult.CreateSuccess(data);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNull(result.ErrorCode);
        Assert.IsNotNull(result.Data);
    }

    [TestMethod]
    public void McpToolResult_CreateError_ReturnsValidErrorResult()
    {
        // Arrange
        const string message = "Test error";
        const string code = "TEST_ERROR";
        const string details = "Test error details";

        // Act
        var result = McpToolResult.CreateError(message, code, details);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Success);
        Assert.AreEqual(message, result.ErrorMessage);
        Assert.AreEqual(code, result.ErrorCode);
        Assert.AreEqual(details, result.ErrorDetails);
    }

    [TestMethod]
    public void VisualStudioInstance_Serialization_WorksCorrectly()
    {
        // Arrange
        var instance = new VisualStudioInstance
        {
            ProcessId = 1234,
            Version = "17.8.0",
            SolutionName = "TestSolution",
            StartTime = DateTime.UtcNow,
            IsConnected = true
        };

        // Act
        var json = JsonSerializer.Serialize(instance);
        var deserialized = JsonSerializer.Deserialize<VisualStudioInstance>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(instance.ProcessId, deserialized.ProcessId);
        Assert.AreEqual(instance.Version, deserialized.Version);
        Assert.AreEqual(instance.SolutionName, deserialized.SolutionName);
        Assert.AreEqual(instance.IsConnected, deserialized.IsConnected);
    }

    [TestMethod]
    public void SolutionInfo_Serialization_WorksCorrectly()
    {
        // Arrange
        var solution = new SolutionInfo
        {
            FullPath = "C:\\Test\\Solution.sln",
            Name = "TestSolution",
            IsOpen = true,
            Projects = new[]
            {
                new ProjectInfo
                {
                    Name = "Project1",
                    ProjectType = "C#",
                    TargetFrameworks = new[] { "net8.0" }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(solution);
        var deserialized = JsonSerializer.Deserialize<SolutionInfo>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(solution.FullPath, deserialized.FullPath);
        Assert.AreEqual(solution.Name, deserialized.Name);
        Assert.AreEqual(solution.IsOpen, deserialized.IsOpen);
        Assert.AreEqual(solution.ProjectCount, deserialized.ProjectCount);
        Assert.AreEqual(1, deserialized.Projects.Length);
    }

    [TestMethod]
    public void BuildResult_Serialization_WorksCorrectly()
    {
        // Arrange
        var buildResult = new BuildResult
        {
            Success = true,
            Output = "Build succeeded",
            Configuration = "Debug",
            Duration = TimeSpan.FromSeconds(30),
            Errors = Array.Empty<BuildError>(),
            Warnings = new[]
            {
                new BuildWarning
                {
                    Message = "Test warning",
                    File = "Test.cs",
                    Line = 10,
                    Column = 5
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(buildResult);
        var deserialized = JsonSerializer.Deserialize<BuildResult>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(buildResult.Success, deserialized.Success);
        Assert.AreEqual(buildResult.Output, deserialized.Output);
        Assert.AreEqual(buildResult.Configuration, deserialized.Configuration);
        Assert.AreEqual(buildResult.ErrorCount, deserialized.ErrorCount);
        Assert.AreEqual(buildResult.WarningCount, deserialized.WarningCount);
    }

    [TestMethod]
    public void ProjectInfo_Serialization_WorksCorrectly()
    {
        // Arrange
        var project = new ProjectInfo
        {
            Name = "TestProject",
            ProjectType = "C#",
            TargetFrameworks = new[] { "net8.0" }
        };

        // Act
        var json = JsonSerializer.Serialize(project);
        var deserialized = JsonSerializer.Deserialize<ProjectInfo>(json);

        // Assert
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(project.Name, deserialized.Name);
        Assert.AreEqual(project.ProjectType, deserialized.ProjectType);
        Assert.AreEqual(project.TargetFrameworks.Length, deserialized.TargetFrameworks.Length);
    }

    [TestMethod]
    public void McpToolResult_JsonSerialization_HandlesComplexData()
    {
        // Arrange
        var complexData = new
        {
            instances = new[]
            {
                new VisualStudioInstance
                {
                    ProcessId = 1234,
                    Version = "17.8.0",
                    SolutionName = "Test"
                }
            },
            metadata = new
            {
                timestamp = DateTime.UtcNow,
                count = 1,
                server = "VisualStudioMcp"
            }
        };

        var result = McpToolResult.CreateSuccess(complexData);

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        Assert.IsNotNull(json);
        Assert.IsTrue(json.Contains("\"success\":true"));
        Assert.IsTrue(json.Contains("instances"));
        Assert.IsTrue(json.Contains("metadata"));
    }

    [TestMethod]
    public void ErrorModels_Serialization_WorksCorrectly()
    {
        // Arrange
        var buildError = new BuildError
        {
            Message = "Test error message",
            File = "TestFile.cs",
            Line = 42,
            Column = 10,
            Code = "CS0001"
        };

        var buildWarning = new BuildWarning
        {
            Message = "Test warning message",
            File = "TestFile.cs",
            Line = 43,
            Column = 11,
            Code = "CS0168"
        };

        // Act
        var errorJson = JsonSerializer.Serialize(buildError);
        var warningJson = JsonSerializer.Serialize(buildWarning);
        
        var deserializedError = JsonSerializer.Deserialize<BuildError>(errorJson);
        var deserializedWarning = JsonSerializer.Deserialize<BuildWarning>(warningJson);

        // Assert
        Assert.IsNotNull(deserializedError);
        Assert.AreEqual(buildError.Message, deserializedError.Message);
        Assert.AreEqual(buildError.File, deserializedError.File);
        Assert.AreEqual(buildError.Line, deserializedError.Line);
        Assert.AreEqual(buildError.Column, deserializedError.Column);
        Assert.AreEqual(buildError.Code, deserializedError.Code);

        Assert.IsNotNull(deserializedWarning);
        Assert.AreEqual(buildWarning.Message, deserializedWarning.Message);
        Assert.AreEqual(buildWarning.File, deserializedWarning.File);
        Assert.AreEqual(buildWarning.Line, deserializedWarning.Line);
        Assert.AreEqual(buildWarning.Column, deserializedWarning.Column);
        Assert.AreEqual(buildWarning.Code, deserializedWarning.Code);
    }

    [TestMethod]
    public void McpProtocol_RequestResponseFormat_ValidationPasses()
    {
        // Test basic MCP protocol format validation
        var requestFormat = new
        {
            method = "tools/call",
            @params = new
            {
                name = "vs_list_instances",
                arguments = new { }
            }
        };

        var responseFormat = new
        {
            result = McpToolResult.CreateSuccess(new { instances = Array.Empty<object>() })
        };

        // Act
        var requestJson = JsonSerializer.Serialize(requestFormat);
        var responseJson = JsonSerializer.Serialize(responseFormat);

        // Assert
        Assert.IsNotNull(requestJson);
        Assert.IsNotNull(responseJson);
        Assert.IsTrue(requestJson.Contains("method"));
        Assert.IsTrue(requestJson.Contains("params"));
        Assert.IsTrue(responseJson.Contains("result"));
    }
}