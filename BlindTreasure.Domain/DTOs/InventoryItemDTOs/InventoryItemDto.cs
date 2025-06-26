using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class InventoryItemDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public List<string> ProductImages { get; set; }
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int RestockThreshold { get; set; }
    public string Location { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}