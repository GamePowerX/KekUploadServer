using System.Text;
using System.Net;
using KekUploadServer.Models;

namespace KekUploadServer.Services;

public class WebService : IWebService
{
    private readonly string _baseUrl;
    private readonly IConfiguration _configuration;
    private readonly string _description;
    private readonly string _embedColor;

    public WebService(IConfiguration configuration)
    {
        _configuration = configuration;

        _baseUrl = _configuration.GetValue<string>("BaseUrl") ?? "http://localhost:5254";
        _description = _configuration.GetValue<string>("Description") ?? "KekUpload";
        _embedColor = _configuration.GetValue<string>("EmbedColor") ?? "#2BFF00";
    }

    public async Task<string> GetMetaPage(UploadItem uploadItem)
    {
        var fileName = (uploadItem.Name ?? uploadItem.Hash) + '.' + uploadItem.Extension;
        var safeFileName = WebUtility.HtmlEncode(fileName);
        var safeDescription = WebUtility.HtmlEncode(_description);
        var safeEmbedColor = WebUtility.HtmlEncode(_embedColor);
        var baseDownloadUrl = _baseUrl + "/d/" + uploadItem.Id;
        var baseVideoUrl = _baseUrl + "/v/" + uploadItem.Id;
        var thumbnailUrl = _baseUrl + "/t/" + uploadItem.Id;

        var content = new StringBuilder();
        content.Append("<!DOCTYPE html>" +
                       $"<meta http-equiv=\"refresh\" content=\"0; url='{WebUtility.HtmlEncode(baseDownloadUrl)}'\" />" +
                       "<meta name='robots' content='noindex'>" +
                       "<meta charset='utf-8'>" +
                       "<meta property='og:type' content='website'>" +
                       "<meta property='twitter:card' content='summary_large_image'>" +
                       $"<meta name='title' content='{safeFileName}'>" +
                       $"<meta property='og:title' content='{safeFileName}'>" +
                       $"<meta name='theme-color' content='{safeEmbedColor}'>");
        var mimeType = await Utils.GetMimeType(uploadItem.Extension) ?? "";
        if (mimeType.StartsWith("image/"))
            content.Append($"<meta property='og:image' content='{WebUtility.HtmlEncode(baseDownloadUrl)}'>" +
                           $"<meta property='twitter:image' content='{WebUtility.HtmlEncode(baseDownloadUrl)}'>" +
                           $"<meta name='description' content='{safeDescription}'>" +
                           $"<meta property='og:description' content='{safeDescription}'>" +
                           $"<meta property='twitter:description' content='{safeDescription}'>");
        else if (mimeType.StartsWith("video/"))
            content.Append($"<meta property='og:image' content='{WebUtility.HtmlEncode(thumbnailUrl)}'>" +
                           $"<meta property='twitter:image' content='{WebUtility.HtmlEncode(thumbnailUrl)}'>" +
                           $"<meta property='og:description' content='{safeDescription}\nWatch video at: {WebUtility.HtmlEncode(baseVideoUrl)}'>" +
                           $"<meta property='twitter:description' content='{safeDescription}\nWatch video at: {WebUtility.HtmlEncode(baseVideoUrl)}'>");
        else
            content.Append($"<meta name='description' content='{safeDescription}'>" +
                           $"<meta property='og:description' content='{safeDescription}'>" +
                           $"<meta property='twitter:description' content='{safeDescription}'>");
        return content.ToString();
    }

    public async Task<string?> GetVideoSite(UploadItem uploadItem)
    {
        var type = await Utils.GetMimeType(uploadItem.Extension) ?? "";
        if (!type.StartsWith("video/"))
            return null;
        if (!File.Exists("VideoPlayer.html")) return "VideoPlayer.html not found";
        var html = await File.ReadAllTextAsync("VideoPlayer.html");
        html = html.Replace("%id%", uploadItem.Id);
        html = html.Replace("%name%", uploadItem.Name ?? uploadItem.Hash);
        html = html.Replace("%description%", _description);
        html = html.Replace("%extension%", uploadItem.Extension);
        html = html.Replace("%downloadUrl%", _baseUrl + "/d/" + uploadItem.Id);
        html = html.Replace("%rootUrl%", _baseUrl + "/");
        html = html.Replace("%thumbnail%", _baseUrl + "/t/" + uploadItem.Id);
        html = html.Replace("%videoEmbedColor%", _embedColor);
        return html;
    }

    public async Task<string?> GetLegalSite()
    {
        if (!File.Exists("Legal.html")) return null;
        var legal = await File.ReadAllTextAsync("Legal.html");
        legal = legal.Replace("%email%", _configuration.GetValue<string>("Email") ?? "uknown@example.com");
        return legal;
    }
    
    public string GetContentType(string filePath)
    {
        if (!File.Exists(filePath))
            return "application/octet-stream";
        var mimeTypeEnumerable = MimeTypeMap.List.MimeTypeMap.GetMimeType(Path.GetExtension(filePath));
        if (mimeTypeEnumerable != null)
            return mimeTypeEnumerable.First();

        var extension = Path.GetExtension(filePath);
        return extension switch
        {
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream"
        };
    }
}