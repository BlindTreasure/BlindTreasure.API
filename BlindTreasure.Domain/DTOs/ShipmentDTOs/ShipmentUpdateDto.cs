namespace BlindTreasure.Domain.DTOs.ShipmentDTOs;

public class ShipmentUpdateDto
{
    public string? OrderCode { get; set; }
    public int? TotalFee { get; set; }
    public int? MainServiceFee { get; set; }
    public string? Provider { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? Status { get; set; }
}