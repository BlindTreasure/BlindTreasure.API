using BlindTreasure.Application.Interfaces;
using Resend;

namespace BlindTreasure.Application.Services;

public class EmailService : IEmailService
{
    private readonly IResend _resend;

    public EmailService(IResend resend)
    {
        _resend = resend;
    }
    
    public async Task SendEmailAsync(string to, string subject, string htmlContent)
    {
        var message = new EmailMessage
        {
            From = "noreply@ae-tao-fullstack-api.site", // Thay thế bằng địa chỉ email của bạn
            Subject = subject,
            HtmlBody = htmlContent
        };
        message.To.Add(to);

        await _resend.EmailSendAsync(message);
    }
}