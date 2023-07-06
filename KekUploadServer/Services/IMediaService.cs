namespace KekUploadServer.Services;

public interface IMediaService
{
    Task<Stream?> GetThumbnail(string uploadId);
}