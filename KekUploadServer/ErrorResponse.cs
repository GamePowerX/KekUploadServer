using System.Diagnostics.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KekUploadServer;

public class ErrorResponse
{
    private ErrorResponse(string generic, string field, string error)
    {
        Generic = generic;
        Field = field;
        Error = error;
    }

    [JsonPropertyName("generic")] public string Generic { get; set; }

    [JsonPropertyName("field")] public string Field { get; set; }

    [JsonPropertyName("error")] public string Error { get; set; }

    // User errors
    public static ErrorResponse FileWithIdNotFound => new("NOT_FOUND", "ID", "File with id not found");
    public static ErrorResponse UploadStreamNotFound => new("NOT_FOUND", "STREAM", "Stream not found");
    public static ErrorResponse HashMismatch => new("HASH_MISMATCH", "HASH", "Hash doesn't match");

    // Server errors
    public static ErrorResponse InternalServerError => new("INTERNAL_SERVER_ERROR", "GENERIC", "Internal server error");

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public override string ToString()
    {
        return ToJson();
    }

    [Pure]
    public static implicit operator string(ErrorResponse errorResponse)
    {
        return errorResponse.ToJson();
    }

    public static ErrorResponse ExtensionTooLong(int maxLength = 10)
    {
        return new ErrorResponse("PARAM_LENGTH", "EXTENSION", $"Extension must be in bounds of 0-{maxLength}");
    }

    public static ErrorResponse InternalServerErrorWithMessage(string message)
    {
        return new ErrorResponse("INTERNAL_SERVER_ERROR", "GENERIC", message);
    }

    public static ErrorResponse InternalServerErrorWithException(Exception exception)
    {
        return new ErrorResponse("INTERNAL_SERVER_ERROR", nameof(exception), exception.Message);
    }
}