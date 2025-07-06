using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Seller : BaseEntity
{
    public Guid UserId { get; set; }

    public bool IsVerified { get; set; }
    public string CoaDocumentUrl { get; set; }
    public string? CompanyName { get; set; }
    public string? TaxId { get; set; }
    public string? CompanyAddress { get; set; }

    public string? RejectReason { get; set; }

    public SellerStatus Status { get; set; }
    public string? StripeAccountId { get; set; }


    public User? User { get; set; }
    public ICollection<Certificate>? Certificates { get; set; }
    public ICollection<Product>? Products { get; set; }
    public ICollection<BlindBox>? BlindBoxes { get; set; }
    
    public ICollection<PromotionParticipant>? PromotionParticipants { get; set; }

}