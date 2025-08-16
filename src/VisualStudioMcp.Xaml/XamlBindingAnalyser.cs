using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Handles XAML data binding analysis and validation.
/// Responsible for finding, parsing, and validating data binding expressions in XAML files.
/// </summary>
public class XamlBindingAnalyser
{
    private readonly ILogger<XamlBindingAnalyser> _logger;
    private readonly XamlParser _xamlParser;

    /// <summary>
    /// Initializes a new instance of the XamlBindingAnalyser class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    /// <param name="xamlParser">XAML parser for document access.</param>
    public XamlBindingAnalyser(ILogger<XamlBindingAnalyser> logger, XamlParser xamlParser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _xamlParser = xamlParser ?? throw new ArgumentNullException(nameof(xamlParser));
    }

    /// <summary>
    /// Analyses data bindings in a XAML file and extracts binding information.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of binding information.</returns>
    public async Task<XamlBindingInfo[]> AnalyseDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Analysing data bindings in XAML file: {FilePath}", xamlFilePath);

        try
        {
            if (!SecureXmlHelper.IsFilePathSafe(xamlFilePath))
            {
                _logger.LogWarning("Unsafe file path rejected: {FilePath}", xamlFilePath);
                return Array.Empty<XamlBindingInfo>();
            }

            if (!File.Exists(xamlFilePath))
            {
                _logger.LogWarning("XAML file not found: {FilePath}", xamlFilePath);
                return Array.Empty<XamlBindingInfo>();
            }

            var xamlContent = await File.ReadAllTextAsync(xamlFilePath).ConfigureAwait(false);
            var bindings = ExtractBindingsFromContent(xamlContent, xamlFilePath);

            _logger.LogInformation("Found {Count} data bindings in XAML file: {FilePath}", bindings.Length, xamlFilePath);
            return bindings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analysing data bindings in XAML file: {FilePath}", xamlFilePath);
            return Array.Empty<XamlBindingInfo>();
        }
    }

    /// <summary>
    /// Validates data bindings in a XAML file and returns validation results.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of validation results.</returns>
    public async Task<BindingValidationResult[]> ValidateDataBindingsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Validating data bindings in XAML file: {FilePath}", xamlFilePath);

        try
        {
            var bindings = await AnalyseDataBindingsAsync(xamlFilePath).ConfigureAwait(false);
            var validationResults = new List<BindingValidationResult>();

            foreach (var binding in bindings)
            {
                var result = ValidateBinding(binding);
                validationResults.Add(result);
            }

            var errorCount = validationResults.Count(r => r.Severity == ValidationSeverity.Error);
            var warningCount = validationResults.Count(r => r.Severity == ValidationSeverity.Warning);

            _logger.LogInformation("Binding validation complete for {FilePath}: {ErrorCount} errors, {WarningCount} warnings", 
                xamlFilePath, errorCount, warningCount);

            return validationResults.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating data bindings in XAML file: {FilePath}", xamlFilePath);
            return Array.Empty<BindingValidationResult>();
        }
    }

    /// <summary>
    /// Finds all elements in a XAML file that have data binding expressions.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of elements with bindings.</returns>
    public async Task<XamlElement[]> FindElementsWithBindingsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Finding elements with bindings in XAML file: {FilePath}", xamlFilePath);

        try
        {
            var elements = await _xamlParser.ParseVisualTreeAsync(xamlFilePath).ConfigureAwait(false);
            var elementsWithBindings = elements.Where(element =>
                element.Properties.Values.Any(XamlBindingRegexPatterns.ContainsBinding))
                .ToArray();

            _logger.LogInformation("Found {Count} elements with bindings in XAML file: {FilePath}", 
                elementsWithBindings.Length, xamlFilePath);

            return elementsWithBindings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding elements with bindings in XAML file: {FilePath}", xamlFilePath);
            return Array.Empty<XamlElement>();
        }
    }

    /// <summary>
    /// Gets statistics about binding usage in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Binding usage statistics.</returns>
    public async Task<BindingStatistics> GetBindingStatisticsAsync(string xamlFilePath)
    {
        _logger.LogInformation("Getting binding statistics for XAML file: {FilePath}", xamlFilePath);

        try
        {
            var bindings = await AnalyseDataBindingsAsync(xamlFilePath).ConfigureAwait(false);
            var validationResults = await ValidateDataBindingsAsync(xamlFilePath).ConfigureAwait(false);

            var statistics = new BindingStatistics
            {
                TotalBindings = bindings.Length,
                DataBindings = bindings.Count(b => b.BindingType == "Binding" || b.BindingType == "x:Bind"),
                StaticResourceBindings = bindings.Count(b => b.BindingType == "StaticResource"),
                DynamicResourceBindings = bindings.Count(b => b.BindingType == "DynamicResource"),
                RelativeSourceBindings = bindings.Count(b => b.BindingType == "RelativeSource"),
                ValidationErrors = validationResults.Count(r => r.Severity == ValidationSeverity.Error),
                ValidationWarnings = validationResults.Count(r => r.Severity == ValidationSeverity.Warning),
                FilePath = xamlFilePath
            };

            _logger.LogInformation("Binding statistics for {FilePath}: {TotalBindings} total, {Errors} errors, {Warnings} warnings", 
                xamlFilePath, statistics.TotalBindings, statistics.ValidationErrors, statistics.ValidationWarnings);

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting binding statistics for XAML file: {FilePath}", xamlFilePath);
            return new BindingStatistics { FilePath = xamlFilePath };
        }
    }

    /// <summary>
    /// Extracts binding expressions from XAML content.
    /// </summary>
    private XamlBindingInfo[] ExtractBindingsFromContent(string xamlContent, string filePath)
    {
        var bindings = new List<XamlBindingInfo>();
        var lines = xamlContent.Split('\n');

        // Use the compiled regex pattern for finding all bindings
        var matches = XamlBindingRegexPatterns.ExtractAllBindings(xamlContent);

        foreach (Match match in matches)
        {
            try
            {
                var binding = ParseBindingExpression(match.Value, filePath);
                if (binding != null)
                {
                    // Find line number
                    var position = match.Index;
                    var lineNumber = xamlContent.Take(position).Count(c => c == '\n') + 1;
                    binding.LineNumber = lineNumber;

                    bindings.Add(binding);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing binding expression: {Expression}", match.Value);
            }
        }

        return bindings.ToArray();
    }

    /// <summary>
    /// Parses a binding expression and extracts binding information.
    /// </summary>
    private XamlBindingInfo? ParseBindingExpression(string expression, string filePath)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return null;

        var binding = new XamlBindingInfo
        {
            Expression = expression.Trim(),
            FilePath = filePath,
            Properties = new Dictionary<string, string>()
        };

        // Determine binding type
        if (XamlBindingRegexPatterns.StaticResourcePattern.IsMatch(expression))
        {
            binding.BindingType = "StaticResource";
            var match = XamlBindingRegexPatterns.StaticResourcePattern.Match(expression);
            if (match.Success)
            {
                binding.ResourceKey = match.Groups[1].Value.Trim();
            }
        }
        else if (XamlBindingRegexPatterns.DynamicResourcePattern.IsMatch(expression))
        {
            binding.BindingType = "DynamicResource";
            var match = XamlBindingRegexPatterns.DynamicResourcePattern.Match(expression);
            if (match.Success)
            {
                binding.ResourceKey = match.Groups[1].Value.Trim();
            }
        }
        else if (XamlBindingRegexPatterns.RelativeSourcePattern.IsMatch(expression))
        {
            binding.BindingType = "RelativeSource";
            var match = XamlBindingRegexPatterns.RelativeSourcePattern.Match(expression);
            if (match.Success)
            {
                binding.Properties["RelativeSource"] = match.Groups[1].Value.Trim();
            }
        }
        else if (expression.Contains("x:Bind"))
        {
            binding.BindingType = "x:Bind";
        }
        else
        {
            binding.BindingType = "Binding";
        }

        // Extract common properties
        ExtractBindingProperties(expression, binding);

        return binding;
    }

    /// <summary>
    /// Extracts properties from a binding expression.
    /// </summary>
    private void ExtractBindingProperties(string expression, XamlBindingInfo binding)
    {
        // Extract Path
        var pathMatch = XamlBindingRegexPatterns.PathPattern.Match(expression);
        if (pathMatch.Success)
        {
            binding.Path = pathMatch.Groups[1].Value.Trim();
        }
        else
        {
            // Check for simple binding syntax
            var simpleMatch = XamlBindingRegexPatterns.SimpleBindingPattern.Match(expression);
            if (simpleMatch.Success)
            {
                binding.Path = simpleMatch.Groups[1].Value.Trim();
            }
        }

        // Extract properties that don't contain nested expressions first
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.ModePattern, "Mode", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.ElementNamePattern, "ElementName", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.SourcePattern, "Source", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.StringFormatPattern, "StringFormat", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.UpdateSourceTriggerPattern, "UpdateSourceTrigger", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.FallbackValuePattern, "FallbackValue", binding);
        ExtractSimpleProperty(expression, XamlBindingRegexPatterns.TargetNullValuePattern, "TargetNullValue", binding);
        
        // Handle Converter last as it can be a StaticResource with nested braces
        ExtractConverterProperty(expression, binding);
    }

    /// <summary>
    /// Extracts a simple property value from a binding expression.
    /// </summary>
    private void ExtractSimpleProperty(string expression, Regex pattern, string propertyName, XamlBindingInfo binding)
    {
        var match = pattern.Match(expression);
        if (match.Success)
        {
            var value = match.Groups[1].Value.Trim();
            // Clean up common artifacts
            value = value.TrimEnd(',', '}').Trim();
            binding.Properties[propertyName] = value;
        }
    }

    /// <summary>
    /// Extracts converter property, handling both simple values and StaticResource references.
    /// </summary>
    private void ExtractConverterProperty(string expression, XamlBindingInfo binding)
    {
        // First try the StaticResource pattern
        var converterResourceMatch = XamlBindingRegexPatterns.ConverterPattern.Match(expression);
        if (converterResourceMatch.Success)
        {
            binding.Properties["Converter"] = $"{{StaticResource {converterResourceMatch.Groups[1].Value.Trim()}}}";
            return;
        }

        // Then try simple converter pattern
        var simpleConverterPattern = new Regex(@"Converter\s*=\s*([^,}]+)", RegexOptions.IgnoreCase);
        var simpleMatch = simpleConverterPattern.Match(expression);
        if (simpleMatch.Success)
        {
            var value = simpleMatch.Groups[1].Value.Trim();
            value = value.TrimEnd(',', '}').Trim();
            binding.Properties["Converter"] = value;
        }
    }

    /// <summary>
    /// Validates a single binding and returns validation result.
    /// </summary>
    private BindingValidationResult ValidateBinding(XamlBindingInfo binding)
    {
        var result = new BindingValidationResult
        {
            Binding = binding,
            Severity = ValidationSeverity.Info,
            Messages = new List<string>()
        };

        try
        {
            // Validate based on binding type
            switch (binding.BindingType)
            {
                case "StaticResource":
                case "DynamicResource":
                    ValidateResourceBinding(binding, result);
                    break;
                case "Binding":
                case "x:Bind":
                    ValidateDataBinding(binding, result);
                    break;
                case "RelativeSource":
                    ValidateRelativeSourceBinding(binding, result);
                    break;
            }

            // General validation rules
            ValidateGeneralRules(binding, result);
        }
        catch (Exception ex)
        {
            result.Severity = ValidationSeverity.Error;
            result.Messages.Add($"Validation error: {ex.Message}");
            _logger.LogWarning(ex, "Error validating binding: {Expression}", binding.Expression);
        }

        return result;
    }

    /// <summary>
    /// Validates resource bindings (StaticResource/DynamicResource).
    /// </summary>
    private void ValidateResourceBinding(XamlBindingInfo binding, BindingValidationResult result)
    {
        // Check for missing resource key in multiple ways
        var hasEmptyKey = string.IsNullOrWhiteSpace(binding.ResourceKey);
        var hasEmptyPattern = binding.Expression.Contains("{StaticResource}") || 
                             binding.Expression.Contains("{StaticResource }") ||
                             binding.Expression.Contains("{DynamicResource}") ||
                             binding.Expression.Contains("{DynamicResource }");

        if (hasEmptyKey || hasEmptyPattern)
        {
            result.Severity = ValidationSeverity.Error;
            result.Messages.Add("Resource binding is missing a resource key");
        }
        else if (!string.IsNullOrWhiteSpace(binding.ResourceKey) && binding.ResourceKey.Contains(" "))
        {
            result.Severity = ValidationSeverity.Warning;
            result.Messages.Add("Resource key contains spaces, which may cause issues");
        }
    }

    /// <summary>
    /// Validates data bindings (Binding/x:Bind).
    /// </summary>
    private void ValidateDataBinding(XamlBindingInfo binding, BindingValidationResult result)
    {
        // Check for missing Path
        if (string.IsNullOrWhiteSpace(binding.Path))
        {
            result.Severity = ValidationSeverity.Warning;
            result.Messages.Add("Data binding is missing a Path property");
        }

        // Check for potential performance issues
        if (binding.Properties.TryGetValue("Mode", out var mode))
        {
            if (mode.Equals("TwoWay", StringComparison.OrdinalIgnoreCase) && 
                !binding.Properties.ContainsKey("UpdateSourceTrigger"))
            {
                result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Warning);
                result.Messages.Add("TwoWay binding without UpdateSourceTrigger may have performance implications");
            }
        }

        // Check for converter issues
        if (binding.Properties.TryGetValue("Converter", out var converter))
        {
            if (!converter.StartsWith("{StaticResource"))
            {
                result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Warning);
                result.Messages.Add("Converter should typically be referenced as StaticResource");
            }
        }
    }

    /// <summary>
    /// Validates RelativeSource bindings.
    /// </summary>
    private void ValidateRelativeSourceBinding(XamlBindingInfo binding, BindingValidationResult result)
    {
        if (binding.Properties.TryGetValue("RelativeSource", out var relativeSource))
        {
            var validRelativeSources = new[] { "Self", "FindAncestor", "PreviousData", "TemplatedParent" };
            var isValid = validRelativeSources.Any(rs => relativeSource.Contains(rs));
            
            if (!isValid)
            {
                result.Severity = ValidationSeverity.Warning;
                result.Messages.Add($"Unknown RelativeSource type: {relativeSource}");
            }
        }
    }

    /// <summary>
    /// Validates general binding rules that apply to all binding types.
    /// </summary>
    private void ValidateGeneralRules(XamlBindingInfo binding, BindingValidationResult result)
    {
        // Check expression length
        if (binding.Expression.Length > 200)
        {
            result.Severity = (ValidationSeverity)Math.Max((int)result.Severity, (int)ValidationSeverity.Warning);
            result.Messages.Add("Very long binding expression may be difficult to maintain");
        }

        // Check for common typos in property names by examining the raw expression
        var commonTypos = new Dictionary<string, string>
        {
            { "Convertor", "Converter" },
            { "Sorce", "Source" },
            { "Elemnent", "Element" }
        };

        foreach (var (typo, correct) in commonTypos)
        {
            // Check both in properties and in the raw expression
            if (binding.Properties.ContainsKey(typo) || 
                binding.Expression.Contains(typo, StringComparison.OrdinalIgnoreCase))
            {
                result.Severity = ValidationSeverity.Error;
                result.Messages.Add($"Possible typo in property name: '{typo}' should be '{correct}'");
            }
        }

        // Additional pattern-based typo detection in the raw expression
        if (binding.Expression.Contains("Convertor=", StringComparison.OrdinalIgnoreCase))
        {
            result.Severity = ValidationSeverity.Error;
            result.Messages.Add("Possible typo in property name: 'Convertor' should be 'Converter'");
        }
    }
}

/// <summary>
/// Statistics about binding usage in a XAML file.
/// </summary>
public class BindingStatistics
{
    public int TotalBindings { get; set; }
    public int DataBindings { get; set; }
    public int StaticResourceBindings { get; set; }
    public int DynamicResourceBindings { get; set; }
    public int RelativeSourceBindings { get; set; }
    public int ValidationErrors { get; set; }
    public int ValidationWarnings { get; set; }
    public string FilePath { get; set; } = string.Empty;
}