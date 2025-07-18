using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class ShipmentCheckoutResponseDTO
    {
        public ShipmentDto? Shipment { get; set; }
        public Guid? ProductId { get; set; }
        public GhnCreateResponse? GhnResponse { get; set; }
        public GhnPreviewResponse? GhnPreviewResponse { get; set; }
    }
}
