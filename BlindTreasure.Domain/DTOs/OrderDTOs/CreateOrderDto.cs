using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.OrderDTOs
{
    public class CreateOrderDto
    {
        public Guid? ShippingAddressId { get; set; }
        // Có thể bổ sung thêm payment info, note, v.v.
    }
}
