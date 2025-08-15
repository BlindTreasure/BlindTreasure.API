using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;
using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

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
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService; // Thêm dòng này
    private readonly IOrderDetailInventoryItemLogService _orderDetailInventoryItemLogService;

    public TransactionService(
        ICacheService cacheService,
        IClaimsService claimsService,
        ILoggerService logger,
        IMapperService mapper,
        IOrderService orderService,
        IUnitOfWork unitOfWork,
        IInventoryItemService inventoryItemService,
        ICustomerBlindBoxService customerBlindBoxService,
        IGhnShippingService ghnShippingService,
        IEmailService emailService,
        INotificationService notificationService,
        IOrderDetailInventoryItemLogService orderDetailInventoryItemLogService)
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
        _emailService = emailService;
        _notificationService = notificationService; // Gán vào field
        _orderDetailInventoryItemLogService = orderDetailInventoryItemLogService;
    }


    /// <summary>
    ///     Xử lý khi thanh toán Stripe shipment thành công (webhook).
    /// </summary>
    /// <summary>
    ///     Xử lý khi thanh toán Stripe shipment thành công (webhook).
    ///     Đảm bảo cập nhật đúng trạng thái InventoryItem, OrderDetail, ghi log đầy đủ.
    /// </summary>
    /// <summary>
    /// Xử lý khi thanh toán Stripe shipment thành công (webhook).
    /// Đảm bảo cập nhật đúng trạng thái InventoryItem, OrderDetail, ghi log đầy đủ.
    /// </summary>
    public async Task HandleSuccessfulShipmentPaymentAsync(IEnumerable<Guid> shipmentIds)
    {
        if (shipmentIds == null || !shipmentIds.Any())
            throw ErrorHelper.BadRequest("Danh sách shipmentId rỗng.");

        // 1. Lấy shipment và inventory item liên quan, include đầy đủ OrderDetail và InventoryItems
        var shipments = await _unitOfWork.Shipments.GetQueryable()
            .Where(s => shipmentIds.Contains(s.Id) && s.Status == ShipmentStatus.WAITING_PAYMENT)
            .Include(s => s.InventoryItems)
            .Include(s => s.OrderDetails)
            .ThenInclude(od => od.InventoryItems)
            .Include(s => s.OrderDetails)
            .ThenInclude(od => od.Order)
            .ThenInclude(o => o.Seller)
            .ToListAsync();

        // 2. Tập hợp các OrderDetail cần cập nhật (dùng HashSet để tránh trùng lặp)
        var orderDetailsToUpdate = new HashSet<OrderDetail>();

        // 3. Cập nhật trạng thái InventoryItem và collect OrderDetail
        foreach (var shipment in shipments)
        {
            var oldShipmentStatus = shipment.Status;
            shipment.Status = ShipmentStatus.PROCESSING;
            shipment.EstimatedPickupTime = DateTime.UtcNow.Date.AddDays(new Random().Next(1, 3))
                .AddHours(new Random().Next(8, 18))
                .AddMinutes(new Random().Next(60));

            // Lấy seller và address để truyền vào log tracking
            var firstOrderDetail = shipment.OrderDetails?.FirstOrDefault();
            Seller seller = null;
            Address customerAddress = null;
            if (firstOrderDetail != null)
            {
                seller = firstOrderDetail.Order?.Seller;
                var userId = firstOrderDetail.Order?.UserId;
                if (userId.HasValue)
                    customerAddress = await _unitOfWork.Addresses.GetQueryable()
                        .FirstOrDefaultAsync(a => a.UserId == userId.Value && a.IsDefault);
            }

            // Cập nhật InventoryItem status và collect OrderDetail
            foreach (var item in shipment.InventoryItems)
            {
                var oldItemStatus = item.Status;
                item.Status = InventoryItemStatus.Delivering;
                item.ShipmentId = shipment.Id;
                await _unitOfWork.InventoryItems.Update(item);

                // Đảm bảo OrderDetail đã include InventoryItems
                if (item.OrderDetail != null)
                    // Nếu chưa có trong set thì add vào
                    orderDetailsToUpdate.Add(item.OrderDetail);

                // Tạo tracking message cho log
                var trackingMessage = await _orderDetailInventoryItemLogService.GenerateTrackingMessageAsync(
                    shipment,
                    oldShipmentStatus,
                    shipment.Status,
                    seller,
                    customerAddress
                );

                // Tạo log cho mỗi InventoryItem
                await _orderDetailInventoryItemLogService.LogShipmentTrackingInventoryItemUpdateAsync(
                    item.OrderDetail,
                    oldItemStatus,
                    item,
                    trackingMessage
                );
            }

            await _unitOfWork.Shipments.Update(shipment);
        }

        // 4. Đảm bảo mỗi OrderDetail có đầy đủ InventoryItems trước khi cập nhật status
        foreach (var orderDetail in orderDetailsToUpdate)
        {
            // Nếu InventoryItems chưa đầy đủ (do lazy loading), reload lại từ DB
            if (orderDetail.InventoryItems == null || !orderDetail.InventoryItems.Any())
            {
                var odWithItems = await _unitOfWork.OrderDetails.GetQueryable()
                    .Include(od => od.InventoryItems)
                    .FirstOrDefaultAsync(od => od.Id == orderDetail.Id);

                if (odWithItems != null)
                    // Gán lại navigation property
                    orderDetail.InventoryItems = odWithItems.InventoryItems;
            }

            var oldStatus = orderDetail.Status;
            OrderDtoMapper.UpdateOrderDetailStatusAndLogs(orderDetail);

            await _orderDetailInventoryItemLogService.LogOrderDetailStatusChangeAsync(
                orderDetail,
                oldStatus,
                orderDetail.Status,
                $"Cập nhật trạng thái sau khi thanh toán shipment thành công"
            );
            await _unitOfWork.OrderDetails.Update(orderDetail);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Handles successful Stripe payment (webhook).
    /// </summary>
    public async Task HandleSuccessfulPaymentAsync(string sessionId, string orderId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("SessionId is required.");
        if (string.IsNullOrWhiteSpace(orderId))
            throw ErrorHelper.BadRequest("OrderId is required.");

        try
        {
            // Load transaction and all necessary navigation properties in one query
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.OrderDetails)
                            .ThenInclude(od => od.BlindBox)
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.OrderDetails)
                            .ThenInclude(od => od.Shipments)
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.ShippingAddress)
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.Seller)
                        .ThenInclude(s => s.User) // Ensure Seller.User is loaded
                .Include(t => t.Payment)
                    .ThenInclude(p => p.Order)
                        .ThenInclude(o => o.User) // Ensure Order.User is loaded
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            _logger.Info($"Found transaction with external ref:{transaction.Id}");

            var order = transaction.Payment.Order;
            if (order == null)
            {
                _logger.Info($"[HandleSuccessfulPaymentAsync] Không tìm thấy Order Include bởi payment từ {transaction.Id}.");
                order = await _unitOfWork.Orders.GetQueryable()
                    .Include(o => o.Payment).ThenInclude(o=>o.Transactions)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.BlindBox)
                    .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Shipments)
                    .Include(o => o.ShippingAddress)
                    .Include(o => o.Seller)
                        .ThenInclude(s => s.User) // Ensure Seller.User is loaded
                    .Include(o => o.User) // Ensure Order.User is loaded
                    .FirstOrDefaultAsync(o => o.Id.ToString() == orderId);
            }


            // Idempotency: skip if already paid
            if (order.Status == OrderStatus.PAID.ToString())
            {
                _logger.Warn($"[HandleSuccessfulPaymentAsync] Order {orderId} đã ở trạng thái PAID, bỏ qua xử lý.");
                return;
            }

            // Update transaction, payment, and order status
            UpdatePaymentAndOrderStatus(transaction, order);

            // 1. Create GHN shipments and update shipment info
            await CreateGhnOrdersAndUpdateShipments(order);

            // 2. Create inventory for physical products
            await CreateInventoryForOrderDetailsAsync(order);

            // 3. Update status and logs for order details with inventory
            foreach (var od in order.OrderDetails.Where(od => od.ProductId.HasValue))
            {
                if (od.InventoryItems == null || !od.InventoryItems.Any())
                {
                    _logger.Warn($"[HandleSuccessfulPaymentAsync] OrderDetail {od.Id} không có InventoryItems.");
                    continue;
                }
                OrderDtoMapper.UpdateOrderDetailStatusAndLogs(od);
            }
            await _unitOfWork.OrderDetails.UpdateRange(order.OrderDetails.ToList());

            // 4. Create customer blind boxes for blind box order details
            await CreateCustomerBlindBoxForOrderDetails(order);

            // 5. Save all changes in one batch
            await _unitOfWork.Transactions.Update(transaction);
            await _unitOfWork.Payments.Update(transaction.Payment);
            await _unitOfWork.Orders.Update(order);
            var session = await _unitOfWork.GroupPaymentSessions.GetQueryable()
                .FirstOrDefaultAsync(x => x.StripeSessionId == sessionId && !x.IsCompleted); 
            if (session != null) { 
                session.IsCompleted = true; session.UpdatedAt = DateTime.UtcNow; await _unitOfWork.GroupPaymentSessions.Update(session); 
                _logger.Info("Đánh dấu hoàn thành cho thanh toán nhóm"); }
            await _unitOfWork.SaveChangesAsync();

            await _emailService.SendOrderPaymentSuccessToBuyerAsync(order);

            // Notify user (buyer)
            if (order.User != null)
            {
                await _notificationService.PushNotificationToUser(order.UserId, new NotificationDto
                {
                    Title = $"Thanh toán thành công đơn hàng #{order.Id}",
                    Message = "Đơn hàng của bạn đã được xác nhận. Nếu có giao hàng, hệ thống sẽ tiến hành xử lý vận chuyển.",
                    Type = NotificationType.Order,
                    SourceUrl = null
                });
            }

            // Notify seller
            if (order.Seller?.User != null)
            {
                var buyerName = order.User?.FullName ?? order.User?.Email ?? "Khách hàng";
                var sellerMsg = $@"
                    Sản phẩm của bạn vừa được khách hàng <b>{buyerName}</b> thanh toán thành công.<br/>
                    Đơn hàng #{order.Id} - Tổng tiền: <b>{order.FinalAmount:N0}đ</b>.<br/>
                    Vui lòng kiểm tra trạng thái đơn hàng và chuẩn bị giao hàng nếu có.";
                await _notificationService.PushNotificationToUser(order.Seller.User.Id, new NotificationDto
                {
                    Title = $"Đơn hàng mới đã được thanh toán",
                    Message = sellerMsg,
                    Type = NotificationType.Order,
                    SourceUrl = null
                });
            }

            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã xác nhận thanh toán thành công cho order {orderId}.");
            _logger.Success($"[HandleSuccessfulPaymentAsync] Đã cập nhật trạng thái PAID thành công cho {orderId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleSuccessfulPaymentAsync] {ex}");
            throw;
        }
    }

    /// <summary>
    /// Updates transaction, payment, and order status for successful payment.
    /// </summary>
    private void UpdatePaymentAndOrderStatus(Transaction transaction, Order order)
    {
        transaction.Status = TransactionStatus.Successful.ToString();
        transaction.Payment.Status = PaymentStatus.Paid;
        transaction.Payment.PaidAt = DateTime.UtcNow;
        order.Status = OrderStatus.PAID.ToString();
        order.CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates GHN shipment orders and updates shipment info.
    /// </summary>
    private async Task CreateGhnOrdersAndUpdateShipments(Order order)
    {
        var shipmentIds = order.OrderDetails
            .SelectMany(od => od.Shipments ?? Enumerable.Empty<Shipment>())
            .Where(s => s.Status == ShipmentStatus.WAITING_PAYMENT)
            .Select(s => s.Id)
            .Distinct()
            .ToList();

        if (!shipmentIds.Any()) return;

        var shipments = await _unitOfWork.Shipments.GetQueryable()
            .Where(s => shipmentIds.Contains(s.Id))
            .Include(s => s.OrderDetails)
            .ToListAsync();

        foreach (var shipment in shipments)
        {
            var seller = order.Seller;
            var address = order.ShippingAddress;
            var orderDetailsInGroup = shipment.OrderDetails?.ToList() ?? new List<OrderDetail>();

            var ghnOrderRequest = BuildGhnOrderRequestFromOrderDetails(orderDetailsInGroup, seller, address);
            var ghnCreateResponse = await _ghnShippingService.CreateOrderAsync(ghnOrderRequest);

            UpdateShipmentWithGhnResponse(shipment, ghnCreateResponse);
        }
        await _unitOfWork.Shipments.UpdateRange(shipments);
    }

    /// <summary>
    /// Builds GHN order request from order details, seller, and address.
    /// </summary>
    private GhnOrderRequest BuildGhnOrderRequestFromOrderDetails(
        List<OrderDetail> orderDetails, Seller seller, Address address)
    {
        var items = orderDetails.Where(od => od.Product != null).Select(od => new GhnOrderItemDto
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

    /// <summary>
    /// Updates shipment entity with GHN response.
    /// </summary>
    private void UpdateShipmentWithGhnResponse(Shipment shipment, GhnCreateResponse? ghnCreateResponse)
    {
        shipment.OrderCode = ghnCreateResponse?.OrderCode;
        shipment.TotalFee = ghnCreateResponse?.TotalFee != null ? Convert.ToInt32(ghnCreateResponse.TotalFee.Value) : 0;
        shipment.MainServiceFee = (int)(ghnCreateResponse?.Fee?.MainService ?? 0);
        shipment.TrackingNumber = ghnCreateResponse?.OrderCode ?? "";
        //shipment.ShippedAt = DateTime.UtcNow.AddDays(4);
        shipment.EstimatedPickupTime = DateTime.UtcNow.Date.AddDays(new Random().Next(1, 3)).AddHours(new Random().Next(8, 18)).AddMinutes(new Random().Next(60));
        shipment.EstimatedDelivery = ghnCreateResponse?.ExpectedDeliveryTime.AddDays(3) ?? DateTime.UtcNow.AddDays(3);
        shipment.Status = ShipmentStatus.PROCESSING;
    }

    /// <summary>
    /// Creates inventory items for each physical product in the order after successful payment.
    /// </summary>
    private async Task CreateInventoryForOrderDetailsAsync(Order order)
    {
        Address? shippingAddress = null;
        if (order.ShippingAddressId.HasValue)
        {
            shippingAddress = await _unitOfWork.Addresses.GetByIdAsync(order.ShippingAddressId.Value);
            if (shippingAddress == null || shippingAddress.IsDeleted || shippingAddress.UserId != order.UserId)
            {
                _logger.Warn(ErrorMessages.OrderShippingAddressInvalidLog);
                throw ErrorHelper.BadRequest(ErrorMessages.OrderShippingAddressInvalid);
            }
        }

        var shipmentsByDetail = order.OrderDetails
            .Where(od => od.Shipments != null && od.Shipments.Any())
            .ToDictionary(od => od.Id, od => od.Shipments!.ToList());

        var inventoryItems = new List<InventoryItem>();
        foreach (var od in order.OrderDetails.Where(od => od.ProductId.HasValue))
        {
            shipmentsByDetail.TryGetValue(od.Id, out var shipmentList);

            for (var i = 0; i < od.Quantity; i++)
            {
                Guid? shipmentId = null;
                var status = InventoryItemStatus.Available;
                Shipment? selectedShipment = null;

                if (shipmentList != null && shipmentList.Count > 0)
                {
                    selectedShipment = shipmentList[0];
                    shipmentId = selectedShipment.Id;
                    status = selectedShipment.Status switch
                    {
                        ShipmentStatus.PROCESSING => InventoryItemStatus.Delivering,
                        ShipmentStatus.WAITING_PAYMENT => InventoryItemStatus.Available,
                        ShipmentStatus.CANCELLED => InventoryItemStatus.Available,
                        _ => InventoryItemStatus.Available
                    };
                }

                var dto = new InventoryItem
                {
                    ProductId = od.ProductId!.Value,
                    Location = order.Seller.CompanyAddress,
                    Status = status,
                    ShipmentId = shipmentId,
                    IsFromBlindBox = false,
                    OrderDetailId = od.Id,
                    AddressId = shippingAddress?.Id,
                    UserId = order.UserId
                };

                inventoryItems.Add(dto);
                if (od.InventoryItems == null)
                    od.InventoryItems = new List<InventoryItem>();
                od.InventoryItems.Add(dto);
                // Track new inventory item creation for logging
                dto = await _unitOfWork.InventoryItems.AddAsync(dto);

                // Log: InventoryItem vừa được tạo cho OrderDetail sử dụng TEntity
                var orderDetaillog = await _orderDetailInventoryItemLogService.LogInventoryItemOrCustomerBlindboxAddedAsync(
                    od, dto, null, $"Inventory item created for OrderDetail {od.Id} after payment."
                );

                od.OrderDetailInventoryItemLogs.Add(orderDetaillog);

                // Nếu có shipment, log trạng thái shipment cho inventory item
                if (selectedShipment != null)
                {
                    var oldItemStatus = InventoryItemStatus.Available;
                    var trackingMessage = await _orderDetailInventoryItemLogService.GenerateTrackingMessageAsync(
                        selectedShipment,
                        ShipmentStatus.WAITING_PAYMENT,
                        selectedShipment.Status,
                        order.Seller,
                        shippingAddress
                    );

                    _logger.Info($"Generate tracking message succesfully: {trackingMessage}");

                    var itemInventoryLog = await _orderDetailInventoryItemLogService.LogShipmentTrackingInventoryItemUpdateAsync(
                        od,
                        oldItemStatus,
                        dto,
                        trackingMessage
                    );
                    _logger.Info($"[CreateInventoryForOrderDetailsAsync] Đã log trạng thái shipment cho inventory item {dto.Id}.");

                    dto.OrderDetailInventoryItemLogs.Add(itemInventoryLog);

                }

            }
        }
        //if (inventoryItems.Any())
        //    await _unitOfWork.InventoryItems.AddRangeAsync(inventoryItems);
    }

    /// <summary>
    /// Creates customer blind boxes for blind box order details.
    /// </summary>
    private async Task CreateCustomerBlindBoxForOrderDetails(Order order)
    {
        var blindBoxes = new List<CustomerBlindBox>();
        foreach (var detail in order.OrderDetails.Where(od => od.BlindBoxId.HasValue))
        {
            for (var i = 0; i < detail.Quantity; i++)
            {
                var cbBox = new CustomerBlindBox
                {
                    BlindBoxId = detail.BlindBoxId.Value,
                    OrderDetailId = detail.Id,
                    UserId = order.UserId,
                    IsOpened = false
                };
                blindBoxes.Add(cbBox);
                cbBox = await _unitOfWork.CustomerBlindBoxes.AddAsync(cbBox);
                // Log: CustomerBlindBox vừa được tạo cho OrderDetail sử dụng TEntity
                var log = await _orderDetailInventoryItemLogService.LogInventoryItemOrCustomerBlindboxAddedAsync(
                    detail, null, cbBox, $"CustomerBlindBox created for OrderDetail {detail.Id} after payment."
                );
            }
            var oldStatus = detail.Status;
            detail.Status = OrderDetailItemStatus.IN_INVENTORY;
            if (oldStatus != detail.Status)
            {
                await _orderDetailInventoryItemLogService.LogOrderDetailStatusChangeAsync(detail, oldStatus, detail.Status, "Chuyển sang IN_INVENTORY sau khi tạo Customer BlindBox.");
            }
        }
        //if (blindBoxes.Any())
        //    await _unitOfWork.CustomerBlindBoxes.AddRangeAsync(blindBoxes);
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
            // 1. Check for group payment session
            var groupSession = await _unitOfWork.GroupPaymentSessions
                .FirstOrDefaultAsync(s => s.StripeSessionId == sessionId && !s.IsCompleted);

            if (groupSession != null)
            {
                //groupSession.ExpiresAt = DateTime.UtcNow; expire này set để biết khi nào hết hạn.
                groupSession.IsCompleted = true;
                groupSession.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.GroupPaymentSessions.Update(groupSession);

                // Get all orders in the group
                var orders = await _unitOfWork.Orders.GetQueryable()
                    .Where(o => o.CheckoutGroupId == groupSession.CheckoutGroupId && !o.IsDeleted)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Shipments)
                    .Include(o => o.Payment)
                    .Include(o => o.Seller)
                    .ThenInclude(s => s.User)
                    .ToListAsync();

                foreach (var order in orders)
                {
                    if(order.Status == OrderStatus.CANCELLED.ToString() || order.Status == OrderStatus.EXPIRED.ToString() ||
                       order.Status == OrderStatus.PAID.ToString())
                    {
                        _logger.Warn($"[HandleFailedPaymentAsync] Order {order.Id} đã ở trạng thái không cần xử lý.");
                        continue; // Skip already cancelled, expired or paid orders
                    }

                    // Mark order as expired
                    order.Status = OrderStatus.EXPIRED.ToString();
                    order.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.Orders.Update(order);

                    // Mark payment as failed
                    if (order.Payment != null)
                    {
                        order.Payment.Status = PaymentStatus.Failed;
                        order.Payment.UpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.Payments.Update(order.Payment);

                        // Mark all transactions for this session as failed
                        var transactions = await _unitOfWork.Transactions.GetQueryable()
                            .Where(t => t.PaymentId == order.Payment.Id && t.ExternalRef == sessionId)
                            .ToListAsync();

                        foreach (var tx in transactions)
                        {
                            tx.Status = TransactionStatus.Failed.ToString();
                            tx.UpdatedAt = DateTime.UtcNow;
                            await _unitOfWork.Transactions.Update(tx);
                        }
                    }

                    // Mark all order details as cancelled
                    foreach (var detail in order.OrderDetails)
                    {
                        detail.Status = OrderDetailItemStatus.CANCELLED;
                        detail.UpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.OrderDetails.Update(detail);

                        // Mark all related shipments as cancelled
                        if (detail.Shipments != null)
                            foreach (var shipment in detail.Shipments)
                            {
                                shipment.Status = ShipmentStatus.CANCELLED;
                                shipment.UpdatedAt = DateTime.UtcNow;
                                await _unitOfWork.Shipments.Update(shipment);
                            }
                    }

                    // Gửi email thông báo hết hạn/hủy cho user
                    if (order.User != null)
                        await _emailService.SendOrderExpiredOrCancelledToBuyerAsync(order,
                            "Đơn hàng đã hết hạn do không thanh toán thành công.");

                    // Thông báo realtime cho user
                    if (order.User != null)
                        await _notificationService.PushNotificationToUser(order.UserId, new NotificationDto
                        {
                            Title = $"Link thanh toán cho đơn hàng #{order.Id} đã hết hạn",
                            Message =
                                "Đơn hàng của bạn đã bị hủy hoặc hết hạn do không hoàn tất thanh toán. Vui lòng đặt lại nếu muốn tiếp tục mua.",
                            Type = NotificationType.Order,
                            SourceUrl = null
                        });

                    // Thông báo cho seller
                    if (order.Seller?.User != null)
                    {
                        var buyerName = order.User?.FullName ?? order.User?.Email ?? "Khách hàng";
                        var sellerMsg = $@"
                            Đơn hàng #{order.Id} của khách <b>{buyerName}</b> đã hết hạn do không thanh toán thành công.<br/>
                            Bạn có thể kiểm tra lại trạng thái đơn hàng trong hệ thống.";
                        await _notificationService.PushNotificationToUser(order.Seller.UserId, new NotificationDto
                        {
                            Title = $"Đơn hàng #{order.Id} đã hết hạn",
                            Message = sellerMsg,
                            Type = NotificationType.Order,
                            SourceUrl = null
                        });
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                _logger.Warn($"[HandleFailedPaymentAsync] Đã xử lý thất bại thanh toán cho group session {sessionId}.");
                return;
            }



            // 2. Fallback: Single order session (old logic)
            var transaction = await _unitOfWork.Transactions.GetQueryable()
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.OrderDetails)
                .ThenInclude(od => od.Shipments)
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order)
                .ThenInclude(o => o.Seller)
                .Include(t => t.Payment)
                .ThenInclude(p => p.Order).ThenInclude(o => o.User)
                .FirstOrDefaultAsync(t => t.ExternalRef == sessionId);

            if (transaction.Payment?.Order != null)
            {
                var status = transaction.Payment.Order.Status;
                if (status == OrderStatus.CANCELLED.ToString() || status == OrderStatus.EXPIRED.ToString() ||
                    status == OrderStatus.PAID.ToString())
                {
                    _logger.Warn($"[HandleFailedPaymentAsync] Order {transaction.Payment.OrderId} đã ở trạng thái không cần xử lý.");
                    return; // Skip already cancelled, expired or paid orders
                }   

                transaction.Payment.Order.Status = OrderStatus.EXPIRED.ToString();
                foreach (var detail in transaction.Payment.Order.OrderDetails)
                {
                    detail.Status = OrderDetailItemStatus.CANCELLED;
                    detail.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.OrderDetails.Update(detail);

                    if (detail.Shipments != null)
                        foreach (var shipment in detail.Shipments)
                        {
                            shipment.Status = ShipmentStatus.CANCELLED;
                            shipment.UpdatedAt = DateTime.UtcNow;
                            await _unitOfWork.Shipments.Update(shipment);
                        }
                }
            }

            if (transaction == null)
                throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

            transaction.Status = TransactionStatus.Failed.ToString();
            if (transaction.Payment != null)
                transaction.Payment.Status = PaymentStatus.Failed;
      
            await _unitOfWork.Transactions.Update(transaction);
            if (transaction.Payment != null)
                await _unitOfWork.Payments.Update(transaction.Payment);
            if (transaction.Payment?.Order != null)
                await _unitOfWork.Orders.Update(transaction.Payment.Order);

            // Gửi email thông báo hết hạn/hủy cho user
            if (transaction.Payment?.Order?.User != null)
                await _emailService.SendOrderExpiredOrCancelledToBuyerAsync(transaction.Payment.Order,
                    "Đơn hàng đã hết hạn do không thanh toán thành công.");

            // Thông báo realtime cho user
            if (transaction.Payment?.Order?.User != null)
                await _notificationService.PushNotificationToUser(transaction.Payment.Order.UserId, new NotificationDto
                {
                    Title = $"Link thanh toán cho đơn hàng #{transaction.Payment.OrderId} đã hết hạn",
                    Message =
                        "Đơn hàng của bạn đã bị hủy hoặc hết hạn do không hoàn tất thanh toán. Vui lòng đặt lại nếu muốn tiếp tục mua.",
                    Type = NotificationType.Order,
                    SourceUrl = null
                });

            // Thông báo cho seller
            if (transaction.Payment?.Order?.Seller != null)
            {
                var buyerName = transaction.Payment.Order.User?.FullName ??
                                transaction.Payment.Order.User?.Email ?? "Khách hàng";
                var sellerMsg = $@"
                    Đơn hàng #{transaction.Payment.Order.Id} của khách <b>{buyerName}</b> đã hết hạn do không thanh toán thành công.<br/>
                    Bạn có thể kiểm tra lại trạng thái đơn hàng trong hệ thống.";
                await _notificationService.PushNotificationToUser(transaction.Payment.Order.Seller.UserId,
                    new NotificationDto
                    {
                        Title = $"Đơn hàng #{transaction.Payment.Order.Id} đã hết hạn",
                        Message = sellerMsg,
                        Type = NotificationType.Order,
                        SourceUrl = null
                    });
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.Warn($"[HandleFailedPaymentAsync] Đã xử lý thất bại thanh toán cho session {sessionId}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"[HandleFailedPaymentAsync] {ex}");
            throw ErrorHelper.BadRequest($"[HandleFailedPaymentAsync] {ex}");
        }
    }

    /// <summary>
    ///     Xác nhận khi PaymentIntent được tạo (Stripe webhook).
    /// </summary>
    public async Task HandlePaymentIntentCreatedAsync(string paymentIntentId, string sessionId, string? couponId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId) || string.IsNullOrWhiteSpace(sessionId))
            throw ErrorHelper.BadRequest("PaymentIntentId và SessionId là bắt buộc.");

        try
        {
            var transactions = await _unitOfWork.Transactions.GetQueryable()
                .Where(t => t.ExternalRef == sessionId).ToListAsync();

            var groupSession = await _unitOfWork.GroupPaymentSessions
                .FirstOrDefaultAsync(s => s.StripeSessionId == sessionId && !s.IsCompleted);

            if (groupSession != null)
            {
                groupSession.PaymentIntentId = paymentIntentId;
                groupSession.UpdatedAt = DateTime.UtcNow;
                groupSession.IsCompleted = true;
                groupSession.PaymentUrl = "";
                await _unitOfWork.GroupPaymentSessions.Update(groupSession);
                _logger.Info($"[HandlePaymentIntentCreatedAsync] Đã cập nhật PaymentIntentId cho group session {groupSession.Id}.");
            }
            else
                _logger.Warn($"[HandlePaymentIntentCreatedAsync] Không tìm thấy group session cho session {sessionId}.");

            if (transactions.Any())
                foreach (var transaction in transactions)
                {
                    if (transaction == null)
                        throw ErrorHelper.NotFound("Không tìm thấy transaction cho session Stripe này.");

                    transaction.Payment.PaymentIntentId = paymentIntentId;
                    if (couponId != null) transaction.Payment.CouponId = couponId; // Lưu couponId nếu có

                    await _unitOfWork.Transactions.Update(transaction);
                    _logger.Info(
                        $"[HandlePaymentIntentCreatedAsync] Đã cập nhật PaymentIntentId cho transaction {transaction.Id}.");
                }

            await _unitOfWork.SaveChangesAsync();
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