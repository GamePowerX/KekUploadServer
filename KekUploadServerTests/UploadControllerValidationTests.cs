using System.Net.WebSockets;
using KekUploadServer;
using KekUploadServer.Controllers;
using KekUploadServer.Models;
using KekUploadServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SharpHash.Interfaces;

namespace KekUploadServerTests;

public class UploadControllerValidationTests
{
    [Test]
    public async Task CreateUploadStream_WithTooLongName_ReturnsBadRequest()
    {
        var configuration = new ConfigurationBuilder().Build();
        var controller = new UploadController(configuration, new FakeUploadService());

        var result = await controller.CreateUploadStream("txt", new string('a', 256));

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);
        Assert.That(badRequest!.Value, Is.TypeOf<ErrorResponse>());
        var error = (ErrorResponse)badRequest.Value!;
        Assert.That(error.Field, Is.EqualTo("NAME"));
        Assert.That(error.Generic, Is.EqualTo("PARAM_LENGTH"));
    }

    [Test]
    public async Task FinishUploadStream_WithHashMismatch_ReturnsBadRequest()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new FakeUploadService
        {
            StreamExists = true,
            FinalizeUploadResult = null
        };
        var controller = new UploadController(configuration, service);

        var result = await controller.FinishUploadStream("stream", "invalid-hash");

        var badRequest = result as BadRequestObjectResult;
        Assert.That(badRequest, Is.Not.Null);
        Assert.That(badRequest!.Value, Is.TypeOf<ErrorResponse>());
        var error = (ErrorResponse)badRequest.Value!;
        Assert.That(error.Generic, Is.EqualTo("HASH_MISMATCH"));
        Assert.That(error.Field, Is.EqualTo("HASH"));
    }

    private sealed class FakeUploadService : IUploadService
    {
        public bool StreamExists { get; set; }
        public string? FinalizeUploadResult { get; set; } = "upload-id";

        public Task<string> CreateUploadStream(string extension, string? name = null) => Task.FromResult("stream");
        public bool DoesUploadStreamExist(string streamId) => StreamExists;
        public Task TerminateUploadStream(string streamId) => Task.CompletedTask;
        public Task<UploadItem?> GetUploadItem(string streamId) => Task.FromResult<UploadItem?>(null);
        public Task<string> FinalizeHash(IHash hash) => Task.FromResult(string.Empty);
        public Task<string?> FinalizeUpload(string streamId, string? expectedHash = null) => Task.FromResult(FinalizeUploadResult);
        public Task<string> FinishUploadStream(UploadItem uploadItem) => Task.FromResult("upload-id");
        public Task<bool> UploadChunk(UploadItem uploadItem, Stream requestBody, string? hash = null) => Task.FromResult(true);
        public Task<bool> UploadChunk(UploadItem uploadItem, byte[] data, string? hash = null, int offset = 0, int? count = null) => Task.FromResult(true);
        public Task<(UploadItem?, string)> GetUploadedItem(string uploadId) => Task.FromResult<(UploadItem?, string)>((null, string.Empty));
        public Task<string?> GetMimeType(string extension) => Task.FromResult<string?>("application/octet-stream");
        public Task<IReadOnlyList<UploadItem>> GetUploads() => Task.FromResult<IReadOnlyList<UploadItem>>(Array.Empty<UploadItem>());
        public Task HandleWebSocket(WebSocket webSocket) => Task.CompletedTask;
    }
}

