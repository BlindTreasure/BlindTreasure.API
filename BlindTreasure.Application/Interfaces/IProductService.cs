using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IProductService
{
    Task<ProducDetailstDto> CreateAsync(ProductCreateDto dto);
    Task<ProducDetailstDto> DeleteAsync(Guid id);
    Task<Pagination<ProducDetailstDto>> GetAllAsync(ProductQueryParameter param);
    Task<ProducDetailstDto?> GetByIdAsync(Guid id);
    Task<ProducDetailstDto> UpdateAsync(Guid id, ProductUpdateDto dto);
    Task<ProducDetailstDto> UpdateProductImagesAsync(Guid productId, List<IFormFile> images);
    Task<string?> UploadProductImageAsync(Guid productId, IFormFile file);
}