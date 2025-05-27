using BlindTreasure.Domain.DTOs.SellerDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface ISellerVerificationService
{
    Task<bool> VerifySellerAsync(Guid sellerId, SellerVerificationDto dto);
}