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