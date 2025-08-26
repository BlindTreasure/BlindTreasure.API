using BlindTreasure.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class PayoutAdminQueryParameter : PaginationParameter
{
    public PayoutStatus? Status { get; set; }
    public Guid? SellerId { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
}