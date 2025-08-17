using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities
{
    public class PromotionUserUsage : BaseEntity
    {
        public Guid? PromotionId { get; set; }
        public Promotion? Promotion { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }

        public int UsageCount { get; set; } = 0;
        public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
        // Computed property, không map vào DB
        public bool IsMaxUsageReached =>
            Promotion != null && UsageCount >= Promotion.MaxUsagePerUser;

    }
}
