using KekUploadServer.Models;
using MediaToolkit.Core.Meta;

namespace KekUploadServer.Services;

/// <summary>
/// Extracts media-specific assets and metadata from stored uploads.
/// </summary>
public interface IMediaService
{
    /// <summary>
    /// Generates a thumbnail stream for an uploaded video file.
    /// </summary>
    Task<Stream?> GetThumbnail((UploadItem?, string) upload);

    /// <summary>
    /// Reads media metadata for an uploaded file.
    /// </summary>
    Task<Metadata?> GetMetadata((UploadItem?, string) uploadItem);
}
