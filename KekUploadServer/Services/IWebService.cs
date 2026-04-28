using KekUploadServer.Models;

namespace KekUploadServer.Services;

/// <summary>
/// Builds HTML responses and content metadata for public upload pages.
/// </summary>
public interface IWebService
{
    /// <summary>
    /// Renders the Open Graph metadata page for an uploaded item.
    /// </summary>
    Task<string> GetMetaPage(UploadItem uploadItem);

    /// <summary>
    /// Renders the video playback page for an uploaded video, when available.
    /// </summary>
    Task<string?> GetVideoSite(UploadItem uploadItem);

    /// <summary>
    /// Renders the configured legal information page, when available.
    /// </summary>
    Task<string?> GetLegalSite();

    /// <summary>
    /// Determines the response content type for a file path.
    /// </summary>
    string GetContentType(string filePath);
}
