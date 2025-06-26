using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class CreateInventoryItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Location { get; set; }
    public string? Status { get; set; }
}