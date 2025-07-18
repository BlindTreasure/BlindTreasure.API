namespace BlindTreasure.Domain.DTOs.OrderDTOs;

public class CreateCheckoutRequestDto
{

    public bool? IsShip{ get; set; } = false; // có muốn ship hàng hay không

    public bool? IsPreview { get; set; } = false; // có muốn xem trước thôn tin cước đơn hàng hay không

    public Guid? PromotionId { get; set; }
    // Có thể bổ sung thêm payment info, note, v.v.
}