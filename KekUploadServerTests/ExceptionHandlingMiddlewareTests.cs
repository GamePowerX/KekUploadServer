using System.Text.Json;
using KekUploadServer.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace KekUploadServerTests;

public class ExceptionHandlingMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_ReturnsInternalServerError_WithExpectedDetailLevel()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("sensitive-details"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await middleware.InvokeAsync(context);

        Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        responseBody.Position = 0;
        var json = await new StreamReader(responseBody).ReadToEndAsync();
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.That(root.GetProperty("generic").GetString(), Is.EqualTo("INTERNAL_SERVER_ERROR"));
#if DEBUG
        Assert.That(root.GetProperty("error").GetString(), Does.Contain("InvalidOperationException"));
        Assert.That(root.GetProperty("error").GetString(), Does.Contain("sensitive-details"));
#else
        Assert.That(root.GetProperty("error").GetString(), Is.EqualTo("Internal server error"));
#endif
    }
}

