using BlindTreasure.Domain.DTOs.OrderDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerService
{
    Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file);
    Task<SellerProfileDto> GetSellerProfileByIdAsync(Guid sellerId);
    Task<SellerProfileDto> GetSellerProfileByUserIdAsync(Guid userId);
    Task<SellerDto> UpdateSellerInfoAsync(Guid userId, UpdateSellerInfoDto dto);
    Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination);
    Task<ProducDetailDto?> GetProductByIdAsync(Guid id, Guid userId);
    Task<Pagination<ProducDetailDto>> GetAllProductsAsync(ProductQueryParameter param, Guid userId);
    Task<ProducDetailDto?> CreateProductAsync(ProductSellerCreateDto dto);
    Task<ProducDetailDto?> UpdateProductAsync(Guid productId, ProductUpdateDto dto);
    Task<ProducDetailDto> DeleteProductAsync(Guid productId);
    Task<ProducDetailDto> UpdateSellerProductImagesAsync(Guid productId, List<IFormFile> images);
    Task<string> UpdateSellerAvatarAsync(Guid userId, IFormFile file);
    Task<Pagination<OrderDto>> GetSellerOrdersAsync(OrderQueryParameter param);
    Task<OrderDto> GetSellerOrderByIdAsync(Guid orderId);
}