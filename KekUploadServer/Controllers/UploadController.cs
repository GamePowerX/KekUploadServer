using System.Net.WebSockets;
using System.Text;
using KekUploadServer.Models;
using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace KekUploadServer.Controllers;

[ApiController]
public class UploadController(IConfiguration configuration, IUploadService uploadService) : ControllerBase
{
    private readonly IConfiguration _configuration = configuration;
    private const int MaxExtensionLength = 10;

    [HttpPost]
    [Route("c/{extension}")]
    public async Task<IActionResult> CreateUploadStream(string extension)
    {
        if (extension.Length > MaxExtensionLength)
            return BadRequest(ErrorResponse.ExtensionTooLong());
        var uploadStreamId = await uploadService.CreateUploadStream(extension);
        return Ok(new
        {
            stream = uploadStreamId
        });
    }

    [HttpPost]
    [Route("c/{extension}/{name}")]
    public async Task<IActionResult> CreateUploadStream(string extension, string name)
    {
        if (extension.Length > MaxExtensionLength)
            return BadRequest(ErrorResponse.ExtensionTooLong());
        var uploadStreamId = await uploadService.CreateUploadStream(extension, name);
        return Ok(new
        {
            stream = uploadStreamId
        });
    }

    [HttpPost]
    [Route("r/{streamId}")]
    public async Task<IActionResult> TerminateUploadStream(string streamId)
    {
        if (!uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        await uploadService.TerminateUploadStream(streamId);
        return Ok(new
        {
            success = true
        });
    }

    [HttpPost]
    [Route("f/{streamId}/{hash}")]
    public async Task<IActionResult> FinishUploadStream(string streamId, string hash)
    {
        var uploadItem = await uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        uploadItem.Hash = await uploadService.FinalizeHash(uploadItem.Hasher);
        if (uploadItem.Hash != hash)
            return BadRequest(ErrorResponse.HashMismatch);
        var uploadId = await uploadService.FinishUploadStream(uploadItem);
        return Ok(new
        {
            id = uploadId
        });
    }

    [HttpPost]
    [Route("u/{streamId}")]
    public async Task<IActionResult> UploadChunk(string streamId)
    {
        if (!uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var uploadItem = await uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        await uploadService.UploadChunk(uploadItem, Request.Body);
        return Ok(new
        {
            success = true
        });
    }

    [HttpPost]
    [Route("u/{streamId}/{hash}")]
    public async Task<IActionResult> UploadChunkWithHash(string streamId, string hash)
    {
        var uploadItem = await uploadService.GetUploadItem(streamId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var equal = await uploadService.UploadChunk(uploadItem, Request.Body, hash);
        if (!equal)
            return BadRequest(ErrorResponse.HashMismatch);
        return Ok(new
        {
            success = true
        });
    }

    [HttpGet]
    [Route("ws")]
    public async Task WebSocket()
    {
              
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await uploadService.HandleWebSocket(webSocket);
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }  
    }

    [HttpGet]
    [Route("d/{uploadId}")]
    public async Task<IActionResult> DownloadFile(string uploadId)
    {
        var (uploadItem, path) = await uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);
        var mimeType = await uploadService.GetMimeType(uploadItem.Extension);
        return PhysicalFile(path, mimeType ?? "application/octet-stream",
            uploadItem.Name != null
                ? uploadItem.Name + '.' + uploadItem.Extension
                : uploadItem.Hash + '.' + uploadItem.Extension);
    }
}