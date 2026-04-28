using System.Net.WebSockets;
using KekUploadServer.Database;
using KekUploadServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KekUploadServerTests;

public class UploadServiceWebSocketTests
{
    [Test]
    public async Task HandleWebSocket_WhenReceiveClosesPrematurely_DoesNotThrow()
    {
        var uploadDirectory = Path.Combine(Path.GetTempPath(), "kekupload-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(uploadDirectory);

        var (service, provider) = CreateUploadService(uploadDirectory);
        try
        {
            await service.HandleWebSocket(new PrematureCloseWebSocket());
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
            ["UploadDirectory"] = uploadDirectory
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

    private sealed class PrematureCloseWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State { get; } = WebSocketState.Open;
        public override string? SubProtocol => null;

        public override void Abort()
        {
        }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public override void Dispose()
        {
        }

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer,
            CancellationToken cancellationToken) => throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
