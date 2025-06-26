namespace BlindTreasure.Domain.Entities;

public class CustomerInventory : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid BlindBoxId { get; set; }
    public BlindBox? BlindBox { get; set; }

    public bool IsOpened { get; set; } = false;
    public DateTime? OpenedAt { get; set; }

    public Guid? OrderDetailId { get; set; }
    public OrderDetail? OrderDetail { get; set; }
}
