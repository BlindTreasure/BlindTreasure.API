using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.SellerStatisticDTOs
{
    public enum StatisticsTimeRange
    {
        Day,        // Theo giờ trong ngày
        Week,       // Theo ngày trong tuần
        Month,      // Theo ngày trong tháng
        Quarter,    // Theo tháng trong quý
        Year,       // Theo tháng trong năm
        Custom      // Tùy chỉnh
    }
}
