using System.ComponentModel.DataAnnotations.Schema;
using KekUploadServerApi.Uploads;
using SharpHash.Interfaces;

namespace KekUploadServer.Models;

public class UploadItem : IUploadItem, IUploadedItem
{
    public string Id { get; set; } = string.Empty;
    public string UploadStreamId { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string Hash { get; set; } = string.Empty;

    [NotMapped] public FileStream FileStream { get; set; } = null!;
    [NotMapped] public IHash Hasher { get; set; } = null!;
}