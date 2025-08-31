using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ShipmentService : IShipmentService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IGhnShippingService _ghnShippingService;
    private readonly ILoggerService _loggerService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public ShipmentService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService loggerService,
        IProductService productService,
        IUnitOfWork unitOfWork,
        IGhnShippingService ghnShippingService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _loggerService = loggerService;
        _productService = productService;
        _unitOfWork = unitOfWork;
        _ghnShippingService = ghnShippingService;
    }

    // Lấy shipment theo Id (và kiểm tra quyền user)
    public async Task<ShipmentDto?> GetByIdAsync(Guid shipmentId)
    {
        var shipment = await _unitOfWork.Shipments.GetQueryable()
            .Include(s => s.InventoryItems)
            .Include(s => s.OrderDetails).ThenInclude(od => od.Order)
            .FirstOrDefaultAsync(s => s.Id == shipmentId && !s.IsDeleted);

        if (shipment == null)
            throw ErrorHelper.NotFound(
                "Rất tiếc, không tìm thấy thông tin vận chuyển bạn đang tìm kiếm hoặc đã bị xóa. Vui lòng kiểm tra lại ID.");

        //// Chỉ cho phép user là chủ đơn hàng xem shipment
        //var userId = _claimsService.CurrentUserId;
        //if (!shipment.OrderDetails.Any(od => od.Order.UserId == userId))
        //    throw ErrorHelper.Forbidden("Bạn không có quyền xem shipment này.");

        return ShipmentDtoMapper.ToShipmentDtoWithFullIncluded(shipment);
    }

    // Lấy tất cả shipment của user hiện tại (có thể filter theo orderId hoặc orderDetailId)
    public async Task<List<ShipmentDto>> GetMyShipmentsAsync(Guid? orderId = null, Guid? orderDetailId = null)
    {
        var userId = _claimsService.CurrentUserId;
        var query = _unitOfWork.Shipments.GetQueryable()
            .Include(s => s.OrderDetails).ThenInclude(od => od.Order)
            .Include(s => s.InventoryItems)
            .Where(s => !s.IsDeleted && s.OrderDetails.Any(od => od.Order.UserId == userId));

        if (orderId.HasValue)
            query = query.Where(s => s.OrderDetails.Any(od => od.OrderId == orderId.Value));

        if (orderDetailId.HasValue)
            query = query.Where(s => s.OrderDetails.Any(od => od.Id == orderDetailId.Value));

        var shipments = await query.ToListAsync();
        return shipments.Select(ShipmentDtoMapper.ToShipmentDtoWithFullIncluded).ToList();
    }

    // Lấy shipment theo orderDetailId (không cần kiểm tra user)
    public async Task<List<ShipmentDto>> GetByOrderDetailIdAsync(Guid orderDetailId)
    {
        var shipments = await _unitOfWork.Shipments.GetQueryable()
            .Include(s => s.OrderDetails)
            .Include(s => s.InventoryItems)
            .Where(s => !s.IsDeleted && s.OrderDetails.Any(od => od.Id == orderDetailId))
            .ToListAsync();

        return shipments.Select(ShipmentDtoMapper.ToShipmentDtoWithFullIncluded).ToList();
    }
}