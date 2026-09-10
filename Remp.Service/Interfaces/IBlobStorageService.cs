using System;

namespace Remp.Service.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName);
    Task DeleteIfExistsAsync(string fileName);
}
