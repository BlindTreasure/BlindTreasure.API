using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Services.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class TransactionService : ITransactionService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ICustomerBlindBoxService _customerBlindBoxService;
    private readonly IInventoryItemService _inventoryItemService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IOrderService _orderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGhnShippingService _ghnShippingService;

    public TransactionService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService logger,
        IMapperService mapper,
        IOrderService orderService,
        IUnitOfWork unitOfWork,
        IInventoryItemService inventoryItemService,
        ICustomerBlindBoxService customerBlindBoxService,
        IGhnShippingService ghnShippingService)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _orderService = orderService;
        _unitOfWork = unitOfWork;
        _inventoryItemService = inventoryItemService;
        _customerBlindBoxService = customerBlindBoxService;
        _ghnShippingService = ghnShippingService;
    }

    /// <summary>
    ///     Xử lý khi thanh toán Stripe thành công (webhook).
    /// </summary>
    public async Task HandleSuccessfulPaymentAsync(string sessionId, string orderId)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("SessionId is required.");
        if (string.IsNullOrWhiteSpace(orderId))
            throw ErrorHelper.BadRequest("OrderId is required.");

        try
        {
            // Tìm transaction theo sessionId
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order).ThenInclude(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .ThenInclude(p => p.Seller)
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order).ThenInclude(o => o.OrderDetails)
                .ThenInclude(od => od.BlindBox)
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order).ThenInclude(o => o.ShippingAddress)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            var order = transaction.Payment?.Order;
            if (order == null)
                throw ErrorHelper.NotFound("Không tìm thấy order cho transaction này.");

            // Idempotency: Nếu đã PAID thì bỏ qua
            if (order.Status == OrderStatus.PAID.ToString())
            {
                _logger.Warn($"[HandleSuccessfulPaymentAsync] Order {orderId} đã ở trạng thái PAID, bỏ qua xử lý.");
                return;
            }

            _logger.Info($"[HandleSuccessfulPaymentAsync] OrderDetails count = {order.OrderDetails?.Count ?? 0}");

            // Cập nhật trạng thái transaction, payment, order
            UpdatePaymentAndOrderStatus(transaction, order);

            var orderDetails = await GetOrderDetails(order.Id);

            if (!orderDetails.Any())
            {
                _logger.Warn($"[HandleSuccessfulPaymentAsync] Không tìm thấy order details cho order {orderId}.");
                return;
            }

            // 1. Tạo đơn GHN chính thức và cập nhật shipment
            await CreateGhnOrdersAndUpdateShipments(order, orderDetails);

            // 2. Tạo inventory cho sản phẩm vật lý
            await CreateInventoryForOrderDetailsAsync(order, orderDetails);

            // 3. Tạo customer inventory cho BlindBox
            await CreateCustomerBlindBoxForOrderDetails(order, orderDetails);

            // 4. Lưu thay đổi cuối cùng
            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.Payments.Update(transaction.Payment);
            await _unitOfWork.Orders.Update(order);
            await _unitOfWork.SaveChangesAsync();

            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã xác nhận thanh toán thành công cho order {orderId}.");
            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã cập nhật trạng thái PAID thành công cho {orderId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleSuccessfulPaymentAsync] {ex}");
            throw;
        }
    }

    private void UpdatePaymentAndOrderStatus(Transaction transaction, Order order)
    {
        transaction.Status = TransactionStatus.Successful.ToString();
        transaction.Payment.Status = PaymentStatus.Paid.ToString();
        transaction.Payment.PaidAt = DateTime.UtcNow;
        order.Status = OrderStatus.PAID.ToString();
        order.CompletedAt = DateTime.UtcNow;
    }

    private async Task<List<OrderDetail>> GetOrderDetails(Guid orderId)
    {
        return await _unitOfWork.OrderDetails.GetAllAsync(od => od.OrderId == orderId, x => x.Shipments)
               ?? new List<OrderDetail>();
    }

    private async Task CreateGhnOrdersAndUpdateShipments(Order order, List<OrderDetail> orderDetails)
    {
        var shipments = await _unitOfWork.Shipments.GetQueryable()
            .Where(s => orderDetails.Select(od => od.Id).Contains(s.OrderDetailId.Value) &&
                        s.Status == ShipmentStatus.WAITING_PAYMENT)
            .Include(s => s.OrderDetail).ThenInclude(od => od.Product).ThenInclude(p => p.Seller)
            .ToListAsync();

        var sellerGroups = shipments
            .Where(s => s.OrderDetail.Product != null)
            .GroupBy(s => s.OrderDetail.Product.SellerId);

        foreach (var group in sellerGroups)
        {
            var seller = group.First().OrderDetail.Product.Seller;
            var address = order.ShippingAddress;
            var orderDetailsInGroup = group.Select(s => s.OrderDetail).ToList();

            var ghnOrderRequest = BuildGhnOrderRequestFromOrderDetails(orderDetailsInGroup, seller, address);

            var ghnCreateResponse = await _ghnShippingService.CreateOrderAsync(ghnOrderRequest);

            foreach (var shipment in group)
            {
                UpdateShipmentWithGhnResponse(shipment, ghnCreateResponse);
                await _unitOfWork.Shipments.Update(shipment);
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private GhnOrderRequest BuildGhnOrderRequestFromOrderDetails(
        List<OrderDetail> orderDetails, Seller seller, Address address)
    {
        var items = orderDetails.Select(od => new GhnOrderItemDto
        {
            Name = od.Product.Name,
            Code = od.Product.Id.ToString(),
            Quantity = od.Quantity,
            Price = Convert.ToInt32(od.Product.Price),
            Length = Convert.ToInt32(od.Product.Length ?? 10),
            Width = Convert.ToInt32(od.Product.Width ?? 10),
            Height = Convert.ToInt32(od.Product.Height ?? 10),
            Weight = Convert.ToInt32(od.Product.Weight ?? 1000),
            Category = new GhnItemCategory
            {
                Level1 = od.Product.Category?.Name,
                Level2 = od.Product.Category?.Parent?.Name,
                Level3 = od.Product.Category?.Parent?.Parent?.Name
            }
        }).ToArray();

        return new GhnOrderRequest
        {
            PaymentTypeId = 2,
            Note = $"Giao hàng cho seller {seller.CompanyName}",
            RequiredNote = "CHOXEMHANGKHONGTHU",
            FromName = seller.CompanyName ?? "BlindTreasure Warehouse",
            FromPhone = seller.CompanyPhone ?? "0925136907",
            FromAddress = seller.CompanyAddress ?? "72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, TP.HCM",
            FromWardName = seller.CompanyWardName ?? "Phường 14",
            FromDistrictName = seller.CompanyDistrictName ?? "Quận 10",
            FromProvinceName = seller.CompanyProvinceName ?? "HCM",
            ToName = address.FullName,
            ToPhone = address.Phone,
            ToAddress = address.AddressLine,
            ToWardName = address.Ward ?? "",
            ToDistrictName = address.District ?? "",
            ToProvinceName = address.Province,
            CodAmount = 0,
            Content = $"Giao hàng cho {address.FullName} từ seller {seller.CompanyName}",
            Length = items.Max(i => i.Length),
            Width = items.Max(i => i.Width),
            Height = items.Max(i => i.Height),
            Weight = items.Sum(i => i.Weight),
            InsuranceValue = items.Sum(i => i.Price * i.Quantity),
            ServiceTypeId = 2,
            Items = items
        };
    }

    private void UpdateShipmentWithGhnResponse(Shipment shipment, GhnCreateResponse? ghnCreateResponse)
    {
        shipment.OrderCode = ghnCreateResponse?.OrderCode;
        shipment.TotalFee = ghnCreateResponse?.TotalFee != null ? Convert.ToInt32(ghnCreateResponse.TotalFee.Value) : 0;
        shipment.MainServiceFee = (int)(ghnCreateResponse?.Fee?.MainService ?? 0);
        shipment.TrackingNumber = ghnCreateResponse?.OrderCode ?? "";
        shipment.ShippedAt = DateTime.UtcNow;
        shipment.EstimatedDelivery = ghnCreateResponse?.ExpectedDeliveryTime ?? DateTime.UtcNow.AddDays(3);
        shipment.Status = ShipmentStatus.PROCESSING;
    }

    /// <summary>
    /// Tạo InventoryItem cho từng sản phẩm vật lý trong order sau khi thanh toán thành công.
    /// Mỗi OrderDetail có thể có nhiều shipment (nếu chia theo seller hoặc điều kiện khác).
    /// Mỗi InventoryItem đại diện cho một vật phẩm duy nhất, gắn với đúng OrderDetail và Shipment.
    /// </summary>
    private async Task CreateInventoryForOrderDetailsAsync(
     Order order,
     List<OrderDetail> orderDetails)
    {
        // 1) Lấy và validate shippingAddress (1 lần)
        Address? shippingAddress = null;
        if (order.ShippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses
                .GetByIdAsync(order.ShippingAddressId.Value);
            if (shippingAddress == null
                || shippingAddress.IsDeleted
                || shippingAddress.UserId != order.UserId)
            {
                _logger.Warn(ErrorMessages.OrderShippingAddressInvalidLog);
                throw ErrorHelper.BadRequest(
                    ErrorMessages.OrderShippingAddressInvalid);
            }
        }

        // Đảm bảo lấy đầy đủ các shipment cho từng order detail
        orderDetails = await _unitOfWork.OrderDetails
            .GetQueryable()
            .Where(od => od.OrderId == order.Id)
            .Include(od => od.Shipments)
            .Include(od => od.Seller)
            .ToListAsync();

        // 2) Build map: OrderDetailId → List<Shipment>
        var shipmentsByDetail = orderDetails
            .Where(od => od.Shipments != null && od.Shipments.Any())
            .ToDictionary(
                od => od.Id,
                od => od.Shipments!
                    .Select(s => new { s.Id, s.Status, SellerId = s.OrderDetail.SellerId })
                    .ToList()
            );

        // 3) Tạo từng InventoryItem
        var createdCount = 0;
        foreach (var od in orderDetails.Where(od => od.ProductId.HasValue))
        {
            shipmentsByDetail.TryGetValue(od.Id, out var shipmentInfos);
            _logger.Info($"[CreateInventory] OrderDetail {od.Id} có {shipmentInfos?.Count ?? 0} shipment.");

            for (var i = 0; i < od.Quantity; i++)
            {
                Guid? shipmentId = null;
                InventoryItemStatus status = InventoryItemStatus.Available;

                if (shipmentInfos != null && shipmentInfos.Count > 0)
                {
                    var selectedShipment = shipmentInfos.Count == 1
                        ? shipmentInfos[0]
                        : shipmentInfos.FirstOrDefault(si => si.SellerId == od.SellerId) ?? shipmentInfos[0];

                    shipmentId = selectedShipment.Id;
                    // Gắn trạng thái dựa trên shipment thực tế
                    status = selectedShipment.Status == ShipmentStatus.PROCESSING
                        ? InventoryItemStatus.Delivering
                        : InventoryItemStatus.Available;
                }

                var dto = new CreateInventoryItemDto
                {
                    ProductId = od.ProductId!.Value,
                    Quantity = 1,
                    Location = od.Seller.CompanyAddress,
                    Status = status,
                    ShipmentId = shipmentId,
                    OrderDetailId = od.Id,
                    AddressId = shippingAddress?.Id
                };

                await _inventoryItemService.CreateAsync(dto, order.UserId);
                _logger.Success(
                    $"[HandlePayment] Created inventory #{++createdCount} " +
                    $"for Product {od.ProductId} in Order {order.Id}.");
            }
        }
    }

    private async Task CreateCustomerBlindBoxForOrderDetails(Order order, List<OrderDetail> orderDetails)
    {
        var blindBoxCount = 0;
        foreach (var od in orderDetails.Where(od => od.BlindBoxId.HasValue))
        {
            _logger.Info(
                $"[HandleSuccessfulPaymentAsync] Tạo customer inventory cho BlindBox {od.BlindBoxId.Value} trong order {order.Id}.");
            for (var i = 0; i < od.Quantity; i++)
            {
                var createBlindBoxDto = new CreateCustomerInventoryDto
                {
                    BlindBoxId = od.BlindBoxId.Value,
                    OrderDetailId = od.Id,
                    IsOpened = false
                };
                await _customerBlindBoxService.CreateAsync(createBlindBoxDto, order.UserId);
                _logger.Success(
                    $"[HandleSuccessfulPaymentAsync] Đã tạo customer inventory thứ {++blindBoxCount} cho BlindBox {od.BlindBoxId.Value} trong order {order.Id}.");
            }
        }
    }

    /// <summary>
    ///     Xử lý khi thanh toán Stripe thất bại hoặc session hết hạn.
    /// </summary>
    public async Task HandleFailedPaymentAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("SessionId is required.");

        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Status = TransactionStatus.Failed.ToString();
            if (transaction.Payment != null)
                transaction.Payment.Status = PaymentStatus.Failed.ToString();
            if (transaction.Payment?.Order != null)
                transaction.Payment.Order.Status = OrderStatus.EXPIRED.ToString();

            await _unitOfWork.Transactions.Update(transaction);
            if (transaction.Payment != null)
                await _unitOfWork.Payments.Update(transaction.Payment);
            if (transaction.Payment?.Order != null)
                await _unitOfWork.Orders.Update(transaction.Payment.Order);

            await _unitOfWork.SaveChangesAsync();
            _logger.Warn($"[HandleFailedPaymentAsync] Đã xử lý thất bại thanh toán cho session {sessionId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleFailedPaymentAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    ///     Xác nhận khi PaymentIntent được tạo (Stripe webhook).
    /// </summary>
    public async Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("PaymentIntentId và SessionId là bắt buộc.");

        try
        {
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Payment.TransactionId = paymentIntentId;
            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.SaveChangesAsync();
            _logger.Info(
                $"[HandlePaymentIntentCreatedAsync] Đã cập nhật PaymentIntentId cho transaction {transaction.Id}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandlePaymentIntentCreatedAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    ///     Lấy danh sách transaction của user hiện tại.
    /// </summary>
    public async Task<List<Transaction>> GetMyTransactionsAsync()
    {
        var userId = _claimsService.CurrentUserId;
        return await _unitOfWork.Transactions.GetQueryable()
            .Where(t => t.Payment.Order.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Lấy danh sách transaction theo orderId.
    /// </summary>
    public async Task<List<Transaction>> GetTransactionsByOrderIdAsync(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw ErrorHelper.BadRequest("OrderId is required.");

        return await _unitOfWork.Transactions.GetQueryable()
            .Include(t => t.Payment)
            .Where(t => t.Payment.OrderId == orderId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    ///     Lấy chi tiết transaction theo Id.
    /// </summary>
    public async Task<Transaction?> GetTransactionByIdAsync(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
            throw ErrorHelper.BadRequest("TransactionId is required.");

        return await _unitOfWork.Transactions.GetByIdAsync(transactionId);
    }
}