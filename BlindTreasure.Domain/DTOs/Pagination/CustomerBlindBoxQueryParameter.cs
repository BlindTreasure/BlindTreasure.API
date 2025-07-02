using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.Pagination
{
    public class CustomerBlindBoxQueryParameter : PaginationParameter
    {
        public bool? IsOpened { get; set; }
        public string? Search { get; set; } // Lọc theo tên BlindBox nếu muốn
        public Guid? BlindBoxId { get; set; }
    }
}
