using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ICustomerInventoryService
{
    Task<CustomerInventoryDto> CreateAsync(CreateCustomerInventoryDto dto, Guid? userId = null);
    Task<bool> DeleteAsync(Guid id);
    Task<CustomerInventoryDto?> GetByIdAsync(Guid id);
    Task<List<CustomerInventoryDto>> GetByUserIdAsync(Guid? userId = null);
    Task<CustomerInventoryDto> MarkAsOpenedAsync(Guid id);
}