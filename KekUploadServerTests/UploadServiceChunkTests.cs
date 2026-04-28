using System.Security.Cryptography;
using KekUploadServer.Database;
using KekUploadServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KekUploadServerTests;

public class UploadServiceChunkTests
{
    [Test]
    public async Task UploadChunk_Stream_WritesAllBytesToTempFile()
    {
        var uploadDirectory = Path.Combine(Path.GetTempPath(), "kekupload-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadDirectory);

        var (service, provider) = CreateUploadService(uploadDirectory);
        try
        {
            var streamId = await service.CreateUploadStream("bin");
            var uploadItem = await service.GetUploadItem(streamId);
            Assert.That(uploadItem, Is.Not.Null);

            var payload = new byte[2 * 1024 * 1024];
            RandomNumberGenerator.Fill(payload);

            var success = await service.UploadChunk(uploadItem!, new MemoryStream(payload));

            Assert.That(success, Is.True);
            Assert.That(uploadItem!.FileStream.Length, Is.EqualTo(payload.Length));
        }
        finally
        {
            await provider.DisposeAsync();
            Directory.Delete(uploadDirectory, true);
        }
    }

    [Test]
    public async Task UploadChunk_WithWrongHash_ReturnsFalse()
    {
        var uploadDirectory = Path.Combine(Path.GetTempPath(), "kekupload-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadDirectory);

        var (service, provider) = CreateUploadService(uploadDirectory);
        try
        {
            var streamId = await service.CreateUploadStream("txt");
            var uploadItem = await service.GetUploadItem(streamId);
            Assert.That(uploadItem, Is.Not.Null);

            var payload = "hello-world"u8.ToArray();
            var success = await service.UploadChunk(uploadItem!, payload, "invalid-hash");

            Assert.That(success, Is.False);
            Assert.That(uploadItem!.FileStream.Length, Is.EqualTo(0));
        }
        finally
        {
            await provider.DisposeAsync();
            Directory.Delete(uploadDirectory, true);
        }
    }

    [Test]
    public async Task FinalizeUpload_WithSameHash_ReturnsNewUploadIdForExistingFile()
    {
        var uploadDirectory = Path.Combine(Path.GetTempPath(), "kekupload-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadDirectory);

        var (service, provider) = CreateUploadService(uploadDirectory);
        try
        {
            var payload = "same-content"u8.ToArray();
            var hash = Convert.ToHexString(SHA1.HashData(payload)).ToLowerInvariant();

            var firstStreamId = await service.CreateUploadStream("txt", "first");
            var firstUploadItem = await service.GetUploadItem(firstStreamId);
            Assert.That(firstUploadItem, Is.Not.Null);
            await service.UploadChunk(firstUploadItem!, payload);
            var firstUploadId = await service.FinalizeUpload(firstStreamId, hash);

            var secondStreamId = await service.CreateUploadStream("txt", "second");
            var secondUploadItem = await service.GetUploadItem(secondStreamId);
            Assert.That(secondUploadItem, Is.Not.Null);
            await service.UploadChunk(secondUploadItem!, payload);
            var secondUploadId = await service.FinalizeUpload(secondStreamId, hash);

            Assert.That(firstUploadId, Is.Not.Null);
            Assert.That(secondUploadId, Is.Not.Null);
            Assert.That(secondUploadId, Is.Not.EqualTo(firstUploadId));

            var (firstUploadedItem, firstPath) = await service.GetUploadedItem(firstUploadId!);
            var (secondUploadedItem, secondPath) = await service.GetUploadedItem(secondUploadId!);
            Assert.That(firstUploadedItem, Is.Not.Null);
            Assert.That(secondUploadedItem, Is.Not.Null);
            Assert.That(secondUploadedItem!.Name, Is.EqualTo("second"));
            Assert.That(secondPath, Is.EqualTo(firstPath));
            Assert.That(File.ReadAllBytes(secondPath), Is.EqualTo(payload));
        }
        finally
        {
            await provider.DisposeAsync();
            Directory.Delete(uploadDirectory, true);
        }
    }

    private static (UploadService Service, ServiceProvider Provider) CreateUploadService(string uploadDirectory)
    {
        var configValues = new Dictionary<string, string?>
        {
            ["UploadDirectory"] = uploadDirectory,
            ["IdLength"] = "12"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddMemoryCache();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<UploadDataContext>(options => options.UseInMemoryDatabase(databaseName));
        var provider = services.BuildServiceProvider();

        var memoryCache = provider.GetRequiredService<IMemoryCache>();
        var service = new UploadService(new HttpContextAccessor(), provider, configuration, memoryCache);
        return (service, provider);
    }
}
