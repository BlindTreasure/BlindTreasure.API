using BlindTreasure.Domain.DTOs.InventoryItemDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IInventoryItemService
{
    Task<InventoryItemDto> CreateAsync(CreateInventoryItemDto dto, Guid? userId);
    Task<bool> DeleteAsync(Guid id);
    Task<List<InventoryItemDto>> GetByUserIdAsync(Guid? userId = null);
    Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto);
    Task<InventoryItemDto?> GetByIdAsync(Guid id);
}