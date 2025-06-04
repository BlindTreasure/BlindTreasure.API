using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IProductService
{
    Task<ProductDto> CreateAsync(ProductSellerCreateDto dto);
    Task<ProductDto> DeleteAsync(Guid id);
    Task<Pagination<ProductDto>> GetAllAsync(ProductQueryParameter param);
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<ProductDto> UpdateAsync(Guid id, ProductUpdateDto dto, IFormFile productImageUrl);
    Task<string?> UploadProductImageAsync(Guid productId, IFormFile file);
}