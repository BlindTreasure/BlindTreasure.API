using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs
{
    public class RequestItemShipmentDTO
    {

        public List<Guid> InventoryItemIds { get; set; }

    }

   
}
