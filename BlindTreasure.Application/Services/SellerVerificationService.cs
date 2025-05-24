using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class SellerVerificationService : ISellerVerificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILoggerService _logger;

    public SellerVerificationService(IUnitOfWork unitOfWork, ILoggerService logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> VerifySellerAsync(Guid sellerId, bool isApproved)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        seller.IsVerified = isApproved;
        seller.Status = isApproved ? SellerStatus.Approved : SellerStatus.Rejected;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        _logger.Info($"[VerifySellerAsync] Seller {sellerId} " +
                     (isApproved ? "được duyệt." : "bị từ chối."));

        return true;
    }
}
