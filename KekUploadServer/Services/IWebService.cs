using KekUploadServer.Models;

namespace KekUploadServer.Services;

public interface IWebService
{
    Task<string> GetMetaPage(UploadItem uploadItem);
    Task<string?> GetVideoSite(UploadItem uploadItem);
    Task<string?> GetLegalSite();
    string GetContentType(string filePath);
}