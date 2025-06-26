using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IProductService
{
    Task<ProducDetailDto> CreateAsync(ProductCreateDto dto);
    Task<ProducDetailDto> DeleteAsync(Guid id);
    Task<Pagination<ProducDetailDto>> GetAllAsync(ProductQueryParameter param);
    Task<ProducDetailDto?> GetByIdAsync(Guid id);
    Task<ProducDetailDto> UpdateAsync(Guid id, ProductUpdateDto dto);
    Task<ProducDetailDto> UpdateProductImagesAsync(Guid productId, List<IFormFile> images);
    Task<string?> UploadProductImageAsync(Guid productId, IFormFile file);
}