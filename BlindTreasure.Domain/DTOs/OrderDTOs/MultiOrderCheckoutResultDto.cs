using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class MultiOrderCheckoutResultDto
{
    public List<OrderPaymentInfo> Orders { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string? GeneralPaymentUrl { get; set; } = string.Empty;
    public Guid? CheckoutGroupId { get; set; } = null;
    public string? CheckOutSessionId { get; set; } = null;
}

public class OrderPaymentInfo
{
    public Guid OrderId { get; set; }
    public Guid SellerId { get; set; }
    public string SellerName { get; set; }
    public string PaymentUrl { get; set; }
    public decimal FinalAmount { get; set; }
}