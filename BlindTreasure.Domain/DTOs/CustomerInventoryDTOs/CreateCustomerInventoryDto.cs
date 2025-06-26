using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.CustomerInventoryDTOs
{
    public class CreateCustomerInventoryDto
    {
        public Guid BlindBoxId { get; set; }
        public Guid? OrderDetailId { get; set; }

        public bool IsOpened { get; set; } = false;
    }
}
