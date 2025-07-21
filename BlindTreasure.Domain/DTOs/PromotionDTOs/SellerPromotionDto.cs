using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.PromotionDTOs
{
    public class SellerPromotionDto
    {
        public Guid SellerId { get; set; }
        public Guid? PromotionId { get; set; }
    }
}
