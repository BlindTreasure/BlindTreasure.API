using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination;

public class PayoutTransactionQueryParameter : PaginationParameter
{
    public Guid? SellerId { get; set; }
    public DateTime? TransferredFrom { get; set; }
    public DateTime? TransferredTo { get; set; }

    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }

    public bool? IsInitiatedBySystem { get; set; }
}