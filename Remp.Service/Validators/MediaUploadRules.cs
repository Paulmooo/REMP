using Microsoft.AspNetCore.Http;
using Remp.Models.Enums;

namespace Remp.Service.Validators;

public static class MediaUploadRules
{
    public const long MiB = 1024 * 1024;
    public const long MaxBatchBytes = 100 * MiB;
    public const int MaxPictures = 20;

    // Validate the entire batch before creating any blobs. ContentType is untrusted.
    public static async Task ValidateAsync(List<IFormFile> files, MediaType type)
    {
        if (type == MediaType.VRTour)
            throw new ArgumentException("VR upload format has not been agreed yet.");
        if (files.Count > MaxPictures || files.Sum(f => f.Length) > MaxBatchBytes)
            throw new ArgumentException("Upload at most 20 pictures and 100 MiB per batch.");
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var image = extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif";
            var allowed = type switch
            {
                MediaType.Picture => image,
                MediaType.FloorPlan => image || extension == ".pdf",
                MediaType.Video => extension is ".mp4" or ".webm",
                _ => false
            };
            var limit = type == MediaType.Video ? MaxBatchBytes : 20 * MiB;
            if (!allowed || file.Length <= 0 || file.Length > limit)
                throw new ArgumentException("Invalid file type or size.");
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var count = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            if (!Matches(extension, header.AsSpan(0, count)))
                throw new ArgumentException("File header does not match its extension.");
        }
    }

    private static bool Matches(string extension, ReadOnlySpan<byte> h) => extension switch
    {
        ".jpg" or ".jpeg" => h.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        ".png" => h.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".gif" => h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8),
        ".webp" => h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h[8..12].SequenceEqual("WEBP"u8),
        ".pdf" => h.StartsWith("%PDF-"u8),
        ".mp4" => h.Length >= 12 && h[4..8].SequenceEqual("ftyp"u8),
        ".webm" => h.StartsWith(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
        _ => false
    };
}
