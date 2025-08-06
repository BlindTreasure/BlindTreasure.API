using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class SellerVerificationService : ISellerVerificationService
{
    private readonly ICacheService _cacheService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerVerificationService(IUnitOfWork unitOfWork, IEmailService emailService, ICacheService cacheService,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _cacheService = cacheService;
        _notificationService = notificationService;
    }

    public async Task<bool> VerifySellerAsync(Guid sellerId, SellerVerificationDto dto)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Không tìm thấy thông tin người dùng của seller.");

        seller.IsVerified = dto.IsApproved;
        seller.Status = dto.IsApproved ? SellerStatus.Approved : SellerStatus.Rejected;
        seller.RejectReason = !dto.IsApproved ? dto.RejectReason : null;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.PushNotificationToUser(
            seller.UserId,
            new NotificationDto
            {
                Title = dto.IsApproved ? "Đã duyệt hồ sơ" : "Hồ sơ bị từ chối",
                Message = dto.IsApproved
                    ? "Hồ sơ seller của bạn đã được duyệt thành công. Bạn có thể bắt đầu kinh doanh."
                    : $"Hồ sơ seller của bạn đã bị từ chối. Lý do: {dto.RejectReason}",
                Type = NotificationType.System
            }
        );


        // XÓA CACHE
        await _cacheService.RemoveAsync($"seller:{seller.Id}");
        await _cacheService.RemoveAsync($"seller:user:{seller.UserId}");

        // Gửi email chúc mừng nếu được duyệt
        if (dto.IsApproved)
            await _emailService.SendSellerApprovalSuccessAsync(new EmailRequestDto
            {
                To = seller.User.Email,
                UserName = seller.User.FullName
            });
        else if (!string.IsNullOrWhiteSpace(dto.RejectReason))
            await _emailService.SendSellerRejectionAsync(new EmailRequestDto
            {
                To = seller.User.Email,
                UserName = seller.User.FullName
            }, dto.RejectReason);

        return true;
    }
}