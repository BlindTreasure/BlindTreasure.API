namespace BlindTreasure.Domain.Entities;

public class Seller : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; }

    public Guid? DepositId { get; set; }
    public Deposit Deposit { get; set; }

    public string ChannelType { get; set; }
    public string StoreName { get; set; }
    public bool IsVerified { get; set; }
    public Guid? VerificationRequestId { get; set; }
    public VerificationRequest VerificationRequest { get; set; }
    public string Status { get; set; }

    // quan hệ 1-n: Seller → Deposits (lịch sử)
    public ICollection<Deposit> Deposits { get; set; }

    // 1-n khác
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<Product> Products { get; set; }
    public ICollection<BlindBox> BlindBoxes { get; set; }
    public ICollection<InventoryItem> InventoryItems { get; set; }
}