namespace VisualStudioMcp.Xaml;

/// <summary>
/// Service interface for XAML designer automation.
/// </summary>
public interface IXamlDesignerService
{
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