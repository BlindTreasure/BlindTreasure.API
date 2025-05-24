namespace BlindTreasure.Application.Interfaces;

public interface ISellerVerificationService
{
    Task<bool> VerifySellerAsync(Guid sellerId, bool isApproved);
}