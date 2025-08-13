using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IEmailService
{
    /// <summary>
    ///     Gửi email thông báo đăng ký tài khoản thành công cho người dùng.
    /// </summary>
    Task SendRegistrationSuccessEmailAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email chứa mã OTP để xác thực email người dùng.
    /// </summary>
    Task SendOtpVerificationEmailAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email chứa mã OTP hỗ trợ lấy lại mật khẩu cho người dùng.
    /// </summary>
    Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email thông báo thay đổi mật khẩu thành công cho người dùng.
    /// </summary>
    Task SendPasswordChangeEmailAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email thông báo Seller đã xác minh email thành công,
    ///     nhắc nhở hoàn thành gửi COA để trở thành Seller chính thức.
    /// </summary>
    Task SendSellerEmailVerificationSuccessAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email chúc mừng Seller được staff hoặc admin duyệt trở thành Seller chính thức.
    /// </summary>
    Task SendSellerApprovalSuccessAsync(EmailRequestDto request);

    /// <summary>
    ///     Gửi email thông báo Seller bị từ chối duyệt,
    ///     kèm theo lý do từ chối do staff hoặc admin cung cấp.
    /// </summary>
    Task SendSellerRejectionAsync(EmailRequestDto request, string rejectReason);

    Task SendBlindBoxApprovedAsync(string toEmail, string userName, string boxName);
    Task SendBlindBoxRejectedAsync(string toEmail, string userName, string boxName, string reason);
    Task SendPromotionApprovedAsync(string toEmail, string? userName, string promotionCode);
    Task SendPromotionRejectedAsync(string toEmail, string? userName, string promotionCode, string reason);
    Task SendOrderPaymentSuccessToBuyerAsync(Order order);

    Task SendOrderExpiredOrCancelledToBuyerAsync(Order order,
        string reason = "Đơn hàng đã hết hạn hoặc bị hủy do không thanh toán thành công.");
}