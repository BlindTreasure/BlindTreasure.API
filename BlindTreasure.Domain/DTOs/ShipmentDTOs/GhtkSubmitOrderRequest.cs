using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.ShipmentDTOs
{
    public class GhtkSubmitOrderRequest
    {
        public List<GhtkProduct> Products { get; set; }

        public GhtkOrderInfo Order { get; set; }
    }

    public class GhtkProduct
    {
        public string Name { get; set; }
        public double Weight { get; set; }
        public int Quantity { get; set; }
        public string Product_Code { get; set; }
    }

    public class GhtkOrderInfo //dựa trên docs api đăng đơn hàng của GHTK
    {
        public string Id { get; set; } = default!;

        public string Pick_Name { get; set; } = default!;

        public string Pick_Address { get; set; } = default!;

        public string Pick_Province { get; set; } = default!;

        public string Pick_District { get; set; } = default!;

        public string Pick_Ward { get; set; } = default!;

        public string Pick_Tel { get; set; } = default!;

        public string Tel { get; set; } = default!;

        public string Name { get; set; } = default!;

        public string Address { get; set; } = default!;

        public string Province { get; set; } = default!;

        public string District { get; set; } = default!;

        public string Ward { get; set; } = default!;

        public string Hamlet { get; set; } = default!;

        public int Is_Freeship { get; set; }

        public DateTimeOffset Pick_Date { get; set; }

        public long Pick_Money { get; set; }

        public string Note { get; set; } = default!;

        public long Value { get; set; }

        public string Transport { get; set; } = default!;

        public string Pick_Option { get; set; } = default!;

        [DefaultValue("empty")]
        public string Deliver_Option { get; set; } = "";
    }


}
