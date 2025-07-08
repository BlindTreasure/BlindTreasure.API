using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination
{
    public class SellerParticipantPromotionParameter : PaginationParameter
    {
        public Guid PromotionId {  get; set; }
    }
}
