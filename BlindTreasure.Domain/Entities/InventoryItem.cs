using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class InventoryItem : BaseEntity
{
    // FK → User (chủ sở hữu sau unbox)
    public Guid UserId { get; set; }
    public User? User { get; set; }

    // FK → Product
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public string Location { get; set; } = "HCM"; // Vị trí kho, mặc định là "HCM"
    public InventoryItemStatus Status { get; set; } // enum
    public bool IsFromBlindBox { get; set; } = false;
    public Guid? SourceCustomerBlindBoxId { get; set; } // nếu cần truy vết chi tiết
    public CustomerBlindBox? SourceCustomerBlindBox { get; set; }
    public Guid? AddressId { get; set; } // FK → Address, optional
    public Address? Address { get; set; }

    // 1-n → Listings
    public ICollection<Listing>? Listings { get; set; }

    // Thêm trường LockedByRequestId
    public Guid? LockedByRequestId { get; set; } // Giao dịch đang khóa item này
    public TradeRequest? LockedByRequest { get; set; } // Liên kết với giao dịch khóa item
}