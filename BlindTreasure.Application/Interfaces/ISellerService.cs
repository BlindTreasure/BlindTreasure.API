using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerService
{
    Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file);
    Task<string> GetSellerDocumentUrlAsync(Guid sellerId);
    Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination);
}