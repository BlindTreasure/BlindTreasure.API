using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services
{
    /// <summary>
    /// Service quản lý kho BlindBox đã mua của user (CustomerInventory).
    /// Lưu trữ các BlindBox đã thanh toán, hỗ trợ lấy danh sách, chi tiết, cập nhật trạng thái mở box, xóa mềm.
    /// </summary>
    public class CustomerInventoryService : ICustomerInventoryService
    {
        private readonly ICacheService _cacheService;
        private readonly IClaimsService _claimsService;
        private readonly ILoggerService _loggerService;
        private readonly IProductService _productService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;

        public CustomerInventoryService(
            ICacheService cacheService,
            IClaimsService claimsService,
            ILoggerService loggerService,
            IProductService productService,
            IUnitOfWork unitOfWork,
            IOrderService orderService)
        {
            _cacheService = cacheService;
            _claimsService = claimsService;
            _loggerService = loggerService;
            _productService = productService;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
        }

        /// <summary>
        /// Thêm 1 BlindBox vào kho của user (sau khi thanh toán thành công).
        /// </summary>
        public async Task<CustomerInventoryDto> CreateAsync(CreateCustomerInventoryDto dto, Guid? userId = null)
        {
            var uid = userId ?? _claimsService.CurrentUserId;
            if (uid == Guid.Empty)
                throw ErrorHelper.Unauthorized("User ID is required for creating customer inventory.");

            var blindBox = await _unitOfWork.BlindBoxes.GetByIdAsync(dto.BlindBoxId);
            if (blindBox == null || blindBox.IsDeleted)
                throw ErrorHelper.NotFound("BlindBox not found.");

            var entity = new CustomerInventory
            {
                UserId = uid,
                BlindBoxId = dto.BlindBoxId,
                OrderDetailId = dto.OrderDetailId,
                IsOpened = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = uid
            };

            var result = await _unitOfWork.CustomerInventories.AddAsync(entity);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync(GetCacheKey(entity.Id));
            _loggerService.Success($"[CreateAsync] CustomerInventory created for user {uid}, blindbox {blindBox.Id}.");
            return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(result);
        }

        /// <summary>
        /// Lấy chi tiết 1 BlindBox trong kho user theo Id.
        /// </summary>
        public async Task<CustomerInventoryDto?> GetByIdAsync(Guid id)
        {
            var cacheKey = GetCacheKey(id);
            var cached = await _cacheService.GetAsync<CustomerInventory>(cacheKey);
            if (cached != null && !cached.IsDeleted)
            {
                _loggerService.Info($"[GetByIdAsync] Cache hit for customer inventory {id}");
                return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(cached);
            }

            var entity = await _unitOfWork.CustomerInventories.GetByIdAsync(id, x => x.BlindBox, x => x.OrderDetail);
            if (entity == null || entity.IsDeleted)
                return null;

            await _cacheService.SetAsync(cacheKey, entity, TimeSpan.FromMinutes(30));
            _loggerService.Info($"[GetByIdAsync] Customer inventory {id} loaded from DB and cached.");
            return CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto(entity);
        }

        /// <summary>
        /// Lấy toàn bộ BlindBox đã mua của user hiện tại.
        /// </summary>
        public async Task<List<CustomerInventoryDto>> GetByUserIdAsync(Guid? userId = null)
        {
            var uid = userId ?? _claimsService.CurrentUserId;
            var items = await _unitOfWork.CustomerInventories.GetAllAsync(
            i => i.UserId == uid && !i.IsDeleted,
            i => i.BlindBox,
            i => i.OrderDetail
    );
            return items.Select(CustomerInventoryMapper.ToCustomerInventoryBlindBoxDto).ToList();
        }

        /// <summary>
        /// Đánh dấu BlindBox đã mở (IsOpened = true).
        /// </summary>
        public async Task<CustomerInventoryDto> MarkAsOpenedAsync(Guid id)
        {
            var entity = await _unitOfWork.CustomerInventories.GetByIdAsync(id);
            if (entity == null || entity.IsDeleted)
                throw ErrorHelper.NotFound("Customer inventory not found.");

            entity.IsOpened = true;
            entity.OpenedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = _claimsService.CurrentUserId;

            await _unitOfWork.CustomerInventories.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync(GetCacheKey(id));
            _loggerService.Success($"[MarkAsOpenedAsync] Customer inventory {id} marked as opened.");
            return await GetByIdAsync(id) ?? throw ErrorHelper.Internal("Failed to update customer inventory.");
        }

        /// <summary>
        /// Xóa mềm 1 BlindBox khỏi kho user.
        /// </summary>
        public async Task<bool> DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.CustomerInventories.GetByIdAsync(id);
            if (entity == null || entity.IsDeleted)
                throw ErrorHelper.NotFound("Customer inventory not found.");

            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            entity.DeletedBy = _claimsService.CurrentUserId;

            await _unitOfWork.CustomerInventories.Update(entity);
            await _unitOfWork.SaveChangesAsync();

            await _cacheService.RemoveAsync(GetCacheKey(id));
            _loggerService.Success($"[DeleteAsync] Customer inventory {id} deleted.");
            return true;
        }

        private static string GetCacheKey(Guid id) => $"customerinventory:{id}";
    }
}
