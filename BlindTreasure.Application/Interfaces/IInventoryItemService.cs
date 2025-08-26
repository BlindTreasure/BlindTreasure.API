using BlindTreasure.Application.Services;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IInventoryItemService
{
    Task<InventoryItemDto> CreateAsync(CreateInventoryItemDto dto, Guid? userId);
    Task<bool> DeleteAsync(Guid id);
    Task<Pagination<InventoryItemDto>> GetMyInventoryAsync(InventoryItemQueryParameter param);
    Task<List<InventoryItemDto>> GetMyUnboxedItemsFromBlindBoxAsync(Guid blindBoxId);
    Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto);
    Task<InventoryItemDto?> GetByIdAsync(Guid id);
    Task<List<ShipmentCheckoutResponseDTO>> PreviewShipmentForListItemsAsync(RequestItemShipmentDTO request);
    Task<ShipmentItemResponseDTO> RequestShipmentAsync(RequestItemShipmentDTO request);
    Task<InventoryItemDto> ForceReleaseHeldItemAsync(Guid inventoryItemId);
}