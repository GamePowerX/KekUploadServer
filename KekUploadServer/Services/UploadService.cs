using System.Net.WebSockets;
using System.Text;
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
                var path = Path.Combine(_uploadDirectory, key + ".tmp");
                File.Delete(path);
            });
        await Task.Run(() => _memoryCache.Set(streamId, uploadItem, options));
        PluginLoader.PluginApi.OnUploadStreamCreated(uploadItem);
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

    public async Task<string> FinishUploadStream(UploadItem uploadItem)
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
            // delete the upload item
            await Task.Run(() => _memoryCache.Remove(uploadItem.UploadStreamId));
            return existingItem.Id;
        }

        var newFilePath = Path.Combine(_uploadDirectory, uploadItem.UploadStreamId + ".upload");
        File.Move(filePath, newFilePath);
        uploadDataContext.UploadItems.Add(uploadItem);
        await uploadDataContext.SaveChangesAsync();
        await Task.Run(() => _memoryCache.Remove(uploadItem.UploadStreamId));
        PluginLoader.PluginApi.OnUploadStreamFinalized(uploadItem);
        return uploadItem.Id;
    }

    public async Task<bool> UploadChunk(UploadItem uploadItem, Stream requestBody, string? hash = null)
    {
        // create a temporary stream to hold the chunk
        await using var tempStream = new MemoryStream();
        // copy the chunk to the temporary stream
        await requestBody.CopyToAsync(tempStream);
        // reset the position of the temporary stream
        tempStream.Position = 0;
        PluginLoader.PluginApi.OnChunkUploaded(uploadItem, tempStream);
        // copy the temporary stream in a byte array
        var tempBytes = tempStream.ToArray();
        return await UploadChunk(uploadItem, tempBytes, hash);
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
        
        // write the chunk to the file
        await uploadItem.FileStream.WriteAsync(data.AsMemory(offset, count.Value));
        // update the hash
        await Task.Run(() => uploadItem.Hasher.TransformBytes(data, offset, count.Value));
        return true;
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
        var maxChunkSize = _configuration.GetValue("WebSocketBufferSize", 2048);
        maxChunkSize *= 1024;
        var buffer = new byte[maxChunkSize];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(webSocketServerPrefix + "Waiting for UploadStreamId")), WebSocketMessageType.Text, true, CancellationToken.None);
        UploadItem? uploadItem = null;
        string? uploadStreamId = null;
        while (!receiveResult.CloseStatus.HasValue)
        {
            
            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
            switch (receiveResult.MessageType)
            {
                case WebSocketMessageType.Text:
                    var info = Encoding.UTF8.GetString(buffer);
                    const string uploadStreamIdPrefix = webSocketClientPrefix + "UploadStreamId: ";
                    const string uploadTextDataPrefix = webSocketClientPrefix + "TextData: ";
                    if (info.StartsWith(uploadStreamIdPrefix))
                    {
                        uploadStreamId = info.Substring(uploadStreamIdPrefix.Length, 32);
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
                            _memoryCache.TryGetValue(uploadStreamId, out _);
                            var offset = Encoding.UTF8.GetByteCount(uploadTextDataPrefix);
                            await UploadChunk(uploadItem, buffer, null, offset, receiveResult.Count - offset);
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
                        _memoryCache.TryGetValue(uploadStreamId, out _);
                        await UploadChunk(uploadItem, buffer, null, 0, receiveResult.Count);
                    }
                    break;
                case WebSocketMessageType.Close:
                    break;
            }
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
}