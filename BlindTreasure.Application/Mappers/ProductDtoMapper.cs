using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Application.Mappers;

public static class ProductDtoMapper
{
    public static ProducDetailDto ToProducDetailDto(Product product)
    {
        if (product == null) return null!;
        return new ProducDetailDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            CategoryId = product.CategoryId,
            Price = product.Price,
            TotalStockQuantity = product.TotalStockQuantity,
            ProductStockStatus = product.TotalStockQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock,
            Height = product.Height,
            Material = product.Material,
            ProductType = product.ProductType,
            Brand = product.Brand,
            Status = product.Status,
            ImageUrls = product.ImageUrls ?? new List<string>(),
            SellerId = product.SellerId,
            IsDeleted = product.IsDeleted,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt,
            DeletedAt = product.DeletedAt
        };
    }
}