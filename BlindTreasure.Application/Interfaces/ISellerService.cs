﻿using BlindTreasure.Domain.DTOs.Pagination;
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
    Task<ProducDetailstDto?> GetProductByIdAsync(Guid id, Guid userId);
    Task<Pagination<ProducDetailstDto>> GetAllProductsAsync(ProductQueryParameter param, Guid userId);
    Task<ProducDetailstDto> CreateProductAsync(ProductSellerCreateDto dto);
    Task<ProducDetailstDto> UpdateProductAsync(Guid productId, ProductUpdateDto dto);
    Task<ProducDetailstDto> DeleteProductAsync(Guid productId);
    Task<ProducDetailstDto> UpdateSellerProductImagesAsync(Guid productId, List<IFormFile> images);
}