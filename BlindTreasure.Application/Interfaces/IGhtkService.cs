using BlindTreasure.Domain.DTOs.ShipmentDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IGhtkService
{
    Task<GhtkAuthResponse> AuthenticateAsync();
    Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest req);
    Task<GhtkSubmitOrderResponse> SubmitOrderAsync(GhtkSubmitOrderRequest request);
    Task<GhtkTrackResponse> TrackOrderAsync(string trackingOrder);
}