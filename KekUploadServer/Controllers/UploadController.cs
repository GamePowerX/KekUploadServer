using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KekUploadServer.Controllers;

[ApiController]
public class UploadController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly int _maxExtensionLength;
    private readonly IUploadService _uploadService;

    public UploadController(IConfiguration configuration, IUploadService uploadService)
    {
        _configuration = configuration;
        _uploadService = uploadService;
        _maxExtensionLength = _configuration.GetValue("MaxExtensionLength", 10);
    }

    [HttpPost]
    [Route("c/{extension}")]
    public async Task<IActionResult> CreateUploadStream(string extension)
    {
        if (extension.Length > _maxExtensionLength)
            return BadRequest(ErrorResponse.ExtensionTooLong(_maxExtensionLength));
        var uploadStreamId = await _uploadService.CreateUploadStream(extension);
        return Ok(new
        {
            stream = uploadStreamId
        });
    }

    [HttpPost]
    [Route("c/{extension}/{name}")]
    public async Task<IActionResult> CreateUploadStream(string extension, string name)
    {
        if (extension.Length > _maxExtensionLength)
            return BadRequest(ErrorResponse.ExtensionTooLong(_maxExtensionLength));
        var uploadStreamId = await _uploadService.CreateUploadStream(extension, name);
        return Ok(new
        {
            stream = uploadStreamId
        });
    }

    [HttpPost]
    [Route("r/{streamId}")]
    public async Task<IActionResult> TerminateUploadStream(string streamId)
    {
        if (!_uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        await _uploadService.TerminateUploadStream(streamId);
        return Ok(new
        {
            success = true
        });
    }

    [HttpPost]
    [Route("f/{streamId}/{hash}")]
    public async Task<IActionResult> FinishUploadStream(string streamId, string hash)
    {
        var uploadItem = await _uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        uploadItem.Hash = await _uploadService.FinalizeHash(uploadItem.Hasher);
        if (uploadItem.Hash != hash)
            return BadRequest(ErrorResponse.HashMismatch);
        var uploadId = await _uploadService.FinishUploadStream(uploadItem);
        return Ok(new
        {
            id = uploadId
        });
    }

    [HttpPost]
    [Route("u/{streamId}")]
    public async Task<IActionResult> UploadChunk(string streamId)
    {
        if (!_uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var uploadItem = await _uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        await _uploadService.UploadChunk(uploadItem, Request.Body);
        return Ok(new
        {
            success = true
        });
    }

    [HttpPost]
    [Route("u/{streamId}/{hash}")]
    public async Task<IActionResult> UploadChunkWithHash(string streamId, string hash)
    {
        var uploadItem = await _uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var equal = await _uploadService.UploadChunk(uploadItem, Request.Body, hash);
        if (!equal)
            return BadRequest(ErrorResponse.HashMismatch);
        return Ok(new
        {
            success = true
        });
    }
    
    [HttpGet]
    [Route("d/{uploadId}")]
    public async Task<IActionResult> DownloadFile(string uploadId)
    {
        var (uploadItem, path ) = await _uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);
        var mimeType = await _uploadService.GetMimeType(uploadItem.Extension);
        return PhysicalFile(path, mimeType ?? "application/octet-stream", uploadItem.Name != null ? uploadItem.Name + '.' + uploadItem.Extension : uploadItem.Hash + '.' + uploadItem.Extension);
    }
}