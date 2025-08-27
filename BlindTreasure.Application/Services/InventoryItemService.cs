using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Application.Utils.SharedCacheKeys;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class InventoryItemService : IInventoryItemService
{
    private readonly ICacheService _cacheService;
    private readonly ICategoryService _categoryService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGhnShippingService _ghnShippingService;
    private readonly IStripeService _stripeService;


    public InventoryItemService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IUnitOfWork unitOfWork,
        ICategoryService categoryService,
        IGhnShippingService ghnShippingService,
        IStripeService stripeService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _categoryService = categoryService; // initialize categoryService
        _ghnShippingService = ghnShippingService; // initialize ghnShippingService
        _stripeService = stripeService; // initialize stripeService
    }

    public async Task<Pagination<InventoryItemDto>> GetOnHoldInventoryItemByUser(PaginationParameter param, Guid userId)
    {
        // Build base query
        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => !i.IsDeleted && i.Status == InventoryItemStatus.OnHold)
            .Include(i => i.Product)
            .ThenInclude(p => p.Category)
            .Include(i => i.Listings)
            .Include(i => i.Shipment)
            .AsNoTracking();

        // Filter theo user nếu được truyền
        if (userId != Guid.Empty) query = query.Where(i => i.UserId == userId);

        // Sort theo UpdatedAt/CreatedAt theo param.Desc
        query = param.Desc
            ? query.OrderByDescending(i => i.UpdatedAt ?? i.CreatedAt)
            : query.OrderBy(i => i.UpdatedAt ?? i.CreatedAt);

        // Count total
        var count = await query.CountAsync();

        List<InventoryItem> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(item =>
        {
            var dto = InventoryItemMapper.ToInventoryItemDtoFullIncluded(item);
            dto.HoldInfo = BuildHoldInfo(item);
            dto.IsOnHold = dto.HoldInfo.IsOnHold; // đồng bộ flag cũ
            return dto;
        }).ToList();

        _loggerService.Info(
            $"[GetOnHoldInventoryItemByUser] Admin requested on-hold items for user {userId}. Returned {dtos.Count}/{count}.");

        return new Pagination<InventoryItemDto>(dtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<InventoryItemDto> ForceReleaseHeldItemAsync(Guid inventoryItemId)
    {
        _loggerService.Warn(
            $"[ForceReleaseHeldItemAsync] Admin forcing release hold for InventoryItem {inventoryItemId}");

        var item = await _unitOfWork.InventoryItems.GetByIdAsync(inventoryItemId,
            i => i.Product,
            i => i.Listings);

        if (item == null)
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm trong kho.");

        if (item.Status != InventoryItemStatus.OnHold)
            throw ErrorHelper.BadRequest("Vật phẩm không ở trạng thái OnHold, không cần force release.");

        // Reset trạng thái
        item.Status = InventoryItemStatus.Available;
        item.HoldUntil = null;
        item.LockedByRequestId = null;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache per-item
        await _cacheService.RemoveAsync(GetCacheKey(inventoryItemId));
        // Invalidate user-items cache (item đã chuyển sang Available => ảnh hưởng danh sách)
        await _cacheService.RemoveAsync(ListingSharedCacheKeys.GetUserAvailableItems(item.UserId));

        // Map sang DTO trả về
        var dto = InventoryItemMapper.ToInventoryItemDto(item);
        dto.ProductName = item.Product?.Name ?? "hehe";
        dto.Image = item.Product?.ImageUrls?.FirstOrDefault() ?? "";
        dto.HasActiveListing = item.Listings?.Any(l => l.Status == ListingStatus.Active) ?? false;
        dto.IsOnHold = false;

        _loggerService.Success($"[ForceReleaseHeldItemAsync] Đã force release item {inventoryItemId}");

        return dto;
    }

    public async Task<List<InventoryItemDto>> GetMyUnboxedItemsFromBlindBoxAsync(Guid blindBoxId)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.UserId == userId
                        && i.IsFromBlindBox
                        && !i.IsDeleted
                        && i.SourceCustomerBlindBox != null
                        && i.SourceCustomerBlindBox.BlindBoxId == blindBoxId).Include(x => x.Shipment)
            .Include(i => i.Product)
            .Include(i => i.SourceCustomerBlindBox)
            .AsNoTracking();

        var result = await query.ToListAsync();

        return result.Select(item =>
        {
            var dto = InventoryItemMapper.ToInventoryItemDto(item);
            dto.HoldInfo = BuildHoldInfo(item);
            dto.IsOnHold = dto.HoldInfo.IsOnHold;
            return dto;
        }).ToList();
    }

    public async Task<InventoryItemDto> CreateAsync(CreateInventoryItemDto dto, Guid? userId)
    {
        if (userId.HasValue)
        {
            userId = userId.Value;
        }
        else
        {
            userId = _claimsService.CurrentUserId;
            if (userId == Guid.Empty)
                throw ErrorHelper.Unauthorized(
                    "User ID is required for creating inventory item. Cannot found current user");
        }

        _loggerService.Info($"[CreateAsync] Creating inventory item for user {userId}, product {dto.ProductId}.");
        var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId, x => x.Seller);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Product not found.");


        // Lấy địa chỉ giao hàng mặc định của user
        var address = await _unitOfWork.Addresses.GetQueryable()
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .FirstOrDefaultAsync();
        if (address == null)
            throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");

        var item = new InventoryItem
        {
            UserId = userId.Value,
            ProductId = dto.ProductId,
            Location = product.Seller.CompanyAddress ?? string.Empty,
            Status = dto.Status
        };

        if (dto.OrderDetailId.HasValue)
        {
            var orderDetail = await _unitOfWork.OrderDetails.GetByIdAsync(dto.OrderDetailId.Value);
            if (orderDetail == null || orderDetail.IsDeleted)
                throw ErrorHelper.NotFound("Order detail not found.");
            item.OrderDetailId = dto.OrderDetailId.Value;
            item.OrderDetail = orderDetail;
        }

        if (dto.ShipmentId.HasValue)
        {
            var shipment = await _unitOfWork.Shipments.GetByIdAsync(dto.ShipmentId.Value);
            if (shipment == null || shipment.IsDeleted)
                throw ErrorHelper.NotFound("Shipment not found.");
            item.ShipmentId = dto.ShipmentId.Value;
            item.Shipment = shipment;
        }

        var result = await _unitOfWork.InventoryItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();
        // Invalidate cache per-item
        await _cacheService.RemoveAsync(GetCacheKey(item.Id));
        // Invalidate cache user-items (listing), vì user có thêm 1 item mới
        await _cacheService.RemoveAsync(ListingSharedCacheKeys.GetUserAvailableItems(item.UserId));

        _loggerService.Success($"[CreateAsync] Inventory item created for user {userId}, product {product.Name}.");
        return InventoryItemMapper.ToInventoryItemDto(result) ??
               throw ErrorHelper.Internal("Failed to create inventory item.");
    }

    public async Task<InventoryItemDto?> GetByIdAsync(Guid id)
    {
        var item = await _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.Id == id && !i.IsDeleted)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Include(i => i.Product).ThenInclude(p => p.Seller)
            .Include(i => i.OrderDetail)
            .Include(i => i.Shipment)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (item == null || item.IsDeleted)
            return null;

        var dto = InventoryItemMapper.ToInventoryItemDtoFullIncluded(item);
        dto.HoldInfo = BuildHoldInfo(item);
        dto.IsOnHold = dto.HoldInfo.IsOnHold;
        return dto;
    }

    public async Task<Pagination<InventoryItemDto>> GetMyInventoryAsync(InventoryItemQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Include(i => i.Shipment)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Include(i => i.OrderDetail)
            .AsNoTracking();

        // Filter theo tên sản phẩm
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(i => i.Product.Name.ToLower().Contains(keyword));
        }


        // Filter theo category
        if (param.CategoryId.HasValue)
        {
            // Lấy tất cả category con nếu cần
            var categoryIds = await _categoryService.GetAllChildCategoryIdsAsync(param.CategoryId.Value);
            query = query.Where(i => categoryIds.Contains(i.Product.CategoryId));
        }

        if (param.IsFromBlindBox.HasValue)
            query = query.Where(i => i.IsFromBlindBox == param.IsFromBlindBox.Value);

        // Filter theo status
        if (param.Status.HasValue)
            query = query.Where(i => i.Status == param.Status.Value);

        // Sort: UpdatedAt/CreatedAt theo hướng param.Desc
        query = param.Desc
            ? query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt)
            : query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);


        var count = await query.CountAsync();

        List<InventoryItem> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(item =>
        {
            var dto = InventoryItemMapper.ToInventoryItemDtoFullIncluded(item);
            dto.HoldInfo = BuildHoldInfo(item);
            dto.IsOnHold = dto.HoldInfo.IsOnHold;
            return dto;
        }).ToList();

        return new Pagination<InventoryItemDto>(dtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id, i => i.Product);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound(
                "Vật phẩm trong kho với mã định danh đã cung cấp không tồn tại hoặc đã bị xóa. Vui lòng kiểm tra lại mã vật phẩm.");

        if (!string.IsNullOrWhiteSpace(dto.Location))
            item.Location = dto.Location;
        if (dto.Status.HasValue)
            item.Status = dto.Status.Value;

        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveAsync(GetCacheKey(id));
        await _cacheService.RemoveAsync(ListingSharedCacheKeys.GetUserAvailableItems(item.UserId));

        _loggerService.Success($"[UpdateAsync] Inventory item {id} updated.");
        return await GetByIdAsync(id) ??
               throw ErrorHelper.Internal("Đã xảy ra lỗi trong quá trình cập nhật vật phẩm trong kho.");
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound(
                "Vật phẩm trong kho với mã định danh đã cung cấp không tồn tại hoặc đã bị xóa. Vui lòng kiểm tra lại mã vật phẩm.");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();
        // Invalidate cache per-item
        await _cacheService.RemoveAsync(GetCacheKey(id));
        // Invalidate user-items cache
        await _cacheService.RemoveAsync(ListingSharedCacheKeys.GetUserAvailableItems(item.UserId));

        _loggerService.Success($"[DeleteAsync] Inventory item {id} deleted.");
        return true;
    }


    /// <summary>
    /// Yêu cầu giao hàng cho một InventoryItem.
    /// Nếu chưa có địa chỉ thì phải truyền vào addressId.
    /// Tạo Shipment và cập nhật trạng thái OrderDetail liên quan.
    /// </summary>
    public async Task<ShipmentItemResponseDTO> RequestShipmentAsync(RequestItemShipmentDTO request)
    {
        var userId = _claimsService.CurrentUserId;
        if (request.InventoryItemIds == null || !request.InventoryItemIds.Any())
            throw ErrorHelper.BadRequest("Danh sách inventory item rỗng.");

        // Lấy inventory item của user
        var items = await _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => request.InventoryItemIds.Contains(i.Id) && i.UserId == userId && !i.IsDeleted)
            .Include(i => i.Product).ThenInclude(p => p.Seller)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .Include(i => i.OrderDetail)
            .ToListAsync();

        if (!items.Any())
            throw ErrorHelper.BadRequest("Không tìm thấy inventory item hợp lệ.");

        // Lấy địa chỉ giao hàng mặc định của user
        var address = await _unitOfWork.Addresses.GetQueryable()
            .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
            .FirstOrDefaultAsync();
        if (address == null)
            throw ErrorHelper.BadRequest("Không tìm thấy địa chỉ mặc định của khách hàng.");

        // Group theo seller
        var sellerGroups = items
            .Where(i => i.Product != null && i.Product.Seller != null)
            .GroupBy(i => i.Product.SellerId);

        var shipments = new List<Shipment>();
        var shipmentDtos = new List<ShipmentDto>();
        var totalShippingFee = 0;

        foreach (var group in sellerGroups)
        {
            var seller = group.First().Product.Seller;
            if (seller == null) continue;

            // Gộp inventory item cùng ProductId
            var ghnOrderItems = group
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var p = g.First().Product;
                    var length = Convert.ToInt32(p.Length ?? 10);
                    var width = Convert.ToInt32(p.Width ?? 10);
                    var height = Convert.ToInt32(p.Height ?? 10);
                    var weight = Convert.ToInt32(p.Weight ?? 1000);

                    return new GhnOrderItemDto
                    {
                        Name = p.Name,
                        Code = p.Id.ToString(),
                        Quantity = g.Count(),
                        Price = Convert.ToInt32(p.RealSellingPrice),
                        Length = length,
                        Width = width,
                        Height = height,
                        Weight = weight,
                        Category = new GhnItemCategory
                        {
                            Level1 = p.Category?.Name,
                            Level2 = p.Category?.Parent?.Name,
                            Level3 = p.Category?.Parent?.Parent?.Name
                        }
                    };
                }).ToList();

            var ghnOrderRequest = new GhnOrderRequest
            {
                PaymentTypeId = 2,
                Note = $"Giao hàng cho seller {seller.CompanyName}",
                RequiredNote = "CHOXEMHANGKHONGTHU",
                FromName = seller.CompanyName ?? "BlindTreasure Warehouse",
                FromPhone = seller.CompanyPhone ?? "0123456789",
                FromAddress = seller.CompanyAddress ?? "123 Đường ABC, Quận 10, TP.HCM",
                FromWardName = seller.CompanyWardName ?? "",
                FromDistrictName = seller.CompanyDistrictName ?? "",
                FromProvinceName = seller.CompanyProvinceName ?? "",
                ToName = address.FullName,
                ToPhone = address.Phone,
                ToAddress = address.AddressLine,
                ToWardName = address.Ward ?? "",
                ToDistrictName = address.District ?? "",
                ToProvinceName = address.Province,
                CodAmount = 0,
                Content = $"Giao hàng cho {address.FullName} từ seller {seller.CompanyName}",
                Length = ghnOrderItems.Max(i => i.Length),
                Width = ghnOrderItems.Max(i => i.Width),
                Height = ghnOrderItems.Max(i => i.Height),
                Weight = ghnOrderItems.Sum(i => i.Weight * i.Quantity),
                InsuranceValue = ghnOrderItems.Sum(i => i.Price * i.Quantity),
                ServiceTypeId = 2,
                Items = ghnOrderItems.ToArray()
            };

            // Tạo đơn hàng GHN chính thức
            var ghnCreateResponse = await _ghnShippingService.CreateOrderAsync(ghnOrderRequest);

            // Tạo shipment cho group này
            var shipment = new Shipment
            {
                Provider = "GHN",
                OrderCode = ghnCreateResponse?.OrderCode,
                TotalFee = ghnCreateResponse?.TotalFee != null ? Convert.ToInt32(ghnCreateResponse.TotalFee.Value) : 0,
                MainServiceFee = (int)(ghnCreateResponse?.Fee?.MainService ?? 0),
                TrackingNumber = ghnCreateResponse?.OrderCode ?? "",
                //ShippedAt = DateTime.UtcNow,
                EstimatedDelivery = ghnCreateResponse?.ExpectedDeliveryTime.AddDays(1) != default
                    ? ghnCreateResponse.ExpectedDeliveryTime.AddDays(1)
                    : DateTime.UtcNow.AddDays(3),
                Status = ShipmentStatus.WAITING_PAYMENT
            };
            shipment = await _unitOfWork.Shipments.AddAsync(shipment);
            await _unitOfWork.SaveChangesAsync();

            // Gán shipmentId cho inventory item trong group
            foreach (var item in group)
            {
                item.ShipmentId = shipment.Id;
                item.Shipment = shipment;
                await _unitOfWork.InventoryItems.Update(item);
            }

            // Gán shipment vào các order-detail liên quan (many-to-many)
            var orderDetailIds = group
                .Where(i => i.OrderDetailId.HasValue)
                .Select(i => i.OrderDetailId.Value)
                .Distinct()
                .ToList();

            foreach (var orderDetailId in orderDetailIds)
            {
                var orderDetail = await _unitOfWork.OrderDetails
                    .GetByIdAsync(orderDetailId, od => od.Shipments);
                if (orderDetail != null)
                {
                    if (orderDetail.Shipments == null)
                        orderDetail.Shipments = new List<Shipment>();
                    if (!orderDetail.Shipments.Any(s => s.Id == shipment.Id))
                    {
                        orderDetail.Shipments.Add(shipment);
                        await _unitOfWork.OrderDetails.Update(orderDetail);
                    }
                }
            }

            shipments.Add(shipment);
            shipmentDtos.Add(ShipmentDtoMapper.ToShipmentDto(shipment));
            totalShippingFee += shipment.TotalFee ?? 0;
        }

        // Tạo duy nhất 1 link thanh toán cho toàn bộ phí ship
        var paymentUrl = await _stripeService.CreateShipmentCheckoutSessionAsync(shipments, userId, totalShippingFee);

        // Cập nhật trạng thái/log cho các order detail liên quan
        var allOrderDetailIds = items
            .Where(i => i.OrderDetailId.HasValue)
            .Select(i => i.OrderDetailId.Value)
            .Distinct()
            .ToList();

        foreach (var orderDetailId in allOrderDetailIds)
        {
            var orderDetail = await _unitOfWork.OrderDetails
                .GetByIdAsync(orderDetailId, od => od.InventoryItems);
            if (orderDetail != null)
            {
                orderDetail.Logs += $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Shipment requested by user {userId}.";
                OrderDtoMapper.UpdateOrderDetailStatusAndLogs(orderDetail);
                await _unitOfWork.OrderDetails.Update(orderDetail);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return new ShipmentItemResponseDTO
        {
            Shipments = shipmentDtos,
            PaymentUrl = paymentUrl
        };
    }

    // C# BlindTreasure.Application\Services\InventoryItemService.cs

    public async Task<List<ShipmentCheckoutResponseDTO>> PreviewShipmentForListItemsAsync(
        RequestItemShipmentDTO request)
    {
        var userId = _claimsService.CurrentUserId;
        if (request.InventoryItemIds == null || !request.InventoryItemIds.Any())
            throw ErrorHelper.BadRequest("Danh sách inventory item rỗng.");

        // Lấy toàn bộ inventory item của user
        var items = await _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => request.InventoryItemIds.Contains(i.Id) && i.UserId == userId && !i.IsDeleted)
            .Include(i => i.Product).ThenInclude(p => p.Seller)
            .Include(i => i.Product).ThenInclude(p => p.Category)
            .ToListAsync();

        if (!items.Any())
            throw ErrorHelper.BadRequest("Không tìm thấy inventory item hợp lệ.");

        // Group theo seller
        var sellerGroups = items
            .Where(i => i.Product != null && i.Product.Seller != null)
            .GroupBy(i => i.Product.SellerId);

        var result = new List<ShipmentCheckoutResponseDTO>();

        foreach (var group in sellerGroups)
        {
            var seller = group.First().Product.Seller;
            if (seller == null) continue;

            // Lấy địa chỉ giao hàng mặc định của user
            var address = await _unitOfWork.Addresses.GetQueryable()
                .Where(a => a.UserId == userId && a.IsDefault && !a.IsDeleted)
                .FirstOrDefaultAsync();
            if (address == null)
                throw ErrorHelper.BadRequest(
                    "Không tìm thấy địa chỉ giao hàng mặc định cho tài khoản của bạn. Vui lòng thiết lập một địa chỉ mặc định trong hồ sơ của bạn để tiếp tục.");

            // Gộp inventory item cùng ProductId
            var ghnOrderItems = group
                .GroupBy(i => i.ProductId)
                .Select(g =>
                {
                    var p = g.First().Product;
                    var length = Convert.ToInt32(p.Length > 0 ? p.Length : 10);
                    var width = Convert.ToInt32(p.Width > 0 ? p.Width : 10);
                    var height = Convert.ToInt32(p.Height > 0 ? p.Height : 10);
                    var weight = Convert.ToInt32(p.Weight > 0 ? p.Weight : 1000);

                    return new GhnOrderItemDto
                    {
                        Name = p.Name,
                        Code = p.Id.ToString(),
                        Price = Convert.ToInt32(p.RealSellingPrice),
                        Length = length,
                        Width = width,
                        Height = height,
                        Weight = weight,
                        Quantity = g.Count(), // tổng số inventory item cùng product
                        Category = new GhnItemCategory
                        {
                            Level1 = p.Category?.Name,
                            Level2 = p.Category?.Parent?.Name
                        }
                    };
                }).ToList();

            var ghnOrderRequest = new GhnOrderRequest
            {
                PaymentTypeId = 2,
                Note = $"Giao hàng cho seller {seller.CompanyName}",
                RequiredNote = "CHOXEMHANGKHONGTHU",
                FromName = seller.CompanyName ?? "BlindTreasure Warehouse",
                FromPhone = seller.CompanyPhone ?? "0123456789",
                FromAddress = seller.CompanyAddress ?? "123 Đường ABC, Quận 10, TP.HCM",
                FromWardName = seller.CompanyWardName ?? "",
                FromDistrictName = seller.CompanyDistrictName ?? "",
                FromProvinceName = seller.CompanyProvinceName ?? "",
                ToName = address.FullName,
                ToPhone = address.Phone,
                ToAddress = address.AddressLine,
                ToWardName = address.Ward ?? "",
                ToDistrictName = address.District ?? "",
                ToProvinceName = address.Province,
                CodAmount = 0,
                Content = $"Giao hàng cho {address.FullName} từ seller {seller.CompanyName}",
                Length = ghnOrderItems.Max(i => i.Length),
                Width = ghnOrderItems.Max(i => i.Width),
                Height = ghnOrderItems.Max(i => i.Height),
                Weight = ghnOrderItems.Sum(i => i.Weight * i.Quantity),
                InsuranceValue = ghnOrderItems.Sum(i => i.Price * i.Quantity),
                ServiceTypeId = 2,
                Items = ghnOrderItems.ToArray()
            };

            // Preview GHN
            var ghnPreviewResponse = await _ghnShippingService.PreviewOrderAsync(ghnOrderRequest);

            result.Add(new ShipmentCheckoutResponseDTO
            {
                Shipment = null,
                SellerCompanyName = seller.CompanyName,
                SellerId = seller.Id,
                GhnPreviewResponse = ghnPreviewResponse
            });
        }

        return result;
    }

    private static HoldInfoDto BuildHoldInfo(InventoryItem item)
    {
        var isOnHold = item.Status == InventoryItemStatus.OnHold;
        var holdUntil = item.HoldUntil;

        double? remainingDays = null;
        if (isOnHold && holdUntil.HasValue)
        {
            var rem = (holdUntil.Value - DateTime.UtcNow).TotalDays;
            remainingDays = rem > 0 ? Math.Round(rem, 4) : 0;
        }

        // Dùng LockedByRequestId làm "LastTradeId" hiển thị (đúng ngữ cảnh trading hiện tại)
        var lastTradeId = item.LockedByRequestId?.ToString();

        return new HoldInfoDto
        {
            IsOnHold = isOnHold,
            HoldUntil = holdUntil,
            RemainingDays = remainingDays,
            LastTradeId = lastTradeId
        };
    }

    private static string GetCacheKey(Guid id)
    {
        return $"inventoryitem:{id}";
    }
}

public class ShipmentItemResponseDTO
{
    public string? PaymentUrl { get; set; } // URL thanh toán phí ship
    public List<ShipmentDto>? Shipments { get; set; }
}