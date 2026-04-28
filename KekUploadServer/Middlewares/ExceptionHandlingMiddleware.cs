using System.Net;

namespace KekUploadServer.Middlewares;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception e)
        {
            logger.LogError(e, "An unhandled exception occurred");
            await HandleExceptionAsync(context, e);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        if (response.HasStarted) return;

        response.ContentType = "application/json";
        response.StatusCode = (int) HttpStatusCode.InternalServerError;
#if DEBUG
        await response.WriteAsync(ErrorResponse.InternalServerErrorWithMessage(exception.ToString()).ToJson());
#else
        await response.WriteAsync(ErrorResponse.InternalServerError.ToJson());
#endif
    }
}
