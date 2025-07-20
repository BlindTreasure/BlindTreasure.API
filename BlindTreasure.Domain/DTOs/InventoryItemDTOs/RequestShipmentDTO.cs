using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Domain.DTOs.InventoryItemDTOs;

public class RequestShipmentDTO
{
    [DefaultValue(1)] public int Quantity { get; set; } = 1; // số lượng hàng hoá cần giao
    public Guid? AddressId { get; set; } = null; // nếu inventory item đã có address thì không cần truyền vào

    [DefaultValue(false)]
    public bool IsPreReview { get; set; } =
        false; // nếu là true thì chạy review đơn giao hàng, nếu là false thì chạy request shipment
}