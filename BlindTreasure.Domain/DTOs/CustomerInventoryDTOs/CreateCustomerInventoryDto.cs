namespace BlindTreasure.Domain.DTOs.CustomerInventoryDTOs;

public class CreateCustomerInventoryDto
{
    public Guid BlindBoxId { get; set; }
    public Guid? OrderDetailId { get; set; }

    public bool IsOpened { get; set; } = false;
}