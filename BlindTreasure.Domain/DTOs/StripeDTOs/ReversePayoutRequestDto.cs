using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.StripeDTOs
{
    public class ReversePayoutRequestDto
    {
        public string TransferId { get; set; }
        public string? Reason { get; set; }

    }
}
