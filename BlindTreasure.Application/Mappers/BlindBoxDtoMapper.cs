using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Application.Mappers;

public static class BlindBoxDtoMapper
{
    public static BlindBoxDetailDto ToBlindBoxDetailDto(BlindBox? box)
    {
        if (box == null) return null!;
        return new BlindBoxDetailDto
        {
            Id = box.Id,
            CategoryId = box.CategoryId,
            Name = box.Name,
            Description = box.Description,
            Price = box.Price,
            TotalQuantity = box.TotalQuantity,
            BlindBoxStockStatus = box.TotalQuantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock,
            CategoryName = box.Category?.Name,
            ImageUrl = box.ImageUrl,
            ReleaseDate = box.ReleaseDate,
            CreatedAt = box.CreatedAt,
            Status = box.Status,
            HasSecretItem = box.HasSecretItem,
            Brand = box.Brand,
            SecretProbability = box.SecretProbability,
            RejectReason = box.RejectReason,
            BindBoxTags = box.BindBoxTags,
            IsDeleted = box.IsDeleted,
            Items = box.BlindBoxItems?
                .Where(i => !i.IsDeleted)
                .Select(ToBlindBoxItemDto)
                .ToList()
        };
    }

    /// <summary>
    ///     Chuyển đổi BlindBoxItem entity sang BlindBoxItemDto.
    /// </summary>
    public static BlindBoxItemResponseDto ToBlindBoxItemDto(BlindBoxItem item)
    {
        if (item == null) return null!;
        return new BlindBoxItemResponseDto
        {
            ProductId = item.ProductId,
            ProductName = item.Product?.Name,
            Quantity = item.Quantity,
            DropRate = item.DropRate,
            ImageUrl = item.Product?.ImageUrls?.FirstOrDefault()
        };
    }
}