using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IAddressService
{
    Task<List<AddressDto>> GetCurrentUserAddressesAsync();
    Task<AddressDto> GetByIdAsync(Guid id);
    Task<AddressDto> CreateAsync(CreateAddressDto dto);
    Task<AddressDto> UpdateAsync(Guid id, UpdateAddressDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<AddressDto> SetDefaultAsync(Guid id);
    Task<Address?> GetDefaultShippingAddressAsync(Guid userId);
}