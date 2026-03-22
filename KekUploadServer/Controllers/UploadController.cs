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
    private const int MaxNameLength = 255;

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
        if (name.Length > MaxNameLength)
            return BadRequest(ErrorResponse.NameTooLong(MaxNameLength));
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
        if (!uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        var uploadId = await uploadService.FinalizeUpload(streamId, hash);
        if (uploadId != null)
            return Ok(new
            {
                id = uploadId
            });
        if (!uploadService.DoesUploadStreamExist(streamId))
            return NotFound(ErrorResponse.UploadStreamNotFound);
        return BadRequest(ErrorResponse.HashMismatch);
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

    [HttpGet]
    [Route("l/{uploadId}")]
    public async Task<IActionResult> GetFileLength(string uploadId)
    {
        var (uploadItem, path) = await uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);

        var fileInfo = new FileInfo(path);
        return Ok(new
        {
            size = fileInfo.Length
        });
    }

    [HttpGet]
    [Route("d/{uploadId}/{offset:long}/{size:long}")]
    public async Task<IActionResult> DownloadFileChunk(string uploadId, long offset, long size)
    {
        if (offset < 0 || size <= 0)
            return BadRequest(ErrorResponse.InvalidRange());

        var maxChunkSize = _configuration.GetValue("DownloadChunkMaxBytes", 8 * 1024 * 1024L);
        if (size > maxChunkSize)
            return BadRequest(ErrorResponse.ChunkSizeTooLarge(maxChunkSize));

        var (uploadItem, path) = await uploadService.GetUploadedItem(uploadId);
        if (uploadItem == null)
            return NotFound(ErrorResponse.FileWithIdNotFound);

        var fileInfo = new FileInfo(path);
        if (offset >= fileInfo.Length)
            return BadRequest(ErrorResponse.OffsetOutOfBounds());

        var bytesToRead = (int)Math.Min(size, fileInfo.Length - offset);
        var buffer = new byte[bytesToRead];

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Position = offset;
        var read = 0;
        while (read < bytesToRead)
        {
            var currentRead = await stream.ReadAsync(buffer.AsMemory(read, bytesToRead - read));
            if (currentRead == 0)
                break;
            read += currentRead;
        }

        if (read != bytesToRead)
            Array.Resize(ref buffer, read);

        var mimeType = await uploadService.GetMimeType(uploadItem.Extension);
        return File(buffer, mimeType ?? "application/octet-stream");
    }
}