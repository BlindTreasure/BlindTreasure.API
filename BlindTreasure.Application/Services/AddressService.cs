using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AddressDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    public class AddressService : IAddressService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;
        private readonly ILoggerService _logger;
        private readonly IClaimsService _claimsService;

        public AddressService(IUnitOfWork unitOfWork, ICacheService cacheService, ILoggerService logger, IClaimsService claimsService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
            _logger = logger;
            _claimsService = claimsService;
        }

        public async Task<List<AddressDto>> GetCurrentUserAddressesAsync()
        {
            var userId = _claimsService.CurrentUserId;
            var addresses = await _unitOfWork.Addresses.GetAllAsync(a => a.UserId == userId && !a.IsDeleted);
            // Sắp xếp: IsDefault=true lên đầu, còn lại sort theo UpdatedAt (nếu có) rồi CreatedAt giảm dần
            var sorted = addresses
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .ToList(); _logger.Info($"[GetCurrentUserAddressesAsync] Loaded from DB and cached for user {userId}");
            return sorted.Select(ToAddressDto).ToList();
        }

        public async Task<AddressDto> GetByIdAsync(Guid id)
        {
            var cacheKey = $"address:{id}";
            var cached = await _cacheService.GetAsync<Address>(cacheKey);
            if (cached != null)
            {
                _logger.Info($"[GetByIdAsync] Cache hit for address {id}");
                return ToAddressDto(cached);
            }

            var address = await _unitOfWork.Addresses.GetByIdAsync(id);
            if (address == null || address.IsDeleted)
            {
                _logger.Warn($"[GetByIdAsync] Address {id} not found.");
                throw ErrorHelper.NotFound("Không tìm thấy địa chỉ.");
            }

            await _cacheService.SetAsync(cacheKey, address, TimeSpan.FromHours(1));
            _logger.Info($"[GetByIdAsync] Address {id} loaded from DB and cached.");
            return ToAddressDto(address);
        }

        public async Task<AddressDto> CreateAsync(CreateAddressDto dto)
        {
            var userId = _claimsService.CurrentUserId;
            _logger.Info($"[CreateAsync] User {userId} creates new address.");

            // Kiểm tra xem user đã có địa chỉ nào chưa
            var existingAddresses = await _unitOfWork.Addresses.GetAllAsync(a => a.UserId == userId && !a.IsDeleted);
            bool isFirstAddress = existingAddresses == null || !existingAddresses.Any();


            var address = new Address
            {
                UserId = userId,
                FullName = dto.FullName,
                Phone = dto.Phone,
                AddressLine1 = dto.AddressLine1,
                //AddressLine2 = dto.AddressLine2,
                City = dto.City,
                Province = dto.Province,
                PostalCode = dto.PostalCode,
                IsDefault = dto.IsDefault,
                //Country = dto.Country,
            };

            if (!isFirstAddress && dto.IsDefault)
            {
                // Unset previous default addresses
                var userAddresses = await _unitOfWork.Addresses.GetAllAsync(a => a.UserId == userId && a.IsDefault && !a.IsDeleted);
                foreach (var addr in userAddresses)
                {
                    addr.IsDefault = false;
                    await _unitOfWork.Addresses.Update(addr);
                }

            }
            else
            {
                // Set this address as default if it's the first one or if IsDefault is true
                address.IsDefault = isFirstAddress;
            }



                await _unitOfWork.Addresses.AddAsync(address);
            await _unitOfWork.SaveChangesAsync();

            await RemoveAddressCacheAsync(userId, address.Id);
            _logger.Success($"[CreateAsync] Address created for user {userId}.");
            return ToAddressDto(address);
        }

        public async Task<AddressDto> UpdateAsync(Guid id, UpdateAddressDto dto)
        {
            var userId = _claimsService.CurrentUserId;
            var address = await _unitOfWork.Addresses.GetByIdAsync(id);
            if (address == null || address.IsDeleted || address.UserId != userId)
            {
                _logger.Warn($"[UpdateAsync] Address {id} not found or not owned by user {userId}.");
                throw ErrorHelper.NotFound("Không tìm thấy địa chỉ.");
            }

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                address.FullName = dto.FullName;
            if (!string.IsNullOrWhiteSpace(dto.Phone))
                address.Phone = dto.Phone;
            if (!string.IsNullOrWhiteSpace(dto.AddressLine1))
                address.AddressLine1 = dto.AddressLine1;
         
            if (!string.IsNullOrWhiteSpace(dto.City))
                address.City = dto.City;
            if (!string.IsNullOrWhiteSpace(dto.Province))
                address.Province = dto.Province;
            if (!string.IsNullOrWhiteSpace(dto.PostalCode))
                address.PostalCode = dto.PostalCode;
       

            await _unitOfWork.Addresses.Update(address);
            await _unitOfWork.SaveChangesAsync();

            await RemoveAddressCacheAsync(userId, address.Id);
            _logger.Success($"[UpdateAsync] Address {id} updated for user {userId}.");
            return ToAddressDto(address);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var userId = _claimsService.CurrentUserId;
            var address = await _unitOfWork.Addresses.GetByIdAsync(id);
            if (address == null || address.IsDeleted || address.UserId != userId)
            {
                _logger.Warn($"[DeleteAsync] Address {id} not found or not owned by user {userId}.");
                throw ErrorHelper.NotFound("Không tìm thấy địa chỉ.");
            }

            await _unitOfWork.Addresses.SoftRemove(address);
            await _unitOfWork.SaveChangesAsync();

            await RemoveAddressCacheAsync(userId, address.Id);
            _logger.Success($"[DeleteAsync] Address {id} deleted for user {userId}.");
            return true;
        }

        public async Task<AddressDto> SetDefaultAsync(Guid id)
        {
            var userId = _claimsService.CurrentUserId;
            var address = await _unitOfWork.Addresses.GetByIdAsync(id);
            if (address == null || address.IsDeleted || address.UserId != userId)
            {
                _logger.Warn($"[SetDefaultAsync] Address {id} not found or not owned by user {userId}.");
                throw ErrorHelper.NotFound("Không tìm thấy địa chỉ.");
            }

            // Unset previous default addresses
            var userAddresses = await _unitOfWork.Addresses.GetAllAsync(a => a.UserId == userId && a.IsDefault && !a.IsDeleted && a.Id != id);
            foreach (var addr in userAddresses)
            {
                addr.IsDefault = false;
                await _unitOfWork.Addresses.Update(addr);
            }

            address.IsDefault = true;
            await _unitOfWork.Addresses.Update(address);
            await _unitOfWork.SaveChangesAsync();

            await RemoveAddressCacheAsync(userId, address.Id);
            _logger.Success($"[SetDefaultAsync] Address {id} set as default for user {userId}.");
            return ToAddressDto(address);
        }

        //private method

        private async Task RemoveAddressCacheAsync(Guid userId, Guid addressId)
        {
            await _cacheService.RemoveAsync($"address:{addressId}");
            await _cacheService.RemoveAsync($"address:user:{userId}");
        }

        private static AddressDto ToAddressDto(Address address)
        {
            return new AddressDto
            {
                Id = address.Id,
                UserId = address.UserId,
                FullName = address.FullName,
                Phone = address.Phone,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                Province = address.Province,
                PostalCode = address.PostalCode,
                Country = address.Country,
                IsDefault = address.IsDefault
            };
        }
    }
}
