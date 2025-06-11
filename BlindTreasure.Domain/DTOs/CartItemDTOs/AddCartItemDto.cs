using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.CartItemDTOs
{
    public class AddCartItemDto
    {
        public Guid? ProductId { get; set; }
        public Guid? BlindBoxId { get; set; }
        public int Quantity { get; set; }
    }
}
