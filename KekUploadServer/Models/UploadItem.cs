using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KekUploadServerApi.Uploads;
using SharpHash.Interfaces;

namespace KekUploadServer.Models;

/// <summary>
/// Represents both persisted upload metadata and transient state while an upload is in progress.
/// </summary>
public class UploadItem : IUploadItem, IUploadedItem
{
    /// <summary>
    /// Public id used by download and metadata endpoints.
    /// </summary>
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Internal stream id used as the backing file name on disk.
    /// </summary>
    [MaxLength(64)]
    public string UploadStreamId { get; init; } = string.Empty;

    /// <summary>
    /// File extension without a leading dot.
    /// </summary>
    [MaxLength(10)]
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Optional client-provided download name without extension.
    /// </summary>
    [MaxLength(255)]
    public string? Name { get; init; }

    /// <summary>
    /// SHA-1 hash of the completed file content.
    /// </summary>
    [MaxLength(40)]
    public string Hash { get; set; } = string.Empty;

    /// <summary>
    /// Temporary file stream used while chunks are still being uploaded.
    /// </summary>
    [NotMapped] public FileStream FileStream { get; init; } = null!;

    /// <summary>
    /// Incremental hasher updated as chunks are written.
    /// </summary>
    [NotMapped] public IHash Hasher { get; init; } = null!;
}
