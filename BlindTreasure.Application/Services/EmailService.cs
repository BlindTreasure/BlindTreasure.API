using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using Microsoft.Extensions.Configuration;
using Resend;

namespace BlindTreasure.Application.Services;

/// <summary>
///     primary color for email templates:
///     đen: #252424
///     đỏ: #d02a2a
///     nền: #ebeaea
/// </summary>
public class EmailService : IEmailService
{
    private readonly string _fromEmail;
    private readonly IResend _resend;

    public EmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _fromEmail = configuration["RESEND_FROM"] ?? "noreply@fpt-devteam.fun";
    }

    public async Task SendRegistrationSuccessEmailAsync(EmailRequestDto request)
    {
        var html = $@"
                  <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                    <body style=""font-family:Arial,sans-serif;color:#252424;padding:20px;background-color:#ebeaea;"">
                      <div style=""max-width:600px;margin:auto;background:#fff;border:1px solid #d02a2a;border-radius:6px;padding:20px;"">
                        <h1 style=""color:#d02a2a;font-size:22px;"">Chào mừng {request.UserName}!</h1>
                        <p>Bạn đã đăng ký thành công tài khoản tại BlindTreasure.</p>
                        <p>Chúc bạn có trải nghiệm tuyệt vời.</p>
                        <p style=""margin-top:30px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
                      </div>
                    </body>
                  </html>";
        await SendEmailAsync(request.To, "Đăng ký thành công tại BlindTreasure", html);
    }

    public async Task SendOtpVerificationEmailAsync(EmailRequestDto request)
    {
        var html = $@"
                  <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                    <body style=""font-family:Arial,sans-serif;color:#252424;padding:20px;background-color:#ebeaea;"">
                      <div style=""max-width:600px;margin:auto;background:#fff;border:1px solid #d02a2a;border-radius:6px;padding:20px;text-align:center;"">
                        <h1 style=""color:#d02a2a;font-size:22px;"">Xác thực OTP</h1>
                        <p>Mã của bạn:</p>
                        <p style=""font-size:28px;color:#d02a2a;font-weight:bold;"">{request.Otp}</p>
                        <p style=""font-size:14px;"">Mã hết hạn sau 10 phút. Không chia sẻ với người khác.</p>
                        <p style=""margin-top:30px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
                      </div>
                    </body>
                  </html>";
        await SendEmailAsync(request.To, "Xác thực OTP tại BlindTreasure", html);
    }

    public async Task SendForgotPasswordOtpEmailAsync(EmailRequestDto request)
    {
        var html = $@"
                    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                      <body style=""font-family:Arial,sans-serif;color:#252424;padding:20px;background-color:#ebeaea;"">
                        <div style=""max-width:600px;margin:auto;background:#fff;border:1px solid #d02a2a;border-radius:6px;padding:20px;text-align:center;"">
                          <h1 style=""color:#d02a2a;font-size:22px;"">Lấy lại mật khẩu</h1>
                          <p>Mã OTP của bạn:</p>
                          <p style=""font-size:28px;color:#d02a2a;font-weight:bold;"">{request.Otp}</p>
                          <p style=""font-size:14px;"">Mã hết hạn sau 10 phút. Không chia sẻ với người khác.</p>
                          <p style=""margin-top:30px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
                        </div>
                      </body>
                    </html>";
        await SendEmailAsync(request.To, "OTP lấy lại mật khẩu tại BlindTreasure", html);
    }

    public async Task SendPasswordChangeEmailAsync(EmailRequestDto request)
    {
        var html = @"
                  <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                    <body style=""font-family:Arial,sans-serif;color:#252424;padding:20px;background-color:#ebeaea;"">
                      <div style=""max-width:600px;margin:auto;background:#fff;border:1px solid #d02a2a;border-radius:6px;padding:20px;"">
                        <h1 style=""color:#d02a2a;font-size:22px;"">Thay đổi mật khẩu thành công</h1>
                        <p>Mật khẩu của bạn đã được cập nhật.</p>
                        <p>Nếu không phải bạn thực hiện, vui lòng liên hệ ngay.</p>
                        <p style=""margin-top:30px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
                      </div>
                    </body>
                  </html>";
        await SendEmailAsync(request.To, "Mật khẩu đã được thay đổi tại BlindTreasure", html);
    }

    public async Task SendSellerEmailVerificationSuccessAsync(EmailRequestDto request)
    {
        var html = $@"
        <html style=""background-color:#ebeaea;margin:0;padding:0;"">
          <body style=""font-family:Arial,sans-serif;color:#252424;padding:20px;background-color:#ebeaea;"">
            <div style=""max-width:600px;margin:auto;background:#fff;border:1px solid #d02a2a;border-radius:6px;padding:20px;"">
              <h1 style=""color:#d02a2a;font-size:22px;"">Xác minh email thành công</h1>
              <p>Chào {request.To},</p>
              <p>Bạn đã xác minh email thành công. Vui lòng hoàn tất việc gửi COA (Certificate of Authenticity) vào hệ thống để hoàn tất quá trình trở thành Seller chính thức.</p>
              <p>Nếu có thắc mắc, vui lòng liên hệ đội ngũ hỗ trợ của BlindTreasure.</p>
              <p style=""margin-top:30px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
            </div>
          </body>
        </html>";
        await SendEmailAsync(request.To, "Xác minh email thành công - Hoàn thành gửi COA", html);
    }

    public async Task SendSellerApprovalSuccessAsync(EmailRequestDto request)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;overflow:hidden;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""margin:0;color:#ffffff;font-size:20px;"">Chúc mừng bạn đã trở thành Đối Tác Seller chính thức</h1>
          </div>
          <div style=""padding:24px;"">
            <p style=""margin:0 0 12px 0;"">Chào {request.UserName},</p>
            <p style=""margin:0 0 12px 0;"">Chúng tôi rất vui thông báo bạn đã được duyệt trở thành Seller chính thức trên BlindTreasure.</p>
            <p style=""margin:0 0 12px 0;"">Bạn có thể bắt đầu đăng tải sản phẩm và sử dụng các tính năng Seller trên nền tảng.</p>
            <p style=""margin:24px 0 0 0;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";

        await SendEmailAsync(request.To, "Chúc mừng bạn đã trở thành Đối Tác Seller chính thức", html);
    }

    public async Task SendSellerRejectionAsync(EmailRequestDto request, string rejectReason)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;overflow:hidden;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""margin:0;color:#ffffff;font-size:20px;"">Thông báo từ chối duyệt Seller</h1>
          </div>
          <div style=""padding:24px;"">
            <p style=""margin:0 0 12px 0;"">Chào {request.UserName},</p>
            <p style=""margin:0 0 12px 0;"">Rất tiếc, đơn đăng ký Seller của bạn đã bị từ chối.</p>
            <p style=""margin:0 0 12px 0;""><strong>Lý do:</strong> {rejectReason}</p>
            <p style=""margin:0 0 12px 0;"">Bạn có thể chỉnh sửa và gửi lại hồ sơ hoặc liên hệ đội ngũ hỗ trợ để biết thêm chi tiết.</p>
            <p style=""margin:24px 0 0 0;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";

        await SendEmailAsync(request.To, "Thông báo từ chối duyệt Seller", html);
    }


    public async Task SendBlindBoxApprovedAsync(string toEmail, string userName, string boxName)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""color:#ffffff;font-size:20px;margin:0;"">Blind Box của bạn đã được duyệt</h1>
          </div>
          <div style=""padding:24px;"">
            <p>Chào {userName},</p>
            <p>Blind Box <strong>{boxName}</strong> của bạn đã được phê duyệt thành công và sẽ hiển thị trên nền tảng vào ngày phát hành.</p>
            <p>Chúc bạn bán hàng thành công.</p>
            <p style=""margin-top:24px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";
        await SendEmailAsync(toEmail, $"Blind Box {boxName} đã được phê duyệt", html);
    }

    public async Task SendBlindBoxRejectedAsync(string toEmail, string userName, string boxName, string reason)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""color:#ffffff;font-size:20px;margin:0;"">Blind Box bị từ chối</h1>
          </div>
          <div style=""padding:24px;"">
            <p>Chào {userName},</p>
            <p>Blind Box <strong>{boxName}</strong> của bạn đã bị từ chối.</p>
            <p><strong>Lý do:</strong> {reason}</p>
            <p>Vui lòng chỉnh sửa và gửi lại để được xét duyệt.</p>
            <p style=""margin-top:24px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";
        await SendEmailAsync(toEmail, $"Blind Box {boxName} bị từ chối", html);
    }

    public async Task SendPromotionApprovedAsync(string toEmail, string? userName, string promotionCode)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""color:#ffffff;font-size:20px;margin:0;"">Voucher của bạn đã được duyệt</h1>
          </div>
          <div style=""padding:24px;"">
            <p>Chào {userName},</p>
            <p>Voucher <strong>{promotionCode}</strong> của bạn đã được phê duyệt và sẵn sàng áp dụng trên nền tảng.</p>
            <p>Chúc bạn kinh doanh thành công.</p>
            <p style=""margin-top:24px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";
        await SendEmailAsync(toEmail, $"Voucher {promotionCode} đã được phê duyệt", html);
    }

    public async Task SendPromotionRejectedAsync(string toEmail, string? userName, string promotionCode, string reason)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""color:#ffffff;font-size:20px;margin:0;"">Voucher của bạn đã bị từ chối</h1>
          </div>
          <div style=""padding:24px;"">
            <p>Chào {userName},</p>
            <p>Rất tiếc, voucher <strong>{promotionCode}</strong> đã bị từ chối xét duyệt.</p>
            <p><strong>Lý do:</strong> {reason}</p>
            <p>Vui lòng điều chỉnh và gửi lại nếu cần.</p>
            <p style=""margin-top:24px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";
        await SendEmailAsync(toEmail, $"Voucher {promotionCode} bị từ chối", html);
    }


    private async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        var message = new EmailMessage
        {
            From = _fromEmail,
            Subject = subject,
            HtmlBody = htmlContent
        };

        message.To.Add(to);
        await _resend.EmailSendAsync(message);
    }
}