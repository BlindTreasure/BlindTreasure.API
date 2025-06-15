using BlindTreasure.Domain.DTOs.AddressDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces
{
    public interface IAddressService
    {
        Task<List<AddressDto>> GetCurrentUserAddressesAsync();
        Task<AddressDto> GetByIdAsync(Guid id);
        Task<AddressDto> CreateAsync(CreateAddressDto dto);
        Task<AddressDto> UpdateAsync(Guid id, UpdateAddressDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<AddressDto> SetDefaultAsync(Guid id);
    }
}
