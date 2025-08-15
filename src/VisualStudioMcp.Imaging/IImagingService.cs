namespace VisualStudioMcp.Imaging;

/// <summary>
/// Service interface for Visual Studio window capture and imaging.
/// </summary>
public interface IImagingService
{
    /// <summary>
    /// Captures a screenshot of a Visual Studio window.
    /// </summary>
    /// <param name="windowTitle">The title of the window to capture.</param>
    /// <returns>The capture result with image data.</returns>
    Task<ImageCapture> CaptureWindowAsync(string windowTitle);

    /// <summary>
    /// Captures a screenshot of the entire Visual Studio IDE.
    /// </summary>
    /// <returns>The capture result with image data.</returns>
    Task<ImageCapture> CaptureFullIdeAsync();

    /// <summary>
    /// Captures a specific region of the Visual Studio IDE.
    /// </summary>
    /// <param name="x">The X coordinate of the region.</param>
    /// <param name="y">The Y coordinate of the region.</param>
    /// <param name="width">The width of the region.</param>
    /// <param name="height">The height of the region.</param>
    /// <returns>The capture result with image data.</returns>
    Task<ImageCapture> CaptureRegionAsync(int x, int y, int width, int height);

    /// <summary>
    /// Saves an image capture to a file.
    /// </summary>
    /// <param name="capture">The image capture to save.</param>
    /// <param name="filePath">The file path where to save the image.</param>
    Task SaveCaptureAsync(ImageCapture capture, string filePath);

    /// <summary>
    /// Captures the Solution Explorer with project structure annotation.
    /// </summary>
    /// <returns>Annotated capture of Solution Explorer.</returns>
    Task<SpecializedCapture> CaptureSolutionExplorerAsync();

    /// <summary>
    /// Captures the Properties window with property highlighting.
    /// </summary>
    /// <returns>Annotated capture of Properties window.</returns>
    Task<SpecializedCapture> CapturePropertiesWindowAsync();

    /// <summary>
    /// Captures the Error List and Output windows with formatting.
    /// </summary>
    /// <returns>Annotated capture of diagnostic windows.</returns>
    Task<SpecializedCapture> CaptureErrorListAndOutputAsync();

    /// <summary>
    /// Captures a code editor window with syntax highlighting preservation.
    /// </summary>
    /// <param name="editorWindowHandle">Handle to the specific editor window.</param>
    /// <returns>Annotated capture of code editor.</returns>
    Task<SpecializedCapture> CaptureCodeEditorAsync(IntPtr? editorWindowHandle = null);

    /// <summary>
    /// Captures the complete Visual Studio IDE with comprehensive layout and metadata.
    /// </summary>
    /// <returns>Complete IDE capture with layout metadata.</returns>
    Task<FullIdeCapture> CaptureFullIdeWithLayoutAsync();

    /// <summary>
    /// Captures a specific window with intelligent annotation based on window type.
    /// </summary>
    /// <param name="windowHandle">Handle to the window to capture.</param>
    /// <param name="windowType">The type of window being captured.</param>
    /// <returns>Specialized capture with appropriate annotations.</returns>
    Task<SpecializedCapture> CaptureWindowWithAnnotationAsync(IntPtr windowHandle, VisualStudioWindowType windowType);
}

/// <summary>
/// Represents an image capture result.
/// </summary>
public class ImageCapture
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

    /// <summary>
    /// The timestamp when the capture was taken.
    /// </summary>
    public DateTime CaptureTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional metadata about the capture.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a specialized image capture with annotations and metadata.
/// </summary>
public class SpecializedCapture : ImageCapture
{
    /// <summary>
    /// The type of window that was captured.
    /// </summary>
    public VisualStudioWindowType WindowType { get; set; } = VisualStudioWindowType.Unknown;

    /// <summary>
    /// Annotations applied to the capture (highlights, overlays, etc.).
    /// </summary>
    public List<CaptureAnnotation> Annotations { get; set; } = new();

    /// <summary>
    /// Extracted text content from the captured window.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Identified UI elements within the capture.
    /// </summary>
    public List<UiElement> UiElements { get; set; } = new();

    /// <summary>
    /// Specialized metadata specific to the window type.
    /// </summary>
    public WindowSpecificMetadata WindowMetadata { get; set; } = new();
}

/// <summary>
/// Represents a full IDE capture with multiple windows and layout information.
/// </summary>
public class FullIdeCapture
{
    /// <summary>
    /// The primary composite image of the entire IDE.
    /// </summary>
    public ImageCapture CompositeImage { get; set; } = new();

    /// <summary>
    /// Individual window captures that make up the composite.
    /// </summary>
    public List<SpecializedCapture> WindowCaptures { get; set; } = new();

    /// <summary>
    /// Layout information at the time of capture.
    /// </summary>
    public WindowLayout Layout { get; set; } = new();

    /// <summary>
    /// The timestamp when this full IDE capture was taken.
    /// </summary>
    public DateTime CaptureTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall metadata about the IDE state.
    /// </summary>
    public Dictionary<string, object> IdeMetadata { get; set; } = new();
}

/// <summary>
/// Represents an annotation applied to a capture.
/// </summary>
public class CaptureAnnotation
{
    /// <summary>
    /// Type of annotation (highlight, outline, text, etc.).
    /// </summary>
    public AnnotationType Type { get; set; }

    /// <summary>
    /// The bounds of the annotation within the image.
    /// </summary>
    public WindowBounds Bounds { get; set; } = new();

    /// <summary>
    /// Color of the annotation in hex format.
    /// </summary>
    public string Color { get; set; } = "#FF0000";

    /// <summary>
    /// Optional text label for the annotation.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Additional properties for the annotation.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Types of annotations that can be applied to captures.
/// </summary>
public enum AnnotationType
{
    /// <summary>Rectangle highlight overlay.</summary>
    Highlight,
    
    /// <summary>Border outline around element.</summary>
    Outline,
    
    /// <summary>Text label annotation.</summary>
    TextLabel,
    
    /// <summary>Arrow pointing to element.</summary>
    Arrow,
    
    /// <summary>Circle or oval highlight.</summary>
    Circle,
    
    /// <summary>Blur effect applied to region.</summary>
    Blur
}

/// <summary>
/// Represents a UI element identified within a capture.
/// </summary>
public class UiElement
{
    /// <summary>
    /// Type of UI element.
    /// </summary>
    public UiElementType Type { get; set; }

    /// <summary>
    /// The bounds of the UI element within the capture.
    /// </summary>
    public WindowBounds Bounds { get; set; } = new();

    /// <summary>
    /// Text content of the element (if applicable).
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Additional properties of the element.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Types of UI elements that can be identified in captures.
/// </summary>
public enum UiElementType
{
    /// <summary>Button control.</summary>
    Button,
    
    /// <summary>Text box or input field.</summary>
    TextBox,
    
    /// <summary>Label or static text.</summary>
    Label,
    
    /// <summary>Tree view node (Solution Explorer).</summary>
    TreeNode,
    
    /// <summary>List item.</summary>
    ListItem,
    
    /// <summary>Menu item.</summary>
    MenuItem,
    
    /// <summary>Tab header.</summary>
    Tab,
    
    /// <summary>Property grid item.</summary>
    PropertyItem,
    
    /// <summary>Error or warning entry.</summary>
    ErrorItem,
    
    /// <summary>Code editor line.</summary>
    CodeLine
}

/// <summary>
/// Window-specific metadata that varies by window type.
/// </summary>
public class WindowSpecificMetadata
{
    /// <summary>
    /// Solution Explorer specific data.
    /// </summary>
    public SolutionExplorerMetadata? SolutionExplorer { get; set; }

    /// <summary>
    /// Properties window specific data.
    /// </summary>
    public PropertiesWindowMetadata? Properties { get; set; }

    /// <summary>
    /// Error List specific data.
    /// </summary>
    public ErrorListMetadata? ErrorList { get; set; }

    /// <summary>
    /// Code Editor specific data.
    /// </summary>
    public CodeEditorMetadata? CodeEditor { get; set; }
}

/// <summary>
/// Solution Explorer specific metadata.
/// </summary>
public class SolutionExplorerMetadata
{
    /// <summary>
    /// Currently expanded project nodes.
    /// </summary>
    public List<string> ExpandedNodes { get; set; } = new();

    /// <summary>
    /// Currently selected item.
    /// </summary>
    public string? SelectedItem { get; set; }

    /// <summary>
    /// Visible project count.
    /// </summary>
    public int ProjectCount { get; set; }
}

/// <summary>
/// Properties window specific metadata.
/// </summary>
public class PropertiesWindowMetadata
{
    /// <summary>
    /// Currently selected object type.
    /// </summary>
    public string? SelectedObjectType { get; set; }

    /// <summary>
    /// Visible property categories.
    /// </summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>
    /// Currently modified properties.
    /// </summary>
    public List<string> ModifiedProperties { get; set; } = new();
}

/// <summary>
/// Error List specific metadata.
/// </summary>
public class ErrorListMetadata
{
    /// <summary>
    /// Number of errors shown.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Number of warnings shown.
    /// </summary>
    public int WarningCount { get; set; }

    /// <summary>
    /// Number of messages shown.
    /// </summary>
    public int MessageCount { get; set; }

    /// <summary>
    /// Currently active filter.
    /// </summary>
    public string? ActiveFilter { get; set; }
}

/// <summary>
/// Code Editor specific metadata.
/// </summary>
public class CodeEditorMetadata
{
    /// <summary>
    /// File name being edited.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Programming language.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Current line number.
    /// </summary>
    public int? CurrentLine { get; set; }

    /// <summary>
    /// Current column position.
    /// </summary>
    public int? CurrentColumn { get; set; }

    /// <summary>
    /// Whether syntax highlighting is active.
    /// </summary>
    public bool SyntaxHighlightingActive { get; set; }
}