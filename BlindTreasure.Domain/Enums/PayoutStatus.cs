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
        [Description("Chờ xử lý")]
        PENDING,

        [Description("Đang xử lý")]
        PROCESSING,

        [Description("Hoàn thành")]
        COMPLETED,

        [Description("Thất bại")]
        FAILED,

        [Description("Đã hủy")]
        CANCELLED,

        [Description("Tạm giữ")]
        ON_HOLD
    }

    public enum PayoutPeriodType
    {
        [Description("Hàng tuần")]
        WEEKLY,

        [Description("Hàng tháng")]
        MONTHLY
    }
}
