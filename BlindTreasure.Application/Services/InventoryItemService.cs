using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
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
    private readonly IOrderService _orderService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;
    public readonly IGhnShippingService _ghnShippingService;


    public InventoryItemService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IOrderService orderService,
        ICategoryService categoryService,
        IGhnShippingService ghnShippingService) // added ghnShippingService
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _categoryService = categoryService; // initialize categoryService
        _ghnShippingService = ghnShippingService; // initialize ghnShippingService
    }

    public async Task<List<InventoryItemDto>> GetMyUnboxedItemsFromBlindBoxAsync(Guid blindBoxId)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.UserId == userId
                        && i.IsFromBlindBox
                        && !i.IsDeleted
                        && i.SourceCustomerBlindBox != null
                        && i.SourceCustomerBlindBox.BlindBoxId == blindBoxId)
            .Include(i => i.Product)
            .Include(i => i.SourceCustomerBlindBox)
            .AsNoTracking();

        var result = await query.ToListAsync();

        return result.Select(InventoryItemMapper.ToInventoryItemDto).ToList();
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
        var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Product not found.");

        var item = new InventoryItem
        {
            UserId = userId.Value,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Location = dto.Location ?? string.Empty,
            Status = dto.Status,
            AddressId = dto.AddressId,
        };

        var result = await _unitOfWork.InventoryItems.AddAsync(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache for this item (should not exist, but for safety)
        await _cacheService.RemoveAsync(GetCacheKey(item.Id));

        _loggerService.Success($"[CreateAsync] Inventory item created for user {userId}, product {product.Name}.");
        return InventoryItemMapper.ToInventoryItemDto(result) ??
               throw ErrorHelper.Internal("Failed to create inventory item.");
    }

    public async Task<InventoryItemDto?> GetByIdAsync(Guid id)
    {
        var cacheKey = GetCacheKey(id);
        var cached = await _cacheService.GetAsync<InventoryItem>(cacheKey);
        if (cached != null && !cached.IsDeleted)
        {
            _loggerService.Info($"[GetByIdAsync] Cache hit for inventory item {id}");
            return InventoryItemMapper.ToInventoryItemDto(cached);
        }

        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id, i => i.Product);
        if (item == null || item.IsDeleted)
            return null;

        await _cacheService.SetAsync(cacheKey, item, TimeSpan.FromMinutes(30));
        _loggerService.Info($"[GetByIdAsync] Inventory item {id} loaded from DB and cached.");
        return InventoryItemMapper.ToInventoryItemDto(item);
    }

    public async Task<Pagination<InventoryItemDto>> GetMyInventoryAsync(InventoryItemQueryParameter param)
    {
        var userId = _claimsService.CurrentUserId;

        var query = _unitOfWork.InventoryItems.GetQueryable()
            .Where(i => i.UserId == userId && !i.IsDeleted)
            .Include(i => i.Product)
            .ThenInclude(p => p.Category).AsNoTracking();

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
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var count = await query.CountAsync();

        List<InventoryItem> items;
        if (param.PageIndex == 0)
            items = await query.ToListAsync();
        else
            items = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var dtos = items.Select(InventoryItemMapper.ToInventoryItemDto).ToList();
        return new Pagination<InventoryItemDto>(dtos, count, param.PageIndex, param.PageSize);
    }

    public async Task<InventoryItemDto> UpdateAsync(Guid id, UpdateInventoryItemDto dto)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id, i => i.Product);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound("Inventory item not found.");

        if (dto.Quantity.HasValue)
            item.Quantity = dto.Quantity.Value;
        if (!string.IsNullOrWhiteSpace(dto.Location))
            item.Location = dto.Location;
        if (dto.Status.HasValue)
            item.Status = dto.Status.Value;

        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache
        await _cacheService.RemoveAsync(GetCacheKey(id));

        _loggerService.Success($"[UpdateAsync] Inventory item {id} updated.");
        return await GetByIdAsync(id) ?? throw ErrorHelper.Internal("Failed to update inventory item.");
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(id);
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound("Inventory item not found.");

        item.IsDeleted = true;
        item.DeletedAt = DateTime.UtcNow;
        item.DeletedBy = _claimsService.CurrentUserId;

        await _unitOfWork.InventoryItems.Update(item);
        await _unitOfWork.SaveChangesAsync();

        // Invalidate cache
        await _cacheService.RemoveAsync(GetCacheKey(id));

        _loggerService.Success($"[DeleteAsync] Inventory item {id} deleted.");
        return true;
    }


    /// <summary>
    /// Yêu cầu giao hàng cho một InventoryItem.
    /// Nếu chưa có địa chỉ thì phải truyền vào addressId.
    /// Tạo Shipment và cập nhật trạng thái OrderDetail liên quan.
    /// </summary>
    public async Task<ShipResponseDTO> RequestShipmentAsync(Guid inventoryItemId, RequestShipmentDTO request)
    {
        // 1. Lấy InventoryItem và các navigation cần thiết
        var item = await _unitOfWork.InventoryItems.GetByIdAsync(
            inventoryItemId,
            i => i.Address,
            i => i.Product,
            i => i.User
        );
        if (item == null || item.IsDeleted)
            throw ErrorHelper.NotFound("Không tìm thấy vật phẩm trong kho.");

        // 2. Kiểm tra hoặc cập nhật địa chỉ giao hàng
        Address address = item.Address!;
        if (item.AddressId == null)
        {
            if (!request.AddressId.HasValue)
                throw ErrorHelper.BadRequest("Please add shipping address for this item/order.");
            address = await _unitOfWork.Addresses.GetByIdAsync(request.AddressId.Value);
            if (address == null || address.IsDeleted || address.UserId != item.UserId)
                throw ErrorHelper.BadRequest("Invalid address.");
            item.AddressId = address.Id;
            await _unitOfWork.InventoryItems.Update(item);
        }

        // 3. Tìm OrderDetail liên quan đến InventoryItem
        var orderDetail = await _unitOfWork.OrderDetails.GetQueryable()
       .Include(od => od.Order)
       .Include(od => od.Product).ThenInclude(p => p.Seller)
       .Include(od => od.Product).ThenInclude(p => p.Category)
       .FirstOrDefaultAsync(od => od.ProductId == item.ProductId && od.Order.UserId == item.UserId);

        if (orderDetail == null)
            throw ErrorHelper.NotFound("Không tìm thấy OrderDetail liên quan.");

        var seller = orderDetail.Product?.Seller;
        if (seller == null || seller.IsDeleted)
            throw ErrorHelper.NotFound("Seller not found for this product.");

        var product = orderDetail.Product;
        if (product == null || product.IsDeleted)
            throw ErrorHelper.NotFound("Product not found for this order detail.");

        var category = product.Category;
        if (category == null)
            throw ErrorHelper.NotFound("Category not found for this product.");

        // 4. Chuẩn bị dữ liệu cho GHN Order Request
        int length = Convert.ToInt32(product.Length > 0 ? product.Length : 10);
        int width = Convert.ToInt32(product.Width > 0 ? product.Width : 10);
        int height = Convert.ToInt32(product.Height > 0 ? product.Height : 10);
        int weight = Convert.ToInt32(product.Weight > 0 ? product.Weight : 1000);

        var ghnOrderRequest = new GhnOrderRequest
        {
            PaymentTypeId = 2,
            Note = $"Giao hàng cho sản phẩm {product.Name}",
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
            Content = $"Sản phẩm: {product.Name} được ship cho {address.FullName} bởi seller {seller.CompanyName}",
            Length = length,
            Width = width,
            Height = height,
            Weight = weight,
            InsuranceValue = (int)orderDetail.UnitPrice * item.Quantity,
            ServiceTypeId = 2,
            Items = new[]
            {
            new GhnOrderItemDto
            {

        Name = product.Name,
        Code = product.Id.ToString(),
        Quantity = item.Quantity,
        Price = Convert.ToInt32(product.Price),
        Length = length,
        Width = width,
        Height = height,
        Weight = weight,
         Category = new GhnItemCategory
        {
            Level1 = product.Category.Name,
        }
            }
        }
        };

        // 5. Call GHN API: Preview hoặc Create Order
        if (request.IsPreReview)
        {
            var ghnPreviewResponse = await _ghnShippingService.PreviewOrderAsync(ghnOrderRequest);
            return new ShipResponseDTO
            {
                Shipment = null,
                GhnPreviewResponse = ghnPreviewResponse
            };
        }
        else
        {
            var ghnCreateResponse = await _ghnShippingService.CreateOrderAsync(ghnOrderRequest);

            // 6. Tạo Shipment mới
            var shipment = new Shipment
            {
                OrderDetailId = orderDetail.Id,
                Provider = "GHN",
                OrderCode = ghnCreateResponse?.OrderCode,
                TotalFee = ghnCreateResponse?.TotalFee != null ? Convert.ToInt32(ghnCreateResponse.TotalFee.Value) : 0,
                MainServiceFee = (int)(ghnCreateResponse?.Fee?.MainService ?? 0), 
                TrackingNumber = ghnCreateResponse?.OrderCode ?? "",
                ShippedAt = DateTime.UtcNow,
                EstimatedDelivery = ghnCreateResponse?.ExpectedDeliveryTime != default ? ghnCreateResponse.ExpectedDeliveryTime : DateTime.UtcNow.AddDays(3),
                Status = "Requested"
            };
            await _unitOfWork.Shipments.AddAsync(shipment);

            // 7. Cập nhật trạng thái OrderDetail
            orderDetail.Status = OrderDetailStatus.DELIVERING.ToString();
            await _unitOfWork.OrderDetails.Update(orderDetail);

            await _unitOfWork.SaveChangesAsync();
            _loggerService.Success($"[RequestShipmentAsync] Đã tạo yêu cầu giao hàng cho item {item.Id}.");

            return new ShipResponseDTO
            {
                Shipment = ShipmentDtoMapper.ToShipmentDto(shipment),
                GhnResponse = ghnCreateResponse
            };
        }
    }

    private static string GetCacheKey(Guid id)
    {
        return $"inventoryitem:{id}";
    }
}

public class ShipResponseDTO
{
    public ShipmentDto? Shipment { get; set; }
    public GhnCreateResponse? GhnResponse { get; set; }
    public GhnPreviewResponse? GhnPreviewResponse { get; set; }
}