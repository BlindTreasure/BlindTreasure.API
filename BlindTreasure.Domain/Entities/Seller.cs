namespace BlindTreasure.Domain.Entities;

public class Seller : BaseEntity
{
    // FK → User
    public Guid UserId { get; set; }
    public User User { get; set; }

    // Official seller đã duyệt COA
    public bool IsVerified { get; set; }
    public string CoaDocumentUrl { get; set; }
    public string Status { get; set; }

    // 1-n → Certificates, Products, BlindBoxes
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<Product> Products { get; set; }
    public ICollection<BlindBox> BlindBoxes { get; set; }
}