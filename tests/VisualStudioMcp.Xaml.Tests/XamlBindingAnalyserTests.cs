using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class XamlBindingAnalyserTests
{
    private Mock<ILogger<XamlBindingAnalyser>> _mockLogger;
    private Mock<XamlParser> _mockParser;
    private XamlBindingAnalyser _analyser;
    private string _testXamlFilePath;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<XamlBindingAnalyser>>();
        var mockParserLogger = new Mock<ILogger<XamlParser>>();
        _mockParser = new Mock<XamlParser>(mockParserLogger.Object);
        _analyser = new XamlBindingAnalyser(_mockLogger.Object, _mockParser.Object);
        _testXamlFilePath = Path.Combine(Path.GetTempPath(), $"test_binding_{Guid.NewGuid():N}.xaml");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testXamlFilePath))
        {
            File.Delete(_testXamlFilePath);
        }
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_XamlWithDataBindings_ReturnsBindingInfo()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Name}" />
                <Button Content="{Binding ClickCommand}" />
                <Label Content="{StaticResource MyResource}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.AreEqual(3, bindings.Length);
        
        var dataBinding = bindings.FirstOrDefault(b => b.BindingType == "Binding");
        Assert.IsNotNull(dataBinding);
        Assert.AreEqual("Name", dataBinding.Path);

        var resourceBinding = bindings.FirstOrDefault(b => b.BindingType == "StaticResource");
        Assert.IsNotNull(resourceBinding);
        Assert.AreEqual("MyResource", resourceBinding.ResourceKey);
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_ComplexBindings_ExtractsAllProperties()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Path=PersonName, Mode=TwoWay, Converter={StaticResource NameConverter}, UpdateSourceTrigger=PropertyChanged}" />
                <Label Content="{Binding ElementName=MyElement, Path=Text}" />
                <Button IsEnabled="{Binding IsEnabled, FallbackValue=True, TargetNullValue=False}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.IsTrue(bindings.Length >= 3);

        var complexBinding = bindings.FirstOrDefault(b => b.Path == "PersonName");
        Assert.IsNotNull(complexBinding);
        Assert.AreEqual("TwoWay", complexBinding.Properties.GetValueOrDefault("Mode", ""));
        Assert.AreEqual("PropertyChanged", complexBinding.Properties.GetValueOrDefault("UpdateSourceTrigger", ""));

        var elementBinding = bindings.FirstOrDefault(b => b.Properties.ContainsKey("ElementName"));
        Assert.IsNotNull(elementBinding);
        Assert.AreEqual("MyElement", elementBinding.Properties["ElementName"]);

        var fallbackBinding = bindings.FirstOrDefault(b => b.Properties.ContainsKey("FallbackValue"));
        Assert.IsNotNull(fallbackBinding);
        Assert.AreEqual("True", fallbackBinding.Properties["FallbackValue"]);
        Assert.AreEqual("False", fallbackBinding.Properties["TargetNullValue"]);
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_DifferentBindingTypes_IdentifiesCorrectly()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <TextBox Text="{Binding Name}" />
                <Button Content="{x:Bind ViewModel.Command}" />
                <Label Background="{StaticResource BlueBrush}" />
                <TextBlock Foreground="{DynamicResource TextColor}" />
                <Border DataContext="{Binding RelativeSource={RelativeSource Self}}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.IsTrue(bindings.Length >= 5);

        Assert.IsTrue(bindings.Any(b => b.BindingType == "Binding"));
        Assert.IsTrue(bindings.Any(b => b.BindingType == "x:Bind"));
        Assert.IsTrue(bindings.Any(b => b.BindingType == "StaticResource"));
        Assert.IsTrue(bindings.Any(b => b.BindingType == "DynamicResource"));
        Assert.IsTrue(bindings.Any(b => b.BindingType == "RelativeSource"));
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_NonExistentFile_ReturnsEmptyArray()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent.xaml");

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(nonExistentPath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.AreEqual(0, bindings.Length);
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_UnsafeFilePath_ReturnsEmptyArray()
    {
        // Arrange
        var unsafePath = "../../../etc/passwd";

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(unsafePath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.AreEqual(0, bindings.Length);
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_ValidBindings_ReturnsNoErrors()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Name}" />
                <Label Content="{StaticResource ValidResource}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        Assert.IsTrue(validationResults.Length > 0);
        Assert.IsFalse(validationResults.Any(r => r.Severity == ValidationSeverity.Error));
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_InvalidBindings_ReturnsErrors()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding}" />
                <Label Content="{StaticResource }" />
                <Button IsEnabled="{Binding Path=IsValid, Convertor=MyConverter}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        Assert.IsTrue(validationResults.Length > 0);
        
        // Should have warnings for missing path and missing resource key
        Assert.IsTrue(validationResults.Any(r => r.Severity >= ValidationSeverity.Warning));
        
        // Should have error for typo in "Convertor"
        Assert.IsTrue(validationResults.Any(r => r.Severity == ValidationSeverity.Error && 
                                                 r.Messages.Any(m => m.Contains("typo"))));
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_PerformanceIssues_ReturnsWarnings()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding Name, Mode=TwoWay}" />
                <Label Content="{Binding VeryLongPropertyNameThatGoesOnAndOnAndOnAndMakesTheBindingExpressionReallyLongAndDifficultToReadAndMaintain}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        Assert.IsTrue(validationResults.Any(r => r.Severity == ValidationSeverity.Warning && 
                                                 r.Messages.Any(m => m.Contains("TwoWay") || m.Contains("long"))));
    }

    [TestMethod]
    public async Task FindElementsWithBindingsAsync_ReturnsElementsWithBindings()
    {
        // Arrange
        var xamlElements = new[]
        {
            new XamlElement
            {
                ElementType = "TextBox",
                Properties = new Dictionary<string, string> { { "Text", "{Binding Name}" } }
            },
            new XamlElement
            {
                ElementType = "Label",
                Properties = new Dictionary<string, string> { { "Content", "Static Text" } }
            },
            new XamlElement
            {
                ElementType = "Button",
                Properties = new Dictionary<string, string> { { "Command", "{Binding ClickCommand}" } }
            }
        };

        _mockParser.Setup(p => p.ParseVisualTreeAsync(_testXamlFilePath))
                  .ReturnsAsync(xamlElements);

        // Act
        var elementsWithBindings = await _analyser.FindElementsWithBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(elementsWithBindings);
        Assert.AreEqual(2, elementsWithBindings.Length); // TextBox and Button have bindings
        Assert.IsTrue(elementsWithBindings.All(e => e.ElementType == "TextBox" || e.ElementType == "Button"));
    }

    [TestMethod]
    public async Task GetBindingStatisticsAsync_ReturnsAccurateStatistics()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <TextBox Text="{Binding Name}" />
                <Button Command="{x:Bind ViewModel.Command}" />
                <Label Background="{StaticResource BlueBrush}" />
                <TextBlock Foreground="{DynamicResource TextColor}" />
                <Border DataContext="{Binding RelativeSource={RelativeSource Self}}" />
                <TextBox Text="{Binding InvalidPath, Convertor=WrongName}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var statistics = await _analyser.GetBindingStatisticsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(statistics);
        Assert.AreEqual(_testXamlFilePath, statistics.FilePath);
        Assert.IsTrue(statistics.TotalBindings >= 6);
        Assert.IsTrue(statistics.DataBindings >= 2); // Binding and x:Bind
        Assert.IsTrue(statistics.StaticResourceBindings >= 1);
        Assert.IsTrue(statistics.DynamicResourceBindings >= 1);
        Assert.IsTrue(statistics.RelativeSourceBindings >= 1);
        Assert.IsTrue(statistics.ValidationErrors > 0); // Should have error for "Convertor" typo
    }

    [TestMethod]
    public async Task GetBindingStatisticsAsync_EmptyFile_ReturnsZeroStatistics()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Label Content="No bindings here" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var statistics = await _analyser.GetBindingStatisticsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(statistics);
        Assert.AreEqual(_testXamlFilePath, statistics.FilePath);
        Assert.AreEqual(0, statistics.TotalBindings);
        Assert.AreEqual(0, statistics.DataBindings);
        Assert.AreEqual(0, statistics.ValidationErrors);
        Assert.AreEqual(0, statistics.ValidationWarnings);
    }

    [TestMethod]
    public async Task AnalyseDataBindingsAsync_IncludesLineNumbers()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox Text="{Binding FirstProperty}" />
                <Label Content="Static text" />
                <Button Command="{Binding SecondProperty}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var bindings = await _analyser.AnalyseDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(bindings);
        Assert.IsTrue(bindings.Length >= 2);
        Assert.IsTrue(bindings.All(b => b.LineNumber > 0));
        
        var firstBinding = bindings.FirstOrDefault(b => b.Path == "FirstProperty");
        var secondBinding = bindings.FirstOrDefault(b => b.Path == "SecondProperty");
        
        Assert.IsNotNull(firstBinding);
        Assert.IsNotNull(secondBinding);
        Assert.IsTrue(firstBinding.LineNumber < secondBinding.LineNumber);
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockParserLogger = new Mock<ILogger<XamlParser>>();
        var parser = new Mock<XamlParser>(mockParserLogger.Object);

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlBindingAnalyser(null!, parser.Object));
    }

    [TestMethod]
    public void Constructor_NullParser_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new XamlBindingAnalyser(_mockLogger.Object, null!));
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_ResourceBindingWithoutKey_ReturnsError()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Label Background="{StaticResource}" />
                <TextBlock Foreground="{DynamicResource }" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        Assert.IsTrue(validationResults.Any(r => r.Severity == ValidationSeverity.Error && 
                                                 r.Messages.Any(m => m.Contains("missing a resource key"))));
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_ResourceKeyWithSpaces_ReturnsWarning()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Label Background="{StaticResource My Resource Key}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        Assert.IsTrue(validationResults.Any(r => r.Severity == ValidationSeverity.Warning && 
                                                 r.Messages.Any(m => m.Contains("contains spaces"))));
    }

    [TestMethod]
    public async Task ValidateDataBindingsAsync_RelativeSourceBinding_ValidatesCorrectly()
    {
        // Arrange
        var xamlContent = """
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <TextBox DataContext="{Binding RelativeSource={RelativeSource Self}}" />
                <Label Content="{Binding RelativeSource={RelativeSource InvalidType}}" />
            </Grid>
            """;
        await File.WriteAllTextAsync(_testXamlFilePath, xamlContent);

        // Act
        var validationResults = await _analyser.ValidateDataBindingsAsync(_testXamlFilePath);

        // Assert
        Assert.IsNotNull(validationResults);
        
        // Should have no errors for valid RelativeSource
        var validRelativeSource = validationResults.Where(r => r.Binding.Expression.Contains("Self"));
        Assert.IsTrue(validRelativeSource.All(r => r.Severity <= ValidationSeverity.Info));
        
        // Should have warning for invalid RelativeSource
        var invalidRelativeSource = validationResults.Where(r => r.Binding.Expression.Contains("InvalidType"));
        Assert.IsTrue(invalidRelativeSource.Any(r => r.Severity == ValidationSeverity.Warning));
    }
}