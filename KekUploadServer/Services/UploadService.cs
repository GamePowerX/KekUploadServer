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
            .RegisterPostEvictionCallback((key, value, reason, state) =>
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
        uploadItem.Id = Utils.RandomString(_configuration.GetValue("IdLength", 12));
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
        if (hash != null)
        {
            // get the hash of the chunk
            var chunkHash = HashFactory.Crypto.CreateSHA1();
            var res = await Task.Run(() => chunkHash.ComputeBytes(tempBytes));
            // compare the hash of the chunk to the hash provided by the client
            if (!string.Equals(res.ToString(), hash, StringComparison.CurrentCultureIgnoreCase)) return false;
        }

        // write the chunk to the file
        await tempStream.CopyToAsync(uploadItem.FileStream);
        // update the hash
        await Task.Run(() => uploadItem.Hasher.TransformBytes(tempBytes));
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
}