using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IAddressService
{
    Task<List<AddressDto>> GetCurrentUserAddressesAsync();
    Task<AddressDto> GetAddressByIdAsync(Guid id);
    Task<AddressDto> CreateAddressAsync(CreateAddressDto dto);
    Task<AddressDto> UpdateAddressAsync(Guid id, UpdateAddressDto dto);
    Task<bool> DeleteAddressAsync(Guid id);
    Task<AddressDto> SetDefaultAddressAsync(Guid id);
    Task<Address?> GetDefaultShippingAddressAsync(Guid userId);
}