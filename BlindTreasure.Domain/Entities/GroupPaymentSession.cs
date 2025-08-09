using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities
{
    public class GroupPaymentSession : BaseEntity
    {
        public Guid? CheckoutGroupId { get; set; }
        public string StripeSessionId { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsCompleted { get; set; }
        public string PaymentUrl { get; set; }
        public PaymentType Type { get; set; } = PaymentType.Order;
    }

    public enum PaymentType
    {
        Shipment,
        Order
    }
}
