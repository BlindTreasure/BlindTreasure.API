using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities
{
    public class OrderSellerPromotion
    {
        public Guid OrderId { get; set; }
        public Order Order { get; set; }

        public Guid SellerId { get; set; }
        public Seller Seller { get; set; }

        public Guid PromotionId { get; set; }
        public Promotion Promotion { get; set; }

        public decimal DiscountAmount { get; set; }
        public string? Note { get; set; }
    }
}
