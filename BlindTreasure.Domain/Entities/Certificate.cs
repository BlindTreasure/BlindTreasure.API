namespace BlindTreasure.Domain.Entities;

public class Certificate : BaseEntity
{
    // FK → Seller
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    // FK → Product
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public string CertificateUrl { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // FK → User (nhân viên duyệt)
    public Guid VerifiedBy { get; set; }
    public User VerifiedByUser { get; set; }

    public DateTime VerifiedAt { get; set; }
}