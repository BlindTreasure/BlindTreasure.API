using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface ICustomerInventoryService
    {
        Task<CustomerInventoryDto> CreateAsync(CreateCustomerInventoryDto dto, Guid? userId = null);
        Task<bool> DeleteAsync(Guid id);
        Task<CustomerInventoryDto?> GetByIdAsync(Guid id);
        Task<List<CustomerInventoryDto>> GetByUserIdAsync(Guid? userId = null);
        Task<CustomerInventoryDto> MarkAsOpenedAsync(Guid id);
    }
}
