using System.Net.WebSockets;
using System.Text;
using System.Collections.Concurrent;
using System.Buffers;
using KekUploadServer.Database;
using KekUploadServer.Models;
using KekUploadServer.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SharpHash.Base;
using SharpHash.Interfaces;

namespace KekUploadServer.Services;

public class UploadService : IUploadService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private readonly int _idLength;
    private readonly IMemoryCache _memoryCache;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _uploadDirectory;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _streamLocks = new();

    public UploadService(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider,
        IConfiguration configuration, IMemoryCache memoryCache)
    {
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _memoryCache = memoryCache;

        _idLength = _configuration.GetValue("IdLength", 12);
        _uploadDirectory = _configuration.GetValue<string>("UploadDirectory") ?? "uploads";
        Directory.CreateDirectory(_uploadDirectory);
    }

    public async Task<string> CreateUploadStream(string extension, string? name = null)
    {
        var streamId = await Task.Run(() => Utils.RandomString(32));
        var uploadItem = new UploadItem
        {
            Extension = extension,
            UploadStreamId = streamId,
            Name = name,
            Hasher = HashFactory.Crypto.CreateSHA1(),
            FileStream = File.Open(Path.Combine(_uploadDirectory, streamId + ".tmp"), FileMode.Create,
                FileAccess.ReadWrite, FileShare.ReadWrite)
        };
        uploadItem.Hasher.Initialize();
        var options = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(1))
            .RegisterPostEvictionCallback((key, value, _, _) =>
            {
                if (value is UploadItem item) item.FileStream.Dispose();
                var streamKey = key?.ToString();
                if (streamKey == null) return;
                _streamLocks.TryRemove(streamKey, out _);
                var path = Path.Combine(_uploadDirectory, streamKey + ".tmp");
                if (File.Exists(path)) File.Delete(path);
            });
        await Task.Run(() => _memoryCache.Set(streamId, uploadItem, options));
        PluginLoader.PluginApi?.OnUploadStreamCreated(uploadItem);
        return streamId;
    }

    public bool DoesUploadStreamExist(string streamId)
    {
        return _memoryCache.TryGetValue(streamId, out _);
    }

    public async Task TerminateUploadStream(string streamId)
    {
        await Task.Run(() => _memoryCache.Remove(streamId));
    }

    public Task<UploadItem?> GetUploadItem(string streamId)
    {
        return Task.FromResult(_memoryCache.Get<UploadItem?>(streamId));
    }

    public async Task<string> FinalizeHash(IHash hash)
    {
        var result = await Task.Run(hash.TransformFinal);
        return result.ToString().ToLower();
    }

    public async Task<string?> FinalizeUpload(string streamId, string? expectedHash = null)
    {
        var streamLock = _streamLocks.GetOrAdd(streamId, _ => new SemaphoreSlim(1, 1));
        await streamLock.WaitAsync();
        try
        {
            var uploadItem = await GetUploadItem(streamId);
            if (uploadItem == null) return null;
            uploadItem.Hash = await FinalizeHash(uploadItem.Hasher);
            if (expectedHash != null && !string.Equals(uploadItem.Hash, expectedHash, StringComparison.OrdinalIgnoreCase))
                return null;
            return await FinishUploadStreamCore(uploadItem);
        }
        finally
        {
            streamLock.Release();
        }
    }

    public async Task<string> FinishUploadStream(UploadItem uploadItem)
    {
        var streamLock = _streamLocks.GetOrAdd(uploadItem.UploadStreamId, _ => new SemaphoreSlim(1, 1));
        await streamLock.WaitAsync();
        try
        {
            return await FinishUploadStreamCore(uploadItem);
        }
        finally
        {
            streamLock.Release();
        }
    }

    private async Task<string> FinishUploadStreamCore(UploadItem uploadItem)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var uploadDataContext = scope.ServiceProvider.GetRequiredService<UploadDataContext>();
        uploadItem.Id = Utils.RandomString(_configuration.GetValue("IdLength", _idLength));
        await uploadItem.FileStream.DisposeAsync();
        var filePath = Path.Combine(_uploadDirectory, uploadItem.UploadStreamId + ".tmp");
        // check if a file with the same hash already exists
        var existingItem = await uploadDataContext.UploadItems.FirstOrDefaultAsync(x => x.Hash == uploadItem.Hash);
        if (existingItem != null)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            _memoryCache.Remove(uploadItem.UploadStreamId);
            return existingItem.Id;
        }

        var newFilePath = Path.Combine(_uploadDirectory, uploadItem.UploadStreamId + ".upload");
        File.Move(filePath, newFilePath);
        uploadDataContext.UploadItems.Add(uploadItem);
        await uploadDataContext.SaveChangesAsync();
        _memoryCache.Remove(uploadItem.UploadStreamId);
        PluginLoader.PluginApi?.OnUploadStreamFinalized(uploadItem);
        return uploadItem.Id;
    }

    public async Task<bool> UploadChunk(UploadItem uploadItem, Stream requestBody, string? hash = null)
    {
        if (hash != null)
        {
            await using var tempStream = new MemoryStream();
            await requestBody.CopyToAsync(tempStream);
            tempStream.Position = 0;
            var tempBytes = tempStream.ToArray();
            using var pluginChunk = new MemoryStream(tempBytes, writable: false);
            PluginLoader.PluginApi?.OnChunkUploaded(uploadItem, pluginChunk);
            return await UploadChunk(uploadItem, tempBytes, hash);
        }

        var streamLock = _streamLocks.GetOrAdd(uploadItem.UploadStreamId, _ => new SemaphoreSlim(1, 1));
        await streamLock.WaitAsync();
        try
        {
            var buffer = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int bytesRead;
                while ((bytesRead = await requestBody.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                {
                    var chunkCopy = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunkCopy, 0, bytesRead);
                    using var pluginChunk = new MemoryStream(chunkCopy, writable: false);
                    PluginLoader.PluginApi?.OnChunkUploaded(uploadItem, pluginChunk);
                    await uploadItem.FileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    await Task.Run(() => uploadItem.Hasher.TransformBytes(buffer, 0, bytesRead));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }
        finally
        {
            streamLock.Release();
        }
    }

    public async Task<bool> UploadChunk(UploadItem uploadItem, byte[] data, string? hash = null, int offset = 0, int? count = null)
    {
        if (hash != null)
        {
            // get the hash of the chunk
            var chunkHash = HashFactory.Crypto.CreateSHA1();
            var res = await Task.Run(() => chunkHash.ComputeBytes(data));
            // compare the hash of the chunk to the hash provided by the client
            if (!string.Equals(res.ToString(), hash, StringComparison.CurrentCultureIgnoreCase)) return false;
        }

        count ??= data.Length;
        
        var streamLock = _streamLocks.GetOrAdd(uploadItem.UploadStreamId, _ => new SemaphoreSlim(1, 1));
        await streamLock.WaitAsync();
        try
        {
            using var pluginChunk = new MemoryStream(data, offset, count.Value, writable: false);
            PluginLoader.PluginApi?.OnChunkUploaded(uploadItem, pluginChunk);
            await uploadItem.FileStream.WriteAsync(data.AsMemory(offset, count.Value));
            await Task.Run(() => uploadItem.Hasher.TransformBytes(data, offset, count.Value));
            return true;
        }
        finally
        {
            streamLock.Release();
        }
    }

    public async Task<(UploadItem?, string)> GetUploadedItem(string uploadId)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var uploadDataContext = scope.ServiceProvider.GetRequiredService<UploadDataContext>();
        var uploadItem = await uploadDataContext.UploadItems.FindAsync(uploadId);
        if (uploadItem == null) return (null, "Not found");
        var filePath = Path.GetFullPath(Path.Combine(_uploadDirectory, uploadItem.UploadStreamId + ".upload"));
        return !File.Exists(filePath) ? (null, "Not found") : (uploadItem, filePath);
    }

    public async Task<string?> GetMimeType(string extension)
    {
        return await Utils.GetMimeType(extension);
    }

    public async Task<IReadOnlyList<UploadItem>> GetUploads()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var uploadDataContext = scope.ServiceProvider.GetRequiredService<UploadDataContext>();
        return await uploadDataContext.UploadItems.ToListAsync();
    }

    public async Task HandleWebSocket(WebSocket webSocket)
    {
        const string webSocketClientPrefix = "[KekUploadClient] ";
        const string webSocketServerPrefix = "[KekUploadServer] ";
        var maxChunkSize = _configuration.GetValue("WebSocketBufferSize", 2048) * 1024;
        var buffer = new byte[Math.Min(maxChunkSize, 81920)];
        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Waiting for UploadStreamId")), WebSocketMessageType.Text, true, CancellationToken.None);
        UploadItem? uploadItem = null;
        string? uploadStreamId = null;
        while (webSocket.State == WebSocketState.Open)
        {
            var (messageType, data, closeStatus, closeDescription) =
                await ReceiveWholeWebSocketMessage(webSocket, buffer, maxChunkSize);
            if (closeStatus.HasValue)
            {
                await webSocket.CloseAsync(closeStatus.Value, closeDescription, CancellationToken.None);
                return;
            }

            switch (messageType)
            {
                case WebSocketMessageType.Text:
                    var info = Encoding.UTF8.GetString(data);
                    const string uploadStreamIdPrefix = webSocketClientPrefix + "UploadStreamId: ";
                    const string uploadTextDataPrefix = webSocketClientPrefix + "TextData: ";
                    const string finishUploadStreamPrefix = webSocketClientPrefix + "Finish: ";
                    if (info.StartsWith(uploadStreamIdPrefix))
                    {
                        uploadStreamId = info.Substring(uploadStreamIdPrefix.Length).Trim();
                        uploadItem = await GetUploadItem(uploadStreamId);
                        if (uploadItem == null)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Invalid UploadStreamId!!!")), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        else
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Valid UploadStreamId specified. Ready for upload!")), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }else if (info.StartsWith(uploadTextDataPrefix))
                    {
                        if (uploadStreamId == null || uploadItem == null)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "No valid UploadStreamId specified, ignoring incoming data!!!")), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        else
                        {
                            if (!_memoryCache.TryGetValue(uploadStreamId, out _))
                            {
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "UploadStream has expired, please create a new one!")), WebSocketMessageType.Text, true, CancellationToken.None);
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Expired UploadStream!", default);
                                return;
                            }
                            var payload = data[Encoding.UTF8.GetByteCount(uploadTextDataPrefix)..];
                            await UploadChunk(uploadItem, payload);
                        }
                    }else if (info.StartsWith(finishUploadStreamPrefix))
                    {
                        if (uploadStreamId == null || uploadItem == null)
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "No valid UploadStreamId specified!!!")), WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        else
                        {
                            var hash = info.Substring(finishUploadStreamPrefix.Length).Trim();
                            var result = await FinalizeUpload(uploadStreamId, hash);
                            if (result == null)
                            {
                                await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Invalid Hash!")), WebSocketMessageType.Text, true, CancellationToken.None);
                                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid Hash!", default);
                                return;
                            }
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Id: " + result)), WebSocketMessageType.Text, true, CancellationToken.None);
                            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Upload successful!", default);
                            return;
                        }
                    }
                    break;
                case WebSocketMessageType.Binary:
                    if (uploadStreamId == null || uploadItem == null)
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "No valid UploadStreamId specified, ignoring incoming data!!!")), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        if (!_memoryCache.TryGetValue(uploadStreamId, out _))
                        {
                            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "UploadStream has expired, please create a new one!")), WebSocketMessageType.Text, true, CancellationToken.None);
                            await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Expired UploadStream!", default);
                            return;
                        }
                        await UploadChunk(uploadItem, data);
                    }
                    break;
                case WebSocketMessageType.Close:
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    return;
            }
        }
    }

    private static async Task<(WebSocketMessageType MessageType, byte[] Data, WebSocketCloseStatus? CloseStatus, string? CloseDescription)>
        ReceiveWholeWebSocketMessage(WebSocket webSocket, byte[] receiveBuffer, int maxMessageSize)
    {
        await using var messageBuffer = new MemoryStream();
        WebSocketReceiveResult receiveResult;
        do
        {
            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
            if (receiveResult.MessageType == WebSocketMessageType.Close)
                return (WebSocketMessageType.Close, Array.Empty<byte>(), receiveResult.CloseStatus, receiveResult.CloseStatusDescription);
            if (messageBuffer.Length + receiveResult.Count > maxMessageSize)
                return (WebSocketMessageType.Close, Array.Empty<byte>(), WebSocketCloseStatus.MessageTooBig, "Message too large");
            await messageBuffer.WriteAsync(receiveBuffer.AsMemory(0, receiveResult.Count));
        } while (!receiveResult.EndOfMessage);

        return (receiveResult.MessageType, messageBuffer.ToArray(), null, null);
    }
}