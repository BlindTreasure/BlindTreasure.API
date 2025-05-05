namespace BlindTreasure.Domain.Entities;

public class Certificate : BaseEntity
{
    public Guid SellerId { get; set; }
    public Seller Seller { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public string CertificateUrl { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? VerifiedBy { get; set; }
    public User VerifiedByUser { get; set; }
    public DateTime? VerifiedAt { get; set; }
}