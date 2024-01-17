using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KekUploadServer.Controllers;

[ApiController]
public class VideoController(IConfiguration configuration, IUploadService uploadService, IMediaService mediaService)
    : ControllerBase
{
    private readonly IConfiguration _configuration = configuration;

    [HttpGet]
    [Route("t/{uploadId}")]
    public async Task<IActionResult> GetThumbnail(string uploadId)
    {
        var uploadItem = await uploadService.GetUploadedItem(uploadId);
        if (uploadItem.Item1 == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var thumbnail = await mediaService.GetThumbnail(uploadItem);
        if (thumbnail == null)
            return NotFound(ErrorResponse.FileIsNotVideo);
        return File(thumbnail, "image/jpeg", "thumbnail.jpg");
    }
    
    [HttpGet]
    [Route("m/{uploadId}")]
    public async Task<IActionResult> GetMetadata(string uploadId)
    {
        var uploadItem = await uploadService.GetUploadedItem(uploadId);
        if (uploadItem.Item1 == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var metadata = await mediaService.GetMetadata(uploadItem);
        if (metadata == null)
            return NotFound(ErrorResponse.FileIsNotVideo);
        if(metadata.Format == null) return NotFound(ErrorResponse.FileIsNotVideo);
        metadata.Format.Filename = (uploadItem.Item1.Name ?? uploadItem.Item1.Hash) + "." + uploadItem.Item1.Extension;
        return new JsonResult(new
        {
            metadata.Format,
            metadata.Streams
        })
        {
            ContentType = "application/json",
            StatusCode = 200
        };
    }
}