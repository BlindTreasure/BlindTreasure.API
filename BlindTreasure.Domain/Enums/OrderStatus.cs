using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.Enums
{
    public enum OrderStatus
    {
        PENDING,
        CANCELLED
    }

    public enum OrderDetailStatus
    {
        PENDING,
        SHIPPED,
        DELIVERED,
        CANCELLED
    }
}
