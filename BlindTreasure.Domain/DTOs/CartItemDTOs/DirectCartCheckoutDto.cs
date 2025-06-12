using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.CartItemDTOs
{
    public class DirectCartCheckoutDto
    {
        public Guid? ShippingAddressId { get; set; }
        public List<DirectCartItemDto> Items { get; set; } = new();
    }

    public class DirectCartItemDto
    {
        public Guid? ProductId { get; set; }
        public string? ProductName { get; set; }
        public Guid? BlindBoxId { get; set; }
        public string? BlindBoxName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
