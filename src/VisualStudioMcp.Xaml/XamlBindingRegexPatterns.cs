using System.Text.RegularExpressions;

namespace VisualStudioMcp.Xaml;

/// <summary>
/// Compiled regex patterns for XAML binding analysis.
/// Using compiled patterns provides significant performance improvements over runtime compilation.
/// </summary>
internal static class XamlBindingRegexPatterns
{
    // Regex options for all patterns - compiled for performance, ignore case for flexibility
    private const RegexOptions DefaultOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>
    /// Matches StaticResource binding expressions like "{StaticResource ResourceKey}".
    /// Captures the resource key in group 1.
    /// </summary>
    public static readonly Regex StaticResourcePattern = new(
        @"\{StaticResource\s+([^}]+)\}",
        DefaultOptions,
        TimeSpan.FromSeconds(1)); // Prevent ReDoS attacks with timeout

    /// <summary>
    /// Matches DynamicResource binding expressions like "{DynamicResource ResourceKey}".
    /// Captures the resource key in group 1.
    /// </summary>
    public static readonly Regex DynamicResourcePattern = new(
        @"\{DynamicResource\s+([^}]+)\}",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches Path property in binding expressions like "Path=PropertyName".
    /// Captures the path value in group 1.
    /// </summary>
    public static readonly Regex PathPattern = new(
        @"Path\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches simplified binding syntax like "{Binding PropertyName}" or "{x:Bind PropertyName}".
    /// Captures the property name in group 1 when no equals sign is present.
    /// </summary>
    public static readonly Regex SimpleBindingPattern = new(
        @"\{(?:x:)?Binding\s+([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches Mode property in binding expressions like "Mode=TwoWay".
    /// Captures the mode value in group 1.
    /// </summary>
    public static readonly Regex ModePattern = new(
        @"Mode\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches Converter property with StaticResource like "Converter={StaticResource ConverterKey}".
    /// Captures the converter key in group 1.
    /// </summary>
    public static readonly Regex ConverterPattern = new(
        @"Converter\s*=\s*\{StaticResource\s+([^}]+)\}",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Additional patterns for comprehensive binding analysis
    /// </summary>

    /// <summary>
    /// Matches ElementName property in binding expressions like "ElementName=SomeElement".
    /// Captures the element name in group 1.
    /// </summary>
    public static readonly Regex ElementNamePattern = new(
        @"ElementName\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches Source property in binding expressions.
    /// Captures the source value in group 1.
    /// </summary>
    public static readonly Regex SourcePattern = new(
        @"Source\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches RelativeSource binding expressions like "{RelativeSource Self}".
    /// Captures the relative source type in group 1.
    /// </summary>
    public static readonly Regex RelativeSourcePattern = new(
        @"\{RelativeSource\s+([^}]+)\}",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches StringFormat property in binding expressions.
    /// Captures the format string in group 1.
    /// </summary>
    public static readonly Regex StringFormatPattern = new(
        @"StringFormat\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches UpdateSourceTrigger property in binding expressions.
    /// Captures the trigger value in group 1.
    /// </summary>
    public static readonly Regex UpdateSourceTriggerPattern = new(
        @"UpdateSourceTrigger\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches FallbackValue property in binding expressions.
    /// Captures the fallback value in group 1.
    /// </summary>
    public static readonly Regex FallbackValuePattern = new(
        @"FallbackValue\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Matches TargetNullValue property in binding expressions.
    /// Captures the null value replacement in group 1.
    /// </summary>
    public static readonly Regex TargetNullValuePattern = new(
        @"TargetNullValue\s*=\s*([^,}]+)",
        DefaultOptions,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Comprehensive pattern to identify any binding expression type.
    /// Matches {Binding}, {StaticResource}, {DynamicResource}, {RelativeSource}, {x:Bind}.
    /// </summary>
    public static readonly Regex AnyBindingPattern = new(
        @"\{(?:(?:x:)?Binding|StaticResource|DynamicResource|RelativeSource|x:Bind)\b[^}]*\}",
        DefaultOptions,
        TimeSpan.FromSeconds(2)); // Slightly longer timeout for complex pattern

    /// <summary>
    /// Tests if an expression contains any type of binding.
    /// This is more efficient than string.Contains for multiple patterns.
    /// </summary>
    /// <param name="expression">The expression to test.</param>
    /// <returns>True if the expression contains any binding syntax.</returns>
    public static bool ContainsBinding(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return false;

        return AnyBindingPattern.IsMatch(expression);
    }

    /// <summary>
    /// Extracts all binding expressions from a given text.
    /// Returns a collection of all binding expressions found.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>Collection of binding expression matches.</returns>
    public static MatchCollection ExtractAllBindings(string text)
    {
        if (string.IsNullOrEmpty(text))
            return AnyBindingPattern.Matches(string.Empty);

        return AnyBindingPattern.Matches(text);
    }
}