using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
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
    private readonly IUnitOfWork _unitOfWork;

    public EmailService(IResend resend, IConfiguration configuration, IUnitOfWork unitOfWork)
    {
        _resend = resend;
        _fromEmail = configuration["RESEND_FROM"] ?? "noreply@fpt-devteam.fun";
        _unitOfWork = unitOfWork;
    }

    public async Task SendCommonItemOutOfStockAsync(string toEmail, string userName, string boxName, string productName)
    {
        var html = $@"
    <html style=""background-color:#ebeaea;margin:0;padding:0;"">
      <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
        <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;"">
          <div style=""background-color:#d02a2a;padding:16px 24px;"">
            <h1 style=""color:#ffffff;font-size:20px;margin:0;"">Cập nhật lại số lượng sản phẩm</h1>
          </div>
          <div style=""padding:24px;"">
            <p>Chào {userName},</p>
            <p>Trong BlindBox <strong>{boxName}</strong>, sản phẩm Common <strong>{productName}</strong> đã hết số lượng.</p>
            <p>BlindBox đã bị tạm dừng. Vui lòng cập nhật lại số lượng sản phẩm để tiếp tục kinh doanh.</p>
            <p>Nếu không cập nhật, BlindBox sẽ không thể được mở bởi khách hàng.</p>
            <p style=""margin-top:24px;"">Trân trọng,<br/>Đội ngũ BlindTreasure</p>
          </div>
        </div>
      </body>
    </html>";
        await SendEmailAsync(toEmail, $"BlindBox {boxName} bị tạm dừng do hết hàng Common", html);
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

    /// <summary>
    ///     Gửi email thông báo giao dịch thành công cho người mua hàng.
    ///     Nếu đơn hàng có shipment, thông báo trạng thái giao hàng.
    ///     Nếu không có shipment, thông báo sản phẩm đã được thêm vào kho hoặc BlindBox đã được mở.
    /// </summary>
    public async Task SendOrderPaymentSuccessToBuyerAsync(Order order)
    {
        if (order == null || order.User == null)
            throw new ArgumentNullException(nameof(order), "Order hoặc User không hợp lệ.");

        var toEmail = order.User.Email;
        var userName = order.User.FullName ?? order.User.Email;
        var orderId = order.Id.ToString();

        // Kiểm tra có shipment hay không
        var hasShipment = order.OrderDetails.Any(od => od.Shipments != null && od.Shipments.Any());

        string subject;
        string htmlContent;

        if (hasShipment)
        {
            // Trường hợp có shipment
            subject = $"Đơn hàng #{orderId} đã được xác nhận - BlindTreasure";
            var shipmentDetails = order.OrderDetails
                .SelectMany(od => od.Shipments ?? new List<Shipment>())
                .Select(s => $@"
                <div style=""background-color:#f8f9fa;padding:12px;border-left:3px solid #d02a2a;margin:8px 0;"">
                    <strong>{s.Provider}:</strong> Mã đơn {s.OrderCode ?? "N/A"}<br/>
                    <span style=""color:#666;"">Phí giao hàng: {s.TotalFee:N0}đ - Trạng thái: {s.Status}</span>
                </div>")
                .ToList();

            htmlContent = $@"
            <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                    <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;overflow:hidden;"">
                        <div style=""background-color:#d02a2a;padding:20px 24px;text-align:center;"">
                            <h1 style=""margin:0;color:#ffffff;font-size:24px;"">✅ Thanh toán thành công</h1>
                        </div>
                        <div style=""padding:24px;"">
                            <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                            <p style=""margin:0 0 20px 0;"">Cảm ơn bạn đã thanh toán thành công đơn hàng <strong>#{orderId}</strong>!</p>
                            
                            <div style=""background-color:#f0f9ff;padding:16px;border-radius:6px;margin:20px 0;"">
                                <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">🚚 Thông tin giao hàng</h3>
                                <p style=""margin:0 0 12px 0;"">Đơn hàng của bạn đang được xử lý và chuẩn bị giao hàng:</p>
                                {string.Join("", shipmentDetails)}
                            </div>
                            
                            <div style=""background-color:#fff3cd;padding:16px;border-radius:6px;border-left:4px solid #ffc107;"">
                                <p style=""margin:0;font-size:14px;"">💡 <strong>Lưu ý:</strong> Bạn có thể theo dõi trạng thái giao hàng trong mục ""Đơn hàng của tôi"" trên BlindTreasure.</p>
                            </div>
                            
                            <p style=""margin:24px 0 0 0;"">Nếu có thắc mắc, vui lòng liên hệ đội ngũ hỗ trợ của chúng tôi.</p>
                            <p style=""margin:16px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                        </div>
                    </div>
                </body>
            </html>";
        }
        else
        {
            // Trường hợp không có shipment
            subject = $"Đơn hàng #{orderId} đã hoàn tất - Sản phẩm đã vào kho";
            var inventoryItems = order.OrderDetails
                .SelectMany(od => od.InventoryItems ?? new List<InventoryItem>())
                .Select(ii => $@"
                <div style=""background-color:#f8f9fa;padding:12px;border-left:3px solid #28a745;margin:8px 0;"">
                    <strong>{ii.Product?.Name ?? "Sản phẩm"}</strong><br/>
                    <span style=""color:#666;"">Vị trí: {ii.Location}</span>
                </div>")
                .ToList();

            var blindBoxes = order.OrderDetails
                .SelectMany(od => od.CustomerBlindBoxes ?? new List<CustomerBlindBox>())
                .Select(cb => $@"
                <div style=""background-color:#f8f9fa;padding:12px;border-left:3px solid #6f42c1;margin:8px 0;"">
                    <strong>{cb.BlindBox?.Name ?? "BlindBox"}</strong><br/>
                    <span style=""color:{(cb.IsOpened ? "#28a745" : "#ffc107")};"">
                        {(cb.IsOpened ? "✅ Đã mở" : "📦 Chưa mở")}
                    </span>
                </div>")
                .ToList();

            htmlContent = $@"
            <html style=""background-color:#ebeaea;margin:0;padding:0;"">
                <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                    <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;overflow:hidden;"">
                        <div style=""background-color:#d02a2a;padding:20px 24px;text-align:center;"">
                            <h1 style=""margin:0;color:#ffffff;font-size:24px;"">🎉 Thanh toán thành công</h1>
                        </div>
                        <div style=""padding:24px;"">
                            <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                            <p style=""margin:0 0 20px 0;"">Cảm ơn bạn đã thanh toán thành công đơn hàng <strong>#{orderId}</strong>!</p>
                            
                            {(inventoryItems.Any() ? $@"
                            <div style=""background-color:#f0f9ff;padding:16px;border-radius:6px;margin:20px 0;"">
                                <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">📦 Sản phẩm đã vào kho</h3>
                                {string.Join("", inventoryItems)}
                            </div>" : "")}
                            
                            {(blindBoxes.Any() ? $@"
                            <div style=""background-color:#fdf2f8;padding:16px;border-radius:6px;margin:20px 0;"">
                                <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">🎁 BlindBox của bạn</h3>
                                {string.Join("", blindBoxes)}
                            </div>" : "")}
                            
                            <div style=""background-color:#d4edda;padding:16px;border-radius:6px;border-left:4px solid #28a745;"">
                                <p style=""margin:0;font-size:14px;"">💡 <strong>Lưu ý:</strong> Bạn có thể kiểm tra sản phẩm trong mục ""Kho hàng của tôi"" trên BlindTreasure.</p>
                            </div>
                            
                            <p style=""margin:24px 0 0 0;"">Nếu có thắc mắc, vui lòng liên hệ đội ngũ hỗ trợ của chúng tôi.</p>
                            <p style=""margin:16px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                        </div>
                    </div>
                </body>
            </html>";
        }

        await SendEmailAsync(toEmail, subject, htmlContent);
    }

    /// <summary>
    ///     Gửi email thông báo đơn hàng đã hết hạn hoặc bị hủy cho người mua hàng.
    /// </summary>
    public async Task SendOrderExpiredOrCancelledToBuyerAsync(Order order,
        string reason = "Đơn hàng đã hết hạn hoặc bị hủy do không thanh toán thành công.")
    {
        if (order == null || order.User == null)
            throw new ArgumentNullException(nameof(order), "Order hoặc User không hợp lệ.");

        var toEmail = order.User.Email;
        var userName = order.User.FullName ?? order.User.Email;
        var orderId = order.Id.ToString();

        var subject = $"Đơn hàng #{orderId} đã bị hủy - BlindTreasure";
        var htmlContent = $@"
        <html style=""background-color:#ebeaea;margin:0;padding:0;"">
            <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #d02a2a;border-radius:8px;overflow:hidden;"">
                    <div style=""background-color:#dc3545;padding:20px 24px;text-align:center;"">
                        <h1 style=""margin:0;color:#ffffff;font-size:24px;"">❌ Đơn hàng đã bị hủy</h1>
                    </div>
                    <div style=""padding:24px;"">
                        <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                        <p style=""margin:0 0 20px 0;"">Rất tiếc, đơn hàng <strong>#{orderId}</strong> của bạn đã bị hủy.</p>
                        
                        <div style=""background-color:#f8d7da;padding:16px;border-radius:6px;border-left:4px solid #dc3545;margin:20px 0;"">
                            <h3 style=""margin:0 0 8px 0;color:#721c24;font-size:16px;"">📋 Lý do hủy đơn hàng:</h3>
                            <p style=""margin:0;color:#721c24;"">{reason}</p>
                        </div>
                        
                        <div style=""background-color:#fff3cd;padding:16px;border-radius:6px;border-left:4px solid #ffc107;margin:20px 0;"">
                            <p style=""margin:0 0 8px 0;font-weight:bold;color:#856404;"">🛒 Bạn vẫn muốn mua sản phẩm này?</p>
                            <p style=""margin:0;color:#856404;"">Vui lòng truy cập lại BlindTreasure và đặt đơn hàng mới. Các sản phẩm có thể vẫn còn hàng!</p>
                        </div>
                        
                        <div style=""text-align:center;margin:24px 0;"">
                            <p style=""margin:0 0 12px 0;"">Nếu bạn có thắc mắc về việc hủy đơn hàng,</p>
                            <p style=""margin:0;"">vui lòng liên hệ đội ngũ hỗ trợ của chúng tôi.</p>
                        </div>
                        
                        <p style=""margin:24px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                    </div>
                </div>
            </body>
        </html>";

        await SendEmailAsync(toEmail, subject, htmlContent);
    }

    /// <summary>
    /// Gửi email thông báo đơn hàng đã hoàn thành cho khách hàng.
    /// Sử dụng khi order chuyển sang trạng thái COMPLETED.
    /// </summary>
    public async Task SendOrderCompletedToBuyerAsync(Order order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order), "Order không hợp lệ.");

        if (order.User == null)
        {
            order.User = await _unitOfWork.Users.GetByIdAsync(order.UserId);
            if (order.User == null)
                throw new ArgumentNullException(nameof(order.User), "User không hợp lệ.");
        }

        var toEmail = order.User.Email;
        var userName = order.User.FullName ?? order.User.Email;
        var orderId = order.Id.ToString();
        var completedAt = order.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ??
                          DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm");

        // Kiểm tra có shipment hay không
        var hasShipment = order.OrderDetails.Any(od => od.Shipments != null && od.Shipments.Any());

        // Kiểm tra điều kiện hoàn thành: giao hàng hay IN_INVENTORY
        var allDelivered = order.OrderDetails.All(od => od.Status == OrderDetailItemStatus.DELIVERED);
        var allInInventory3Days = order.OrderDetails.All(od =>
            od.Status == OrderDetailItemStatus.IN_INVENTORY &&
            od.UpdatedAt.HasValue &&
            (DateTime.UtcNow - od.UpdatedAt.Value).TotalDays >= 3);

        string subject;
        string htmlContent;

        if (hasShipment && allDelivered)
        {
            // Đơn hàng hoàn thành và giao hàng thành công
            subject = $"Đơn hàng #{orderId} đã hoàn thành & giao hàng thành công - BlindTreasure";
            var shipmentDetails = order.OrderDetails
                .SelectMany(od => od.Shipments ?? new List<Shipment>())
                .Select(s => $@"
                <div style=""background-color:#f8f9fa;padding:12px;border-left:3px solid #28a745;margin:8px 0;"">
                    <strong>{s.Provider}:</strong> Mã đơn {s.OrderCode ?? "N/A"}<br/>
                    <span style=""color:#666;"">Phí giao hàng: {s.TotalFee:N0}đ - Trạng thái: {s.Status}</span>
                </div>")
                .ToList();

            htmlContent = $@"
        <html style=""background-color:#ebeaea;margin:0;padding:0;"">
            <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #28a745;border-radius:8px;overflow:hidden;"">
                    <div style=""background-color:#28a745;padding:20px 24px;text-align:center;"">
                        <h1 style=""margin:0;color:#ffffff;font-size:24px;"">🎉 Đơn hàng đã hoàn thành & giao hàng thành công</h1>
                    </div>
                    <div style=""padding:24px;"">
                        <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                        <p style=""margin:0 0 20px 0;"">Đơn hàng <strong>#{orderId}</strong> của bạn đã được xác nhận hoàn thành và giao hàng thành công vào lúc <strong>{completedAt}</strong>.</p>
                        <div style=""background-color:#f0f9ff;padding:16px;border-radius:6px;margin:20px 0;"">
                            <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">Thông tin giao hàng</h3>
                            {string.Join("", shipmentDetails)}
                        </div>
                        <div style=""background-color:#d4edda;padding:16px;border-radius:6px;border-left:4px solid #28a745;"">
                            <p style=""margin:0;font-size:14px;"">💡 Bạn có thể kiểm tra chi tiết đơn hàng và sản phẩm trong mục ""Đơn hàng của tôi"" trên BlindTreasure.</p>
                        </div>
                        <p style=""margin:24px 0 0 0;"">Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi.</p>
                        <p style=""margin:16px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                    </div>
                </div>
            </body>
        </html>";
        }
        else if (allInInventory3Days)
        {
            // Đơn hàng hoàn thành, sản phẩm đã nằm trong túi đồ
            subject = $"Đơn mua hàng #{orderId} đã hoàn tất - Sản phẩm đã vào túi đồ - BlindTreasure";
            var inventoryItems = order.OrderDetails
                .SelectMany(od => od.InventoryItems ?? new List<InventoryItem>())
                .Select(ii => $@"
                <div style=""background-color:#f8f9fa;padding:12px;border-left:3px solid #28a745;margin:8px 0;"">
                    <strong>{ii.Product?.Name ?? "Sản phẩm"}</strong><br/>
                    <span style=""color:#666;"">Vị trí: {ii.Location}</span>
                </div>")
                .ToList();

            htmlContent = $@"
        <html style=""background-color:#ebeaea;margin:0;padding:0;"">
            <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #28a745;border-radius:8px;overflow:hidden;"">
                    <div style=""background-color:#28a745;padding:20px 24px;text-align:center;"">
                        <h1 style=""margin:0;color:#ffffff;font-size:24px;"">🎉 Đơn mua hàng đã hoàn tất</h1>
                    </div>
                    <div style=""padding:24px;"">
                        <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                        <p style=""margin:0 0 20px 0;"">Đơn hàng <strong>#{orderId}</strong> của bạn đã hoàn tất vào lúc <strong>{completedAt}</strong>.</p>
                        <div style=""background-color:#f0f9ff;padding:16px;border-radius:6px;margin:20px 0;"">
                            <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">Sản phẩm đã vào túi đồ</h3>
                            {string.Join("", inventoryItems)}
                        </div>
                        <div style=""background-color:#d4edda;padding:16px;border-radius:6px;border-left:4px solid #28a745;"">
                            <p style=""margin:0;font-size:14px;"">💡 Bạn có thể kiểm tra sản phẩm trong mục ""Kho hàng của tôi"" trên BlindTreasure.</p>
                        </div>
                        <p style=""margin:24px 0 0 0;"">Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi.</p>
                        <p style=""margin:16px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                    </div>
                </div>
            </body>
        </html>";
        }
        else
        {
            // Trường hợp fallback: chỉ thông báo hoàn thành chung
            subject = $"Đơn hàng #{orderId} đã hoàn thành - BlindTreasure";
            htmlContent = $@"
        <html style=""background-color:#ebeaea;margin:0;padding:0;"">
            <body style=""font-family:Arial,sans-serif;color:#252424;padding:40px 0;background-color:#ebeaea;"">
                <div style=""max-width:600px;margin:auto;background:#ffffff;border:1px solid #28a745;border-radius:8px;overflow:hidden;"">
                    <div style=""background-color:#28a745;padding:20px 24px;text-align:center;"">
                        <h1 style=""margin:0;color:#ffffff;font-size:24px;"">🎉 Đơn hàng đã hoàn thành</h1>
                    </div>
                    <div style=""padding:24px;"">
                        <p style=""margin:0 0 16px 0;font-size:16px;"">Chào <strong>{userName}</strong>,</p>
                        <p style=""margin:0 0 20px 0;"">Đơn hàng <strong>#{orderId}</strong> của bạn đã được xác nhận hoàn thành vào lúc <strong>{completedAt}</strong>.</p>
                        <div style=""background-color:#f0f9ff;padding:16px;border-radius:6px;margin:20px 0;"">
                            <h3 style=""margin:0 0 12px 0;color:#d02a2a;font-size:18px;"">Thông tin đơn hàng</h3>
                            <ul style=""padding-left:18px;margin:0;"">
                                <li>Tổng tiền: <strong>{order.FinalAmount:N0}đ</strong></li>
                                <li>Trạng thái: <strong>Hoàn thành</strong></li>
                            </ul>
                        </div>
                        <div style=""background-color:#d4edda;padding:16px;border-radius:6px;border-left:4px solid #28a745;"">
                            <p style=""margin:0;font-size:14px;"">💡 Bạn có thể kiểm tra chi tiết đơn hàng và sản phẩm trong mục ""Đơn hàng của tôi"" trên BlindTreasure.</p>
                        </div>
                        <p style=""margin:24px 0 0 0;"">Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi.</p>
                        <p style=""margin:16px 0 0 0;"">Trân trọng,<br/><strong>Đội ngũ BlindTreasure</strong></p>
                    </div>
                </div>
            </body>
        </html>";
        }

        await SendEmailAsync(toEmail, subject, htmlContent);
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