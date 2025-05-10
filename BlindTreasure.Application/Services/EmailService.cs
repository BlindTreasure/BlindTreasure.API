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
    
    public async Task Execute()
    {
        var message = new EmailMessage();
        message.From = "trangiaphuc362003181@gmail.com";
        message.To.Add( "trangiaphuc362003181@gmail.com" );
        message.Subject = "hello world";
        message.HtmlBody = "<strong>it works!</strong>";

        await _resend.EmailSendAsync( message );
    }
}