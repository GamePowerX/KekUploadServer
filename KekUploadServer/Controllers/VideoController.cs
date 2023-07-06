using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KekUploadServer.Controllers;

public class VideoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IUploadService _uploadService;
    private readonly IMediaService _mediaService;
    
    private readonly string _thumbnailDirectory;
    
    public VideoController(IConfiguration configuration, IUploadService uploadService, IMediaService mediaService)
    {
        _configuration = configuration;
        _uploadService = uploadService;
        _mediaService = mediaService;
        
        _thumbnailDirectory = _configuration.GetValue<string>("ThumbnailDirectory") ?? "thumbs";
    }

    [HttpGet]
    [Route("t/{uploadId}")]
    public async Task<IActionResult> GetThumbnail(string uploadId)
    {
        if (!_uploadService.DoesUploadStreamExist(uploadId))
            return NotFound(ErrorResponse.FileWithIdNotFound);
        var thumbnail = await _mediaService.GetThumbnail(uploadId);
        if (thumbnail == null)
            return NotFound(ErrorResponse.FileIsNotVideo);
        return File(thumbnail, "image/jpeg", "thumbnail.jpg");
    }
}