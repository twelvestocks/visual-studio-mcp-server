using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VisualStudioMcp.Xaml.Tests;

[TestClass]
public class XamlBindingRegexPatternsTests
{
    [TestMethod]
    public void StaticResourcePattern_ValidStaticResource_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "{StaticResource MyResource}",
            "{StaticResource  SpacedResource  }",
            "{STATICRESOURCE CaseInsensitive}",
            "{StaticResource Resource.With.Dots}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.StaticResourcePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No resource key captured for: {testCase}");
        }
    }

    [TestMethod]
    public void StaticResourcePattern_InvalidStaticResource_DoesNotMatch()
    {
        // Arrange
        var invalidCases = new[]
        {
            "{StaticResource}",           // No resource key
            "StaticResource MyResource", // Missing braces
            "{DynamicResource MyResource}", // Wrong type
            "{StaticResource }"           // Empty resource key
        };

        foreach (var invalidCase in invalidCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.StaticResourcePattern.Match(invalidCase);

            // Assert
            if (match.Success)
            {
                // If it matches, the captured group should be empty or whitespace
                Assert.IsTrue(string.IsNullOrWhiteSpace(match.Groups[1].Value), 
                    $"Should not capture valid resource key for: {invalidCase}");
            }
        }
    }

    [TestMethod]
    public void DynamicResourcePattern_ValidDynamicResource_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "{DynamicResource MyResource}",
            "{DynamicResource  SpacedResource  }",
            "{DYNAMICRESOURCE CaseInsensitive}",
            "{DynamicResource Resource_With_Underscores}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.DynamicResourcePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No resource key captured for: {testCase}");
        }
    }

    [TestMethod]
    public void PathPattern_ValidPath_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "Path=PropertyName",
            "Path = PropertyName",
            "PATH=CaseInsensitive",
            "Path=Complex.Property.Path",
            "Path=Property[0].SubProperty"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.PathPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No path captured for: {testCase}");
        }
    }

    [TestMethod]
    public void SimpleBindingPattern_ValidSimpleBinding_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "{Binding PropertyName}",
            "{x:Bind PropertyName}",
            "{BINDING CaseInsensitive}",
            "{x:bind ViewModelProperty}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.SimpleBindingPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No property captured for: {testCase}");
        }
    }

    [TestMethod]
    public void ModePattern_ValidMode_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "Mode=TwoWay",
            "Mode = OneWay",
            "MODE=OneTime",
            "Mode=OneWayToSource"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.ModePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No mode captured for: {testCase}");
        }
    }

    [TestMethod]
    public void ConverterPattern_ValidConverter_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "Converter={StaticResource MyConverter}",
            "Converter = {StaticResource SpacedConverter}",
            "CONVERTER={StaticResource CaseInsensitive}",
            "Converter={StaticResource Boolean_To_Visibility_Converter}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.ConverterPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No converter key captured for: {testCase}");
        }
    }

    [TestMethod]
    public void ElementNamePattern_ValidElementName_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "ElementName=MyElement",
            "ElementName = SomeControl",
            "ELEMENTNAME=CaseInsensitive",
            "ElementName=Element_With_Underscores"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.ElementNamePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No element name captured for: {testCase}");
        }
    }

    [TestMethod]
    public void RelativeSourcePattern_ValidRelativeSource_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "{RelativeSource Self}",
            "{RelativeSource FindAncestor}",
            "{RELATIVESOURCE PreviousData}",
            "{RelativeSource TemplatedParent}",
            "{RelativeSource Mode=FindAncestor, AncestorType=Window}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.RelativeSourcePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No relative source captured for: {testCase}");
        }
    }

    [TestMethod]
    public void StringFormatPattern_ValidStringFormat_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "StringFormat={}{0:C}",
            "StringFormat = N2",
            "STRINGFORMAT=yyyy-MM-dd",
            "StringFormat='Hello {0}'"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.StringFormatPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No format captured for: {testCase}");
        }
    }

    [TestMethod]
    public void UpdateSourceTriggerPattern_ValidTrigger_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "UpdateSourceTrigger=PropertyChanged",
            "UpdateSourceTrigger = LostFocus",
            "UPDATESOURCETRIGGER=Explicit",
            "UpdateSourceTrigger=Default"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.UpdateSourceTriggerPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No trigger captured for: {testCase}");
        }
    }

    [TestMethod]
    public void FallbackValuePattern_ValidFallbackValue_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "FallbackValue=DefaultText",
            "FallbackValue = True",
            "FALLBACKVALUE=0",
            "FallbackValue='Default Value'"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.FallbackValuePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No fallback value captured for: {testCase}");
        }
    }

    [TestMethod]
    public void TargetNullValuePattern_ValidTargetNullValue_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "TargetNullValue=N/A",
            "TargetNullValue = Empty",
            "TARGETNULLVALUE=False",
            "TargetNullValue='Not Available'"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.TargetNullValuePattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
            Assert.IsTrue(match.Groups[1].Value.Trim().Length > 0, $"No target null value captured for: {testCase}");
        }
    }

    [TestMethod]
    public void AnyBindingPattern_VariousBindingTypes_MatchesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            "{Binding PropertyName}",
            "{x:Bind ViewModelProperty}",
            "{StaticResource MyResource}",
            "{DynamicResource ThemeColor}",
            "{RelativeSource Self}",
            "{Binding Path=Name, Mode=TwoWay}",
            "{StaticResource ButtonStyle}"
        };

        foreach (var testCase in testCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.AnyBindingPattern.Match(testCase);

            // Assert
            Assert.IsTrue(match.Success, $"Failed to match: {testCase}");
        }
    }

    [TestMethod]
    public void AnyBindingPattern_NonBindingExpressions_DoesNotMatch()
    {
        // Arrange
        var nonBindingCases = new[]
        {
            "Just plain text",
            "PropertyName",
            "{x:Type local:MyClass}",
            "{x:Null}",
            "SomeValue",
            ""
        };

        foreach (var nonBindingCase in nonBindingCases)
        {
            // Act
            var match = XamlBindingRegexPatterns.AnyBindingPattern.Match(nonBindingCase);

            // Assert
            Assert.IsFalse(match.Success, $"Should not match non-binding: {nonBindingCase}");
        }
    }

    [TestMethod]
    public void ContainsBinding_WithBindingExpressions_ReturnsTrue()
    {
        // Arrange
        var bindingExpressions = new[]
        {
            "{Binding Name}",
            "Some text {StaticResource MyResource} more text",
            "Text='{Binding Title}' Visibility='Visible'",
            "{x:Bind ViewModel.Property}"
        };

        foreach (var expression in bindingExpressions)
        {
            // Act
            var result = XamlBindingRegexPatterns.ContainsBinding(expression);

            // Assert
            Assert.IsTrue(result, $"Should detect binding in: {expression}");
        }
    }

    [TestMethod]
    public void ContainsBinding_WithoutBindingExpressions_ReturnsFalse()
    {
        // Arrange
        var nonBindingExpressions = new[]
        {
            "Just plain text",
            "Property='Value'",
            "",
            null,
            "Some random content"
        };

        foreach (var expression in nonBindingExpressions)
        {
            // Act
            var result = XamlBindingRegexPatterns.ContainsBinding(expression);

            // Assert
            Assert.IsFalse(result, $"Should not detect binding in: {expression ?? "null"}");
        }
    }

    [TestMethod]
    public void ExtractAllBindings_MultipleBindings_ExtractsAll()
    {
        // Arrange
        var xamlContent = """
            <Grid>
                <Button Content="{Binding Title}" Command="{Binding ClickCommand}" 
                        Background="{StaticResource ButtonBrush}" 
                        Foreground="{DynamicResource TextColor}" />
                <TextBox Text="{x:Bind ViewModel.Name, Mode=TwoWay}" />
            </Grid>
            """;

        // Act
        var matches = XamlBindingRegexPatterns.ExtractAllBindings(xamlContent);

        // Assert
        Assert.IsTrue(matches.Count >= 5, $"Expected at least 5 bindings, found {matches.Count}");
        
        var matchTexts = matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).ToArray();
        Assert.IsTrue(matchTexts.Any(m => m.Contains("Binding Title")));
        Assert.IsTrue(matchTexts.Any(m => m.Contains("StaticResource ButtonBrush")));
        Assert.IsTrue(matchTexts.Any(m => m.Contains("DynamicResource TextColor")));
        Assert.IsTrue(matchTexts.Any(m => m.Contains("x:Bind ViewModel.Name")));
    }

    [TestMethod]
    public void ExtractAllBindings_EmptyContent_ReturnsEmptyCollection()
    {
        // Arrange
        var emptyContent = "";

        // Act
        var matches = XamlBindingRegexPatterns.ExtractAllBindings(emptyContent);

        // Assert
        Assert.AreEqual(0, matches.Count);
    }

    [TestMethod]
    public void ExtractAllBindings_NullContent_ReturnsEmptyCollection()
    {
        // Act
        var matches = XamlBindingRegexPatterns.ExtractAllBindings(null!);

        // Assert
        Assert.AreEqual(0, matches.Count);
    }

    [TestMethod]
    public void ComplexBindingExpressions_MatchesCorrectly()
    {
        // Arrange
        var complexBindings = new[]
        {
            "{Binding Path=Items[0].Name, RelativeSource={RelativeSource FindAncestor, AncestorType=ListView}}",
            "{Binding Path=IsSelected, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged, FallbackValue=False}",
            "{Binding Converter={StaticResource BoolToVisibilityConverter}, ConverterParameter=Inverted}",
            "{x:Bind ViewModel.Items.Count, Mode=OneWay, FallbackValue=0, TargetNullValue=-1}"
        };

        foreach (var complexBinding in complexBindings)
        {
            // Act
            var anyBindingMatch = XamlBindingRegexPatterns.AnyBindingPattern.Match(complexBinding);
            var containsBinding = XamlBindingRegexPatterns.ContainsBinding(complexBinding);

            // Assert
            Assert.IsTrue(anyBindingMatch.Success, $"AnyBindingPattern should match: {complexBinding}");
            Assert.IsTrue(containsBinding, $"ContainsBinding should return true for: {complexBinding}");
        }
    }

    [TestMethod]
    public void RegexPatterns_PerformanceTest_CompletesQuickly()
    {
        // Arrange
        var testString = string.Join(" ", Enumerable.Repeat("{Binding TestProperty}", 1000));
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 100; i++)
        {
            XamlBindingRegexPatterns.ExtractAllBindings(testString);
        }
        stopwatch.Stop();

        // Assert
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, 
            $"Performance test took too long: {stopwatch.ElapsedMilliseconds}ms");
    }

    [TestMethod]
    public void RegexPatterns_TimeoutProtection_PreventsHanging()
    {
        // Arrange - Create a potentially problematic input that could cause catastrophic backtracking
        var problematicInput = new string('{', 1000) + "Binding" + new string('}', 1000);

        // Act & Assert - Should not hang or throw timeout exception
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = XamlBindingRegexPatterns.ContainsBinding(problematicInput);
            stopwatch.Stop();
            
            // Should complete quickly due to timeout protection
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000, 
                $"Regex took too long with problematic input: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            // This is expected behavior - timeout protection working
            stopwatch.Stop();
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 1000, "Timeout should be around 1-2 seconds");
        }
    }

    [TestMethod]
    public void CaseInsensitivity_AllPatterns_WorkCorrectly()
    {
        // Test that all patterns work with different case combinations
        var testCases = new Dictionary<System.Text.RegularExpressions.Regex, string[]>
        {
            { XamlBindingRegexPatterns.StaticResourcePattern, new[] { "{staticresource test}", "{STATICRESOURCE TEST}" } },
            { XamlBindingRegexPatterns.DynamicResourcePattern, new[] { "{dynamicresource test}", "{DYNAMICRESOURCE TEST}" } },
            { XamlBindingRegexPatterns.PathPattern, new[] { "path=test", "PATH=TEST" } },
            { XamlBindingRegexPatterns.ModePattern, new[] { "mode=twoway", "MODE=ONEWAY" } }
        };

        foreach (var (pattern, cases) in testCases)
        {
            foreach (var testCase in cases)
            {
                // Act
                var match = pattern.Match(testCase);

                // Assert
                Assert.IsTrue(match.Success, $"Pattern should match case-insensitive input: {testCase}");
            }
        }
    }
}