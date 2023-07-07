using KekUploadServer.Models;
using MediaToolkit.Core.Infrastructure;
using MediaToolkit.Core.Meta;
using MediaToolkit.Core.Services;
using Newtonsoft.Json;

namespace KekUploadServer.Services;

public class MediaService : IMediaService
{
    
    private readonly string _thumbnailDirectory;
    
    public MediaService(IConfiguration configuration)
    {
        _thumbnailDirectory = configuration.GetValue<string>("ThumbnailDirectory") ?? "thumbs";
        Directory.CreateDirectory(_thumbnailDirectory);
    }
    
    
    public async Task<Stream?> GetThumbnail((UploadItem?, string) upload)
    {
        var (uploadItem, path) = upload;
        if (uploadItem == null) return null;
        
        // check if file is video
        var mimeType = await Utils.GetMimeType(uploadItem.Extension);
        if (mimeType == null || !mimeType.StartsWith("video/")) return null;
        
        if(File.Exists(Path.Combine(_thumbnailDirectory, uploadItem.Id + ".jpg")))
            return File.Open(Path.Combine(_thumbnailDirectory, uploadItem.Id + ".jpg"), FileMode.Open, FileAccess.Read, FileShare.Read);
        
        var tcs = new TaskCompletionSource<bool>();

        var mediaConverter = new MediaConverterService(new FFmpegServiceConfiguration());
        mediaConverter.OnCompleteEventHandler += (_, _) => tcs.SetResult(true);
        
        var builder = new ExtractThumbnailInstructionBuilder
        {
            InputFilePath = path,
            OutputFilePath = Path.Combine(_thumbnailDirectory, uploadItem.Id + ".jpg"),
            SeekFrom = TimeSpan.FromSeconds(1)
        };

        await mediaConverter.ExecuteInstructionAsync(builder);
        await tcs.Task;
        return File.Open(Path.Combine(_thumbnailDirectory, uploadItem.Id + ".jpg"), FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public async Task<Metadata?> GetMetadata((UploadItem?, string) uploadItem)
    {
        var (item, path) = uploadItem;
        if(item == null) return null;
        
        // check if file is video
        var mimeType = await Utils.GetMimeType(item.Extension);
        if (mimeType == null || !mimeType.StartsWith("video/")) return null;
        
        var tcs = new TaskCompletionSource<Metadata>();
        var metadataService = new MetadataService(new FFprobeServiceConfiguration());
        metadataService.OnMetadataProcessedEventHandler += (sender, args) => tcs.TrySetResult(args.Metadata);
        var instruction = new GetMetadataInstructionBuilder
        {
            InputFilePath = path
        };
        await metadataService.ExecuteInstructionAsync(instruction);
        var metadata = await tcs.Task;
        return metadata;
    }
}