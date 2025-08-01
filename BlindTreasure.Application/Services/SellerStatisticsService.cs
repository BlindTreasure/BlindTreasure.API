using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.SellerStatisticDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Services;

public class SellerStatisticsService : ISellerStatisticsService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapper;
    private readonly INotificationService _notificationService;
    private readonly IProductService _productService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerStatisticsService(
        IBlobService blobService,
        IEmailService emailService,
        ILoggerService loggerService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IMapperService mapper,
        IClaimsService claimsService,
        IProductService productService, INotificationService notificationService)
    {
        _blobService = blobService;
        _emailService = emailService;
        _loggerService = loggerService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _mapper = mapper;
        _claimsService = claimsService;
        _productService = productService;
        _notificationService = notificationService;
    }

    public async Task<SellerSalesStatisticsDto> GetSalesStatisticsAsync(Guid? sellerId = null, DateTime? from = null,
        DateTime? to = null)
    {
        // Lấy sellerId hiện tại nếu không truyền vào
        sellerId ??= (await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == _claimsService.CurrentUserId))?.Id
                     ?? throw ErrorHelper.Forbidden("Không tìm thấy seller.");

        // Lấy các OrderDetail đã bán thành công của seller
        var orderDetailsQuery = _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Where(od => od.SellerId == sellerId
                         && od.Order.Status == OrderStatus.PAID.ToString()
                         && !od.Order.IsDeleted);

        if (from.HasValue)
            orderDetailsQuery = orderDetailsQuery.Where(od => od.Order.PlacedAt >= from.Value);
        if (to.HasValue)
            orderDetailsQuery = orderDetailsQuery.Where(od => od.Order.PlacedAt <= to.Value);

        var orderDetails = await orderDetailsQuery.ToListAsync();

        var totalOrders = orderDetails.Select(od => od.OrderId).Distinct().Count();
        var totalProductsSold = orderDetails.Sum(od => od.Quantity);
        var grossSales = orderDetails.Sum(od => od.TotalPrice);

        // Tính tổng discount từ OrderSellerPromotion
        var orderIds = orderDetails.Select(od => od.OrderId).Distinct().ToList();
        var discounts = await _unitOfWork.OrderSellerPromotions.GetQueryable()
            .Where(osp => osp.SellerId == sellerId && orderIds.Contains(osp.OrderId))
            .SumAsync(osp => osp.DiscountAmount);

        // Tính tổng refund từ Transaction
        var paymentIds = await _unitOfWork.Orders.GetQueryable()
            .Where(o => orderIds.Contains(o.Id) && o.PaymentId != null)
            .Select(o => o.PaymentId.Value)
            .ToListAsync();

        var totalRefunded = await _unitOfWork.Transactions.GetQueryable()
            .Where(t => paymentIds.Contains(t.PaymentId) && t.Type == "Refund")
            .SumAsync(t => (decimal?)t.RefundAmount ?? 0);

        var netSales = grossSales - discounts - totalRefunded;

        return new SellerSalesStatisticsDto
        {
            SellerId = sellerId.Value,
            TotalOrders = totalOrders,
            TotalProductsSold = totalProductsSold,
            GrossSales = grossSales,
            NetSales = netSales,
            TotalRefunded = totalRefunded,
            TotalDiscount = discounts
        };
    }

    public async Task<SellerStatisticsDto> GetStatisticsAsync(
        Guid sellerId,
        SellerStatisticsRequestDto req,
        CancellationToken ct = default)
    {
        // 1. Lấy OrderDetails đã PAID và chưa CANCELLED
        var ods = _unitOfWork.OrderDetails.GetQueryable()
            .Include(od => od.Order)
            .Include(od => od.Order.OrderSellerPromotions)
            .Include(od => od.Shipments)
            .Where(od =>
                od.SellerId == sellerId &&
                od.Order.Status == OrderStatus.PAID.ToString() &&
                od.Status != OrderDetailItemStatus.CANCELLED &&
                od.Order.CompletedAt >= req.From &&
                od.Order.CompletedAt < req.To);

        // 2. Nếu filter theo product:
        if (req.ProductId.HasValue)
            ods = ods.Where(od => od.ProductId == req.ProductId);

        var detailList = await ods.ToListAsync(ct);

        // 3. Tính toán
        var orderIds = detailList.Select(d => d.OrderId).Distinct().ToList();
        var totalOrders = orderIds.Count;
        var totalItems = detailList.Sum(d => d.Quantity);
        var grossRevenue = detailList.Sum(d => d.TotalPrice);
        var totalDiscount = detailList
            .SelectMany(d => d.Order.OrderSellerPromotions)
            .Where(p => p.SellerId == sellerId)
            .Sum(p => p.DiscountAmount);
        var netRevenue = grossRevenue - totalDiscount;

        // 4. Refund rate (giả sử từ Payment.RefundedAmount)
        var payments = await _unitOfWork.Payments.GetQueryable()
            .Where(p => orderIds.Contains(p.OrderId))
            .ToListAsync(ct);
        var totalRefund = payments.Sum(p => p.RefundedAmount);
        var refundRate = grossRevenue > 0
            ? totalRefund / grossRevenue
            : 0m;

        // 5. Shipping fees
        var shippingFees = detailList.Sum(d => d.Shipments?.Sum(s => s.TotalFee) ?? 0);

        // 6. AOV
        var aov = totalOrders > 0
            ? Math.Round(netRevenue / totalOrders, 2)
            : 0m;

        // 7. Build DTO
        return new SellerStatisticsDto
        {
            TotalOrders = totalOrders,
            TotalItemsSold = totalItems,
            GrossRevenue = decimal.Round(grossRevenue, 2),
            TotalDiscount = decimal.Round(totalDiscount, 2),
            NetRevenue = decimal.Round(netRevenue, 2),
            AverageOrderValue = aov,
            RefundRate = decimal.Round(refundRate, 4),
            ShippingFees = shippingFees,
            Page = req.Page,
            PageSize = req.PageSize
        };
    }
}