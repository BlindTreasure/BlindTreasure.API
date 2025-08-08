using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Enums
{
    public enum PayoutStatus
    {

        [Description("Đang xử lý")]
        PROCESSING,

        [Description("Hoàn thành")]
        COMPLETED,

        [Description("Thất bại")]
        FAILED,

        [Description("Đã hủy")]
        CANCELLED,

    }

    public enum PayoutPeriodType
    {
        [Description("Hàng tuần")]
        WEEKLY,

        [Description("Hàng tháng")]
        MONTHLY
    }
}
