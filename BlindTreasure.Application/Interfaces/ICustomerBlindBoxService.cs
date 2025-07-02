using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ICustomerBlindBoxService
{
    Task<CustomerInventoryDto> CreateAsync(CreateCustomerInventoryDto dto, Guid? userId = null);
    Task<bool> DeleteAsync(Guid id);
    Task<CustomerInventoryDto?> GetByIdAsync(Guid id);
    Task<Pagination<CustomerInventoryDto>> GetMyBlindBoxesAsync(CustomerBlindBoxQueryParameter param);
    Task<CustomerInventoryDto> MarkAsOpenedAsync(Guid id);
}