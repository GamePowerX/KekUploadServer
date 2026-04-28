using System.Net.WebSockets;
using KekUploadServer.Models;
using SharpHash.Interfaces;

namespace KekUploadServer.Services;

/// <summary>
/// Coordinates upload stream creation, chunk writes, finalization, and lookup of stored uploads.
/// </summary>
public interface IUploadService
{
    /// <summary>
    /// Creates a temporary upload stream for a file extension and optional download name.
    /// </summary>
    Task<string> CreateUploadStream(string extension, string? name = null);

    /// <summary>
    /// Checks whether a temporary upload stream is still available.
    /// </summary>
    bool DoesUploadStreamExist(string streamId);

    /// <summary>
    /// Removes a temporary upload stream and its pending file data.
    /// </summary>
    Task TerminateUploadStream(string streamId);

    /// <summary>
    /// Gets the in-progress upload state for a temporary stream.
    /// </summary>
    Task<UploadItem?> GetUploadItem(string streamId);

    /// <summary>
    /// Finalizes a hasher and returns the lowercase digest string.
    /// </summary>
    Task<string> FinalizeHash(IHash hash);

    /// <summary>
    /// Finalizes an upload stream, optionally validating the completed file hash.
    /// </summary>
    Task<string?> FinalizeUpload(string streamId, string? expectedHash = null);

    /// <summary>
    /// Persists an upload item after all chunks have been written.
    /// </summary>
    Task<string> FinishUploadStream(UploadItem uploadItem);

    /// <summary>
    /// Writes a request body chunk to an in-progress upload stream.
    /// </summary>
    Task<bool> UploadChunk(UploadItem uploadItem, Stream requestBody, string? hash = null);

    /// <summary>
    /// Writes a byte-array chunk to an in-progress upload stream.
    /// </summary>
    Task<bool> UploadChunk(UploadItem uploadItem, byte[] data, string? hash = null, int offset = 0, int? count = null);

    /// <summary>
    /// Resolves a public upload id to its metadata and stored file path.
    /// </summary>
    Task<(UploadItem?, string)> GetUploadedItem(string uploadId);

    /// <summary>
    /// Looks up the response MIME type for a file extension.
    /// </summary>
    Task<string?> GetMimeType(string extension);

    /// <summary>
    /// Lists all stored upload records.
    /// </summary>
    Task<IReadOnlyList<UploadItem>> GetUploads();

    /// <summary>
    /// Processes the upload WebSocket protocol for chunked clients.
    /// </summary>
    Task HandleWebSocket(WebSocket webSocket);
}
