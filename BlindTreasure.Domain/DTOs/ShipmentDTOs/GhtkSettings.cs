using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkSettings
    {
        public string BaseUrl { get; set; }
        public string ApiToken { get; set; }
        public string PartnerCode { get; set; }
    }
}
