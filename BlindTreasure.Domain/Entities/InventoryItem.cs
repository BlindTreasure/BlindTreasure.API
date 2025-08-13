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
    public string Location { get; set; } = "HCM"; // Vị trí kho, mặc định là "HCM"
    public InventoryItemStatus Status { get; set; } // enum
    public bool IsFromBlindBox { get; set; }
    public Guid? SourceCustomerBlindBoxId { get; set; } // nếu cần truy vết chi tiết
    public CustomerBlindBox? SourceCustomerBlindBox { get; set; }
    public Guid? AddressId { get; set; } // FK → Address, optional
    public Address? Address { get; set; }
    public RarityName? Tier { get; set; }

    // FK → OrderDetail
    public Guid? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }

    // FK → Shipment
    public Guid? ShipmentId { get; set; }
    public Shipment? Shipment { get; set; }

    // 1-n → Listings
    public ICollection<Listing>? Listings { get; set; }

    // Thêm trường LockedByRequestId
    public Guid? LockedByRequestId { get; set; } // Giao dịch đang khóa item này
    public TradeRequest? LockedByRequest { get; set; } // Liên kết với giao dịch khóa item

    public DateTime? HoldUntil { get; set; } // Thời điểm khi item được giải phóng
    public Guid? LastTradeHistoryId { get; set; } // ID của giao dịch gần nhất
    public TradeHistory? LastTradeHistory { get; set; } // Navigation property

    public ICollection<OrderDetailInventoryItemLog>? OrderDetailInventoryItemLogs { get; set; } =
        new List<OrderDetailInventoryItemLog>();
}