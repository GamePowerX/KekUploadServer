using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KekUploadServerApi.Uploads;
using SharpHash.Interfaces;

namespace KekUploadServer.Models;

public class UploadItem : IUploadItem, IUploadedItem
{
    [MaxLength(32)]
    public string Id { get; set; } = string.Empty;
    [MaxLength(32)]
    public string UploadStreamId { get; init; } = string.Empty;
    [MaxLength(10)]
    public string Extension { get; init; } = string.Empty;
    [MaxLength(255)]
    public string? Name { get; init; }
    [MaxLength(40)]
    public string Hash { get; set; } = string.Empty;

    [NotMapped] public FileStream FileStream { get; init; } = null!;
    [NotMapped] public IHash Hasher { get; init; } = null!;
}