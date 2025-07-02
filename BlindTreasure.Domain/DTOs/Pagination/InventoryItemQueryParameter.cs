using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination
{
    public class InventoryItemQueryParameter : PaginationParameter
    {
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Status { get; set; }
    }
}
