using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class Seller : BaseEntity
{
    public Guid UserId { get; set; }

    public bool IsVerified { get; set; }
    public string CoaDocumentUrl { get; set; }
    public string? TaxId { get; set; }

    public string? RejectReason { get; set; }

    public SellerStatus Status { get; set; }
    public string? StripeAccountId { get; set; }

    public string? CompanyProductDescription { get; set; } = "Chuyên buôn bán vật phẩm gundam"; // FromName

    //địa chỉ lấy hàng của seller cho ghn 
    public string? ShopId { get; set; } = "197002"; // ShopId = 
    public string? CompanyName { get; set; } = "BlindTreasure-Collaboration"; // FromName
    public string? CompanyPhone { get; set; } = "0987654321"; // FromPhone

    public string? CompanyAddress { get; set; } =
        "72 Thành Thái, Phường 14, Quận 10, Hồ Chí Minh, Vietnam"; // FromAddress

    public string? CompanyWardName { get; set; } = "Phường 14"; // FromWardName
    public string? CompanyDistrictName { get; set; } = "Quận 10"; // FromDistrictName
    public string? CompanyProvinceName { get; set; } = "HCM"; // FromProvinceName

    /// 

    public User? User { get; set; }

    public ICollection<Certificate>? Certificates { get; set; }
    public ICollection<Product>? Products { get; set; }
    public ICollection<BlindBox>? BlindBoxes { get; set; }

    public ICollection<PromotionParticipant>? PromotionParticipants { get; set; }
    public ICollection<Review>? Reviews { get; set; } = new List<Review>();
}