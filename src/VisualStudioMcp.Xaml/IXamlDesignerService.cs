namespace VisualStudioMcp.Xaml;

/// <summary>
/// Service interface for XAML designer automation.
/// </summary>
public interface IXamlDesignerService
{
    /// <summary>
    /// Finds XAML designer windows in the Visual Studio process.
    /// </summary>
    /// <param name="vsProcessId">Visual Studio process ID.</param>
    /// <returns>Array of designer window information.</returns>
    Task<XamlDesignerWindow[]> FindXamlDesignerWindowsAsync(int vsProcessId);

    /// <summary>
    /// Gets the active XAML designer window for the currently active document.
    /// </summary>
    /// <returns>Designer window information if found, null otherwise.</returns>
    Task<XamlDesignerWindow?> GetActiveDesignerWindowAsync();

    /// <summary>
    /// Activates a specific XAML designer window.
    /// </summary>
    /// <param name="designerWindow">The designer window to activate.</param>
    /// <returns>True if successfully activated, false otherwise.</returns>
    Task<bool> ActivateDesignerWindowAsync(XamlDesignerWindow designerWindow);

    /// <summary>
    /// Extracts the visual tree elements from a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of visual tree elements.</returns>
    Task<XamlElement[]> ExtractVisualTreeAsync(string xamlFilePath);

    /// <summary>
    /// Modifies a property on elements with the specified name in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if successfully modified, false otherwise.</returns>
    Task<bool> ModifyElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue);

    /// <summary>
    /// Searches for elements by name within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name to search for.</param>
    /// <returns>Array of matching elements.</returns>
    Task<XamlElement[]> FindElementsByNameAsync(string xamlFilePath, string elementName);

    /// <summary>
    /// Searches for elements by type within a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type to search for.</param>
    /// <returns>Array of matching elements.</returns>
    Task<XamlElement[]> FindElementsByTypeAsync(string xamlFilePath, string elementType);

    /// <summary>
    /// Adds a new property to elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to add.</param>
    /// <param name="propertyValue">Value for the new property.</param>
    /// <returns>True if the property was added successfully, false otherwise.</returns>
    Task<bool> AddElementPropertyAsync(string xamlFilePath, string elementName, string propertyName, string propertyValue);

    /// <summary>
    /// Removes a property from elements with the specified name.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementName">Name of the element to modify.</param>
    /// <param name="propertyName">Name of the property to remove.</param>
    /// <returns>True if the property was removed successfully, false otherwise.</returns>
    Task<bool> RemoveElementPropertyAsync(string xamlFilePath, string elementName, string propertyName);

    /// <summary>
    /// Modifies properties for all elements of a specific type.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <param name="elementType">Type of elements to modify.</param>
    /// <param name="propertyName">Name of the property to modify.</param>
    /// <param name="propertyValue">New value for the property.</param>
    /// <returns>True if the modification was successful, false otherwise.</returns>
    Task<bool> ModifyElementsByTypeAsync(string xamlFilePath, string elementType, string propertyName, string propertyValue);

    /// <summary>
    /// Finds all elements in a XAML file that have data binding expressions.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Array of elements with bindings.</returns>
    Task<XamlElement[]> FindElementsWithBindingsAsync(string xamlFilePath);

    /// <summary>
    /// Gets comprehensive statistics about binding usage in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Binding usage statistics.</returns>
    Task<BindingStatistics> GetBindingStatisticsAsync(string xamlFilePath);

    /// <summary>
    /// Gets the root element from a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Root element information, or null if not found.</returns>
    Task<XamlElement?> GetRootElementAsync(string xamlFilePath);

    /// <summary>
    /// Creates a backup of a XAML file before modification.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file.</param>
    /// <returns>Path to the backup file, or null if backup failed.</returns>
    Task<string?> CreateBackupAsync(string xamlFilePath);

    /// <summary>
    /// Invalidates the parser cache for a specific XAML file.
    /// </summary>
    /// <param name="xamlFilePath">Path to the XAML file to invalidate.</param>
    void InvalidateParserCache(string xamlFilePath);

    /// <summary>
    /// Clears all cached XAML documents in the parser.
    /// </summary>
    void ClearParserCache();

    /// <summary>
    /// Analyses data bindings in a XAML file.
    /// </summary>
    /// <param name="xamlFilePath">The path to the XAML file to analyse.</param>
    /// <returns>Array of binding information with analysis results.</returns>
    Task<XamlBindingInfo[]> AnalyseDataBindingsAsync(string xamlFilePath);

    /// <summary>
    /// Validates data bindings and detects potential issues.
    /// </summary>
    /// <param name="xamlFilePath">The path to the XAML file to validate.</param>
    /// <returns>Array of binding validation results.</returns>
    Task<BindingValidationResult[]> ValidateDataBindingsAsync(string xamlFilePath);
}

/// <summary>
/// Represents a XAML designer capture result.
/// </summary>
public class XamlDesignerCapture
{
    /// <summary>
    /// The captured image data as a byte array.
    /// </summary>
    public byte[] ImageData { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The format of the image (PNG, JPEG, etc.).
    /// </summary>
    public string ImageFormat { get; set; } = string.Empty;

    /// <summary>
    /// The width of the captured image.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// The height of the captured image.
    /// </summary>
    public int Height { get; set; }
}

/// <summary>
/// Represents a XAML element in the visual tree.
/// </summary>
public class XamlElement
{
    /// <summary>
    /// The local name of the element.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The namespace of the element.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// The type of the element (local name).
    /// </summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>
    /// The element name from x:Name or Name attribute, if present.
    /// </summary>
    public string ElementName { get; set; } = string.Empty;

    /// <summary>
    /// The parent element, if any.
    /// </summary>
    public XamlElement? Parent { get; set; }

    /// <summary>
    /// The depth of this element in the visual tree (0 for root).
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// Properties of this element.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Represents a property of a XAML element.
/// </summary>
public class XamlProperty
{
    /// <summary>
    /// The name of the property.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The current value of the property.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// The type of the property.
    /// </summary>
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// Represents a XAML designer window.
/// </summary>
public class XamlDesignerWindow
{
    /// <summary>
    /// The window handle (HWND).
    /// </summary>
    public IntPtr Handle { get; set; }

    /// <summary>
    /// The title of the designer window.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The process ID of the Visual Studio instance.
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// The path to the XAML file being designed.
    /// </summary>
    public string XamlFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the designer window is currently active/focused.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether the designer window is visible.
    /// </summary>
    public bool IsVisible { get; set; }

    /// <summary>
    /// Additional metadata about the designer window.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents information about a data binding in XAML.
/// </summary>
public class XamlBindingInfo
{
    /// <summary>
    /// The binding expression (e.g., "{Binding Path=PropertyName}").
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// The type of binding (Binding, StaticResource, DynamicResource, RelativeSource, x:Bind).
    /// </summary>
    public string BindingType { get; set; } = string.Empty;

    /// <summary>
    /// The binding path extracted from the expression.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The resource key for resource bindings.
    /// </summary>
    public string ResourceKey { get; set; } = string.Empty;

    /// <summary>
    /// The file path where this binding was found.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The line number in the XAML file where this binding occurs.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Additional properties extracted from the binding expression.
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Represents the result of binding validation.
/// </summary>
public class BindingValidationResult
{
    /// <summary>
    /// The binding information that was validated.
    /// </summary>
    public XamlBindingInfo Binding { get; set; } = new();

    /// <summary>
    /// The severity of any issues found.
    /// </summary>
    public ValidationSeverity Severity { get; set; }

    /// <summary>
    /// List of validation messages found.
    /// </summary>
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// Severity levels for binding validation issues.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// No issues found.
    /// </summary>
    None = 0,

    /// <summary>
    /// Informational message or suggestion.
    /// </summary>
    Info = 1,

    /// <summary>
    /// Warning about potential issues.
    /// </summary>
    Warning = 2,

    /// <summary>
    /// Error that will likely cause runtime problems.
    /// </summary>
    Error = 3,

    /// <summary>
    /// Critical error that will definitely cause problems.
    /// </summary>
    Critical = 4
}