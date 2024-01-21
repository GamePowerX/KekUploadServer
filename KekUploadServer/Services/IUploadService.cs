using System.Net.WebSockets;
using KekUploadServer.Models;
using SharpHash.Interfaces;

namespace KekUploadServer.Services;

public interface IUploadService
{
    Task<string> CreateUploadStream(string extension, string? name = null);
    bool DoesUploadStreamExist(string streamId);
    Task TerminateUploadStream(string streamId);
    Task<UploadItem?> GetUploadItem(string streamId);
    Task<string> FinalizeHash(IHash hash);
    Task<string> FinishUploadStream(UploadItem uploadItem);
    Task<bool> UploadChunk(UploadItem uploadItem, Stream requestBody, string? hash = null);
    Task<bool> UploadChunk(UploadItem uploadItem, byte[] data, string? hash = null, int offset = 0, int? count = null);
    Task<(UploadItem?, string)> GetUploadedItem(string uploadId);
    Task<string?> GetMimeType(string extension);
    Task<IReadOnlyList<UploadItem>> GetUploads();
    Task HandleWebSocket(WebSocket webSocket);
}