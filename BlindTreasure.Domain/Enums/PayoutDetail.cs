using BlindTreasure.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Enums
{
    public class PayoutDetail : BaseEntity
    {
        public Guid PayoutId { get; set; }
        public Payout Payout { get; set; }

        public Guid OrderDetailId { get; set; }
        public OrderDetail OrderDetail { get; set; }

        // Snapshot tại thời điểm tính payout (để audit)
        public decimal OriginalAmount { get; set; }       // TotalPrice
        public decimal DiscountAmount { get; set; }       // DetailDiscountPromotion
        public decimal FinalAmount { get; set; }          // FinalDetailPrice
        public decimal RefundAmount { get; set; } = 0;    // Nếu có refund
        public decimal ContributedAmount { get; set; }    // Số tiền đóng góp vào payout này

        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}
