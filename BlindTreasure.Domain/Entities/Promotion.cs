using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Promotion : BaseEntity
{
    public string Code { get; set; }
    public string Description { get; set; }

    public DiscountType DiscountType { get; set; } // Enum: [Percentage, Fixed]
    public decimal DiscountValue { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int? UsageLimit { get; set; }
    public string? RejectReason { get; set; } 

    public PromotionStatus Status { get; set; } // Enum: [PENDING, Approved, Rejected]

    public Guid? SellerId { get; set; } // null = Global

    public Seller? Seller { get; set; }
    public RoleType? CreatedByRole { get; set; }
    public ICollection<PromotionParticipant> PromotionParticipants { get; set; }


    //navigation property to Order
    public ICollection<Order> Orders { get; set; }
    public ICollection<OrderSellerPromotion> OrderSellerPromotions { get; set; }


}