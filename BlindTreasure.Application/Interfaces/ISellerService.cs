using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerService
{
    Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file);
}