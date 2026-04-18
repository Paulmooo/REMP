using System;

namespace Recam.Service.Interfaces;

public interface IMediaAssetService
{
    Task DeleteMediaAssetAsync(int id, string userId);
}
