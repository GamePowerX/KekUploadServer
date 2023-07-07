using KekUploadServer.Models;
using MediaToolkit.Core.Meta;

namespace KekUploadServer.Services;

public interface IMediaService
{
    Task<Stream?> GetThumbnail((UploadItem?, string) upload);
    Task<Metadata?> GetMetadata((UploadItem?, string) uploadItem);
}