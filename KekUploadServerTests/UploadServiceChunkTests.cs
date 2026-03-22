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
        services.AddDbContext<UploadDataContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        var provider = services.BuildServiceProvider();

        var memoryCache = provider.GetRequiredService<IMemoryCache>();
        var service = new UploadService(new HttpContextAccessor(), provider, configuration, memoryCache);
        return (service, provider);
    }
}

