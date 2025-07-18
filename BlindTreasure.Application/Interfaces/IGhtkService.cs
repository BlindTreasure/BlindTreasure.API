using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Interfaces;

public interface IGhtkService
{
    Task<GhtkAuthResponse> AuthenticateAsync();
    Task<GhtkFeeResponse> CalculateFeeAsync(GhtkFeeRequest req);
    Task<GhtkSubmitOrderResponse> SubmitOrderAsync(GhtkSubmitOrderRequest request);
    Task<GhtkTrackResponse> TrackOrderAsync(string trackingOrder);
}