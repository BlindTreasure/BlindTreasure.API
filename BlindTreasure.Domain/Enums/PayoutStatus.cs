using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Enums;

public enum PayoutStatus
{
    [Description("Đã thêm vào hàng chờ")] PENDING, // Thêm trạng thái PENDING để phân biệt với PROCESSING

    [Description("SELLER GỬI YÊU CẦU RÚT ")]
    REQUESTED, //  
    [Description("Đang xử lý")] PROCESSING,

    [Description("Hoàn thành")] COMPLETED,

    [Description("Thất bại")] FAILED,

    [Description("Đã hủy")] CANCELLED
}

public enum PayoutPeriodType
{
    [Description("Hàng tuần")] WEEKLY,

    [Description("Hàng tháng")] MONTHLY
}