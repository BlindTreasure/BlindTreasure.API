using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs
{
    public class ParticipantPromotionDto
    {
        public Guid Id { get; set; }
        public Guid PromotionId { get; set; }
        public Guid SellerId { get; set; }
        public DateTime JoinedAt { get; set; }
    }
}
