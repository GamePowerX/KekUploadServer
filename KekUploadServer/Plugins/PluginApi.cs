using System.Text;
using KekUploadServer.Extensions;
using KekUploadServer.Models;
using KekUploadServer.Services;
using KekUploadServerApi;
using KekUploadServerApi.Console;
using KekUploadServerApi.Uploads;

namespace KekUploadServer.Plugins;

public class PluginApi : IKekUploadServer
{
    public PluginApi(WebApplication app)
    {
        App = app;
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        UploadService = services.GetRequiredService<IUploadService>();
    }

    public WebApplication App { get; }
    public IUploadService UploadService { get; }

    public Dictionary<IPlugin, PluginState> Plugins { get; } = new();
    
    public async Task AddAndLoadPlugin(IPlugin plugin)
    {
        await plugin.Load(this);
        Plugins.Add(plugin, PluginState.Loaded);
    }

    public async Task UnloadPlugin(IPlugin plugin)
    {
        await plugin.Unload();
        Plugins[plugin] = PluginState.Unloaded;
    }

    public IReadOnlyList<string> GetPluginNames()
    {
        return Plugins.Keys.Select(x => x.Info.Name).ToList();
    }

    public string GetPluginDataPath(IPlugin plugin)
    {
        return GetPluginDataPath(plugin.Info);
    }

    public string GetPluginDataPath(string pluginName)
    {
        var path = Path.Combine(Environment.CurrentDirectory, "plugins", pluginName);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    public string GetPluginDataPath(PluginInfo pluginInfo)
    {
        return GetPluginDataPath(pluginInfo.Name);
    }

    public IPlugin? GetPlugin(string pluginName)
    {
       return Plugins.FirstOrDefault(x => x.Key.Info.Name == pluginName).Key;
    }

    public string GetPluginPath(IPlugin plugin)
    {
        return plugin.GetType().Assembly.Location;
    }

    public bool IsPluginLoaded(string pluginName)
    {
        var plugin = GetPlugin(pluginName);
        if(plugin != null)
        {
            return Plugins[plugin] == PluginState.Loaded || Plugins[plugin] == PluginState.Enabled;
        }
        return false;
    }

    public bool IsPluginEnabled(string pluginName)
    {
        var plugin = GetPlugin(pluginName);
        if (plugin == null)
        {
            return false;
        }
        return Plugins[plugin] == PluginState.Enabled;
    }

    public ILogger<T> GetPluginLogger<T>() where T : IPlugin
    {
        return App.Services.GetRequiredService<ILogger<T>>();
    }

    public ILogger<T> GetPluginLogger<T>(IPlugin plugin)
    {
        // check if T is in the same assembly as plugin
        var pluginAssembly = plugin.GetType().Assembly;
        var loggerAssembly = typeof(T).Assembly;
        if (pluginAssembly == loggerAssembly)
        {
            return App.Services.GetRequiredService<ILogger<T>>();
        }
        throw new ArgumentException("T is not in the same assembly as plugin!");
    }

    public void RegisterLoggerProvider(IPlugin plugin, ILoggerProvider loggerProvider)
    {
        var loggerFactory = App.Services.GetRequiredService<ILoggerFactory>();
        loggerFactory.AddProvider(loggerProvider);
    }
    
    public T? GetConfigValue<T>(string key)
    {
        return App.Configuration.GetValue<T>(key);
    }

    public object? GetConfigValue(string key)
    {
        return App.Configuration.GetValue<object>(key);
    }

    public string? GetConfigValueString(string key)
    {
        return App.Configuration.GetValue<string>(key);
    }

    public void SetConfigValue<T>(string key, T value)
    {
        App.Configuration.SetValue(key, value);
    }

    public async Task<IReadOnlyList<IUploadedItem>> GetUploads()
    {
        return await UploadService.GetUploads();
    }

    public Task<string> CreateUploadStream(string extension, string? name = null)
    {
        return UploadService.CreateUploadStream(extension, name);
    }

    public Task TerminateUploadStream(string streamId)
    {
        return UploadService.TerminateUploadStream(streamId);
    }

    public async Task<bool> UploadChunk(string streamId, Stream requestBody, string? hash = null)
    {
        var uploadItem = await UploadService.GetUploadItem(streamId);
        if (uploadItem == null)
        {
            return false;
        }
        return await UploadService.UploadChunk(uploadItem, requestBody, hash);
    }

    public async Task<bool> UploadChunk(string streamId, byte[] requestBody, string? hash = null)
    {
        return await UploadChunk(streamId, new MemoryStream(requestBody), hash);
    }

    public async Task<bool> UploadChunk(string streamId, string requestBody, string? hash = null)
    {
        return await UploadChunk(streamId, Encoding.UTF8.GetBytes(requestBody), hash);
    }

    public async Task<bool> UploadChunk(string streamId, string requestBody, Encoding encoding, string? hash = null)
    {
        return await UploadChunk(streamId, encoding.GetBytes(requestBody), hash);
    }

    public async Task<string?> FinalizeUpload(string streamId, string? hash = null)
    {
        var uploadItem = await UploadService.GetUploadItem(streamId);
        if (uploadItem == null)
        {
            return null;
        }
        uploadItem.Hash = await UploadService.FinalizeHash(uploadItem.Hasher);
        if (uploadItem.Hash != hash)
        {
            return null;
        }
        return await UploadService.FinishUploadStream(uploadItem);
    }

    public bool DoesUploadStreamExist(string streamId)
    {
        return UploadService.DoesUploadStreamExist(streamId);
    }

    public async Task<IUploadItem?> GetUploadItem(string streamId)
    {
        return await UploadService.GetUploadItem(streamId);
    }

    public async Task<IUploadedItem?> GetUploadedItem(string uploadId)
    {
        return (await UploadService.GetUploadedItem(uploadId)).Item1;
    }

    public async Task<IEnumerable<string>> GetMimeType(string extension)
    {
        return await Task.Run(() => MimeTypeMap.List.MimeTypeMap.GetMimeType(extension));
    }

    public async Task<Stream?> GetUploadedFileStream(string uploadId)
    {
        var item = await UploadService.GetUploadedItem(uploadId);
        return item.Item1 == null ? null : File.OpenRead(item.Item2);
    }

    public async Task<byte[]?> GetUploadedFileBytes(string uploadId)
    {
        var item = await UploadService.GetUploadedItem(uploadId);
        return item.Item1 == null ? null : await File.ReadAllBytesAsync(item.Item2);
    }

    public async Task<string?> GetUploadedFileString(string uploadId)
    {
        var item = await UploadService.GetUploadedItem(uploadId);
        return item.Item1 == null ? null : await File.ReadAllTextAsync(item.Item2);
    }

    public async Task<string?> GetUploadedFileString(string uploadId, Encoding encoding)
    {
        var item = await UploadService.GetUploadedItem(uploadId);
        return item.Item1 == null ? null : await File.ReadAllTextAsync(item.Item2, encoding);
    }

    public void Shutdown(IPlugin plugin, string reason = "Plugin initiated shutdown", TimeSpan delay = new())
    {
        new Thread(() =>
        {
            App.Services.GetRequiredService<ILogger<Program>>().LogCritical(
                "Plugin {PluginName} initiated shutdown: {Reason} (Delay: {Delay})", plugin.Info.Name, reason, delay);
            Task.Delay(delay).Wait();
            App.StopAsync().Wait();
        }).Start();
    }

    public event EventHandler<UploadStreamCreatedEventArgs>? UploadStreamCreated;
    public event EventHandler<ChunkUploadedEventArgs>? ChunkUploaded;
    public event EventHandler<UploadStreamFinalizedEventArgs>? UploadStreamFinalized;
    
    public event EventHandler<ConsoleLineWrittenEventArgs>? ConsoleLineWritten;

    public void OnUploadStreamCreated(UploadItem uploadItem)
    {
        new Thread(() =>
        {
            UploadStreamCreated?.Invoke(this, new UploadStreamCreatedEventArgs(uploadItem));
        }).Start();
    }
    public void OnChunkUploaded(UploadItem uploadItem, Stream chunk)
    {
        new Thread(() =>
        {
            ChunkUploaded?.Invoke(this, new ChunkUploadedEventArgs(uploadItem, chunk));
        }).Start();
    }
    public void OnUploadStreamFinalized(UploadItem uploadItem)
    {
        new Thread(() =>
        {
            UploadStreamFinalized?.Invoke(this, new UploadStreamFinalizedEventArgs(uploadItem, uploadItem));
        }).Start();
    }
    
    public void OnConsoleLineWritten(string line)
    {
        new Thread(() =>
        {
            ConsoleLineWritten?.Invoke(this, new ConsoleLineWrittenEventArgs(line));
        }).Start();
    }
}

public enum PluginState
{
    Loaded,
    Unloaded,
    Enabled,
    Disabled
}