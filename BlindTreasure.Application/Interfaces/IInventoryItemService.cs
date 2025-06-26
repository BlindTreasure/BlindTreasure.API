using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface IInventoryItemService
    {
        Task<InventoryItemDto> CreateAsync(CreateInventoryItemDto dto, Guid? userId);
        Task<bool> DeleteAsync(Guid id);
        Task<List<InventoryItemDto>> GetByUserIdAsync(Guid? userId = null);
        Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto);
        Task<InventoryItemDto?> GetByIdAsync(Guid id);
    }
}
