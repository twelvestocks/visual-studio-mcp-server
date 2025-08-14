using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class XamlPropertyModifierTests
{
    private Mock<ILogger<XamlPropertyModifier>> _mockLogger;
    private Mock<XamlParser> _mockParser;
    private XamlPropertyModifier _modifier;
    private string _testXamlFilePath;
    private string _originalXamlContent;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<XamlPropertyModifier>>();
        var mockParserLogger = new Mock<ILogger<XamlParser>>();
        _mockParser = new Mock<XamlParser>(mockParserLogger.Object);
        _modifier = new XamlPropertyModifier(_mockLogger.Object, _mockParser.Object);
        
        _testXamlFilePath = Path.Combine(Path.GetTempPath(), $"test_modifier_{Guid.NewGuid():N}.xaml");
        _originalXamlContent = """
            <Window x:Class="TestApp.MainWindow"
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    Title="Test Window" Height="350" Width="525">
                <Grid Name="MainGrid">
                    <Button x:Name="TestButton" Content="Click Me" Width="100" Height="30"/>
                    <TextBox Name="TestTextBox" Text="Hello World" />
                    <Label Content="Test Label" />
                    <Button Name="AnotherButton" Content="Another Button" />
                </Grid>
            </Window>
            """;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testXamlFilePath))
        {
            File.Delete(_testXamlFilePath);
        }

        // Clean up any backup files
        var directory = Path.GetDirectoryName(_testXamlFilePath);
        if (directory != null)
        {
            var backupFiles = Directory.GetFiles(directory, Path.GetFileName(_testXamlFilePath) + ".backup.*");
            foreach (var backupFile in backupFiles)
            {
                try
                {
                    File.Delete(backupFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_ExistingElementAndProperty_ModifiesSuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "Content", "Modified Content");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the file was modified
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("Modified Content"));
        Assert.IsFalse(modifiedContent.Contains("Click Me"));
        
        // Verify parser cache invalidation was called
        _mockParser.Verify(p => p.InvalidateCache(_testXamlFilePath), Times.Once);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_NewProperty_AddsPropertySuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "IsEnabled", "False");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the property was added
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("IsEnabled=\"False\""));
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_NonExistentElement_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "NonExistentElement", "Content", "New Content");

        // Assert
        Assert.IsFalse(result);
        
        // Verify the file was not modified
        var content = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.AreEqual(_originalXamlContent.Trim(), content.Trim());
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_EmptyElementName_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "", "Content", "New Content");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_EmptyPropertyName_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "", "New Content");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.xaml");

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(nonExistentPath, "TestButton", "Content", "New Content");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_UnsafeFilePath_ReturnsFalse()
    {
        // Arrange
        var unsafePath = "../../../etc/passwd";

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(unsafePath, "TestButton", "Content", "New Content");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ModifyElementsByTypeAsync_ExistingElementType_ModifiesAllElements()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementsByTypeAsync(_testXamlFilePath, "Button", "IsEnabled", "False");

        // Assert
        Assert.IsTrue(result);
        
        // Verify both buttons were modified
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        var buttonCount = System.Text.RegularExpressions.Regex.Matches(modifiedContent, @"IsEnabled=""False""").Count;
        Assert.AreEqual(2, buttonCount); // Both TestButton and AnotherButton should be modified
    }

    [TestMethod]
    public async Task ModifyElementsByTypeAsync_NonExistentType_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementsByTypeAsync(_testXamlFilePath, "NonExistentType", "SomeProperty", "SomeValue");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task AddElementPropertyAsync_NewProperty_AddsSuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.AddElementPropertyAsync(_testXamlFilePath, "TestButton", "ToolTip", "This is a tooltip");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the property was added
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("ToolTip=\"This is a tooltip\""));
    }

    [TestMethod]
    public async Task AddElementPropertyAsync_ExistingProperty_DoesNotModify()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act - Try to add Content property which already exists
        var result = await _modifier.AddElementPropertyAsync(_testXamlFilePath, "TestButton", "Content", "New Content");

        // Assert
        Assert.IsTrue(result); // Should still return true even if no modifications were made
        
        // Verify the original content is preserved
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("Click Me")); // Original content should remain
        Assert.IsFalse(modifiedContent.Contains("New Content"));
    }

    [TestMethod]
    public async Task RemoveElementPropertyAsync_ExistingProperty_RemovesSuccessfully()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.RemoveElementPropertyAsync(_testXamlFilePath, "TestButton", "Width");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the property was removed
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsFalse(modifiedContent.Contains("Width=\"100\""));
        
        // Verify other properties are still there
        Assert.IsTrue(modifiedContent.Contains("Height=\"30\""));
        Assert.IsTrue(modifiedContent.Contains("Content=\"Click Me\""));
    }

    [TestMethod]
    public async Task RemoveElementPropertyAsync_NonExistentProperty_ReturnsFalse()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.RemoveElementPropertyAsync(_testXamlFilePath, "TestButton", "NonExistentProperty");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task CreateBackupAsync_ValidFile_CreatesBackup()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var backupPath = await _modifier.CreateBackupAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(backupPath);
        Assert.IsTrue(File.Exists(backupPath));
        
        // Verify backup content matches original
        var backupContent = await File.ReadAllTextAsync(backupPath);
        Assert.AreEqual(_originalXamlContent, backupContent);
        
        // Verify backup path format
        Assert.IsTrue(backupPath.StartsWith(_testXamlFilePath + ".backup."));
    }

    [TestMethod]
    public async Task CreateBackupAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.xaml");

        // Act
        var backupPath = await _modifier.CreateBackupAsync(nonExistentPath);

        // Assert
        Assert.IsNull(backupPath);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_CreatesBackupAutomatically()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "Content", "Modified Content");

        // Assert
        Assert.IsTrue(result);
        
        // Verify backup was created
        var directory = Path.GetDirectoryName(_testXamlFilePath);
        var backupFiles = Directory.GetFiles(directory!, Path.GetFileName(_testXamlFilePath) + ".backup.*");
        Assert.IsTrue(backupFiles.Length > 0);
        
        // Verify backup content is original
        var backupContent = await File.ReadAllTextAsync(backupFiles[0]);
        Assert.AreEqual(_originalXamlContent, backupContent);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_WithNamespaceProperty_HandlesCorrectly()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "x:Name", "ModifiedButton");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the namespace property was modified
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("x:Name=\"ModifiedButton\""));
        Assert.IsFalse(modifiedContent.Contains("x:Name=\"TestButton\""));
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_MultipleElementsSameName_ModifiesAll()
    {
        // Arrange
        var xamlWithDuplicateNames = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Button Name="DuplicateName" Content="First" />
                <TextBox Name="DuplicateName" Text="Second" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlWithDuplicateNames);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "DuplicateName", "IsEnabled", "False");

        // Assert
        Assert.IsTrue(result);
        
        // Verify both elements were modified
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        var enabledCount = System.Text.RegularExpressions.Regex.Matches(modifiedContent, @"IsEnabled=""False""").Count;
        Assert.AreEqual(2, enabledCount);
    }

    [TestMethod]
    public async Task ModifyElementsByTypeAsync_CaseInsensitive_ModifiesCorrectly()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);

        // Act
        var result = await _modifier.ModifyElementsByTypeAsync(_testXamlFilePath, "button", "IsEnabled", "False");

        // Assert
        Assert.IsTrue(result);
        
        // Verify buttons were modified despite case difference
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        var buttonCount = System.Text.RegularExpressions.Regex.Matches(modifiedContent, @"IsEnabled=""False""").Count;
        Assert.AreEqual(2, buttonCount);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_AtomicOperation_RollsBackOnFailure()
    {
        // Arrange
        await File.WriteAllTextAsync(_testXamlFilePath, _originalXamlContent);
        var originalModifiedTime = File.GetLastWriteTime(_testXamlFilePath);
        
        // Make file read-only to simulate failure during save
        File.SetAttributes(_testXamlFilePath, FileAttributes.ReadOnly);

        try
        {
            // Act
            var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "Content", "Modified Content");

            // Assert
            Assert.IsFalse(result);
            
            // Verify original file was not corrupted
            File.SetAttributes(_testXamlFilePath, FileAttributes.Normal);
            var content = await File.ReadAllTextAsync(_testXamlFilePath);
            Assert.AreEqual(_originalXamlContent, content);
        }
        finally
        {
            // Cleanup - remove read-only attribute
            File.SetAttributes(_testXamlFilePath, FileAttributes.Normal);
        }
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockParserLogger = new Mock<ILogger<XamlParser>>();
        var parser = new Mock<XamlParser>(mockParserLogger.Object);

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlPropertyModifier(null!, parser.Object));
    }

    [TestMethod]
    public void Constructor_NullParser_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlPropertyModifier(_mockLogger.Object, null!));
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_InvalidXml_ReturnsFalse()
    {
        // Arrange
        var invalidXaml = "<Grid><Button></Grid>"; // Mismatched tags
        await File.WriteAllTextAsync(_testXamlFilePath, invalidXaml);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "Button", "Content", "Test");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task ModifyElementPropertyAsync_PreservesFormatting_MaintainsStructure()
    {
        // Arrange
        var formattedXaml = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Button Name="TestButton" 
                        Content="Click Me"
                        Width="100"
                        Height="30" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, formattedXaml);

        // Act
        var result = await _modifier.ModifyElementPropertyAsync(_testXamlFilePath, "TestButton", "Content", "Modified");

        // Assert
        Assert.IsTrue(result);
        
        // Verify the modification was made
        var modifiedContent = await File.ReadAllTextAsync(_testXamlFilePath);
        Assert.IsTrue(modifiedContent.Contains("Content=\"Modified\""));
        
        // Verify structure is maintained (multiline attributes preserved)
        Assert.IsTrue(modifiedContent.Contains("Width=\"100\""));
        Assert.IsTrue(modifiedContent.Contains("Height=\"30\""));
    }
}