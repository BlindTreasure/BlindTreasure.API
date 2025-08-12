using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Entities;

public class PayoutLog : BaseEntity
{
    public Guid PayoutId { get; set; }
    public Payout Payout { get; set; }

    public PayoutStatus FromStatus { get; set; }
    public PayoutStatus ToStatus { get; set; }

    public string Action { get; set; } = string.Empty; // "CREATED", "PROCESSED", "COMPLETED", "FAILED", etc.
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }

    public Guid? TriggeredByUserId { get; set; } // Admin user hoặc system
    public User? TriggeredByUser { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
}