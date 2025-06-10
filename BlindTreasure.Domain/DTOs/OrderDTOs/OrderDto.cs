using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.OrderDTOs
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime PlacedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public OrderAddressDto? ShippingAddress { get; set; }
        public List<OrderDetailDto> Details { get; set; }
    }
}
