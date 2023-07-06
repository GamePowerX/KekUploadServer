namespace KekUploadServer.Models;

public class UploadItem
{
    public string Id { get; set; } = string.Empty;
    public string UploadStreamId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Hash { get; set; } = string.Empty;
}