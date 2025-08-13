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
    /// Captures a screenshot of the XAML designer surface.
    /// </summary>
    /// <param name="xamlFilePath">The path to the XAML file being designed.</param>
    /// <returns>The capture result with image data and metadata.</returns>
    Task<XamlDesignerCapture> CaptureDesignerAsync(string xamlFilePath);

    /// <summary>
    /// Gets the visual tree of elements in the XAML designer.
    /// </summary>
    /// <param name="xamlFilePath">The path to the XAML file.</param>
    /// <returns>Array of XAML elements in the visual tree.</returns>
    Task<XamlElement[]> GetVisualTreeAsync(string xamlFilePath);

    /// <summary>
    /// Gets properties of a specific XAML element.
    /// </summary>
    /// <param name="elementPath">The path to the element in the visual tree.</param>
    /// <returns>Array of element properties.</returns>
    Task<XamlProperty[]> GetElementPropertiesAsync(string elementPath);

    /// <summary>
    /// Modifies a property of a XAML element in the designer.
    /// </summary>
    /// <param name="elementPath">The path to the element.</param>
    /// <param name="property">The property name to modify.</param>
    /// <param name="value">The new value for the property.</param>
    Task ModifyElementPropertyAsync(string elementPath, string property, object value);

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
    /// The name of the element.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The type of the element.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The path to this element in the visual tree.
    /// </summary>
    public string Path { get; set; } = string.Empty;
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
    /// The element that contains the binding.
    /// </summary>
    public string ElementName { get; set; } = string.Empty;

    /// <summary>
    /// The element type that contains the binding.
    /// </summary>
    public string ElementType { get; set; } = string.Empty;

    /// <summary>
    /// The property that has the binding.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// The binding expression (e.g., "{Binding Path=PropertyName}").
    /// </summary>
    public string BindingExpression { get; set; } = string.Empty;

    /// <summary>
    /// The binding path extracted from the expression.
    /// </summary>
    public string BindingPath { get; set; } = string.Empty;

    /// <summary>
    /// The binding mode (OneWay, TwoWay, OneTime, etc.).
    /// </summary>
    public string BindingMode { get; set; } = string.Empty;

    /// <summary>
    /// The converter used in the binding, if any.
    /// </summary>
    public string? Converter { get; set; }

    /// <summary>
    /// Whether this is a resource binding ({StaticResource}).
    /// </summary>
    public bool IsResourceBinding { get; set; }

    /// <summary>
    /// Whether this is a dynamic resource binding ({DynamicResource}).
    /// </summary>
    public bool IsDynamicResourceBinding { get; set; }

    /// <summary>
    /// The line number in the XAML file where this binding occurs.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Additional metadata about the binding.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
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
    /// Whether the binding validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// The severity of any issues found.
    /// </summary>
    public ValidationSeverity Severity { get; set; }

    /// <summary>
    /// List of validation issues found.
    /// </summary>
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// List of recommendations for improving the binding.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
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