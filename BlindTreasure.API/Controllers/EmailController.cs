using System.ComponentModel.DataAnnotations;
using BlindTreasure.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Resend;

/// <summary />
[ApiController]
public class EmailController : ControllerBase
{
    private readonly IEmailService _emailService;

    public EmailController(IEmailService emailService)
    {
        _emailService = emailService;
    }
    
    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmail()
    {
        await _emailService.SendEmailAsync(
            "trangiaphuc362003181@gmail.com",
            "Chào mừng bạn đến với BlindTreasure",
            "<strong>Chúc mừng bạn đã đăng ký thành công!</strong>"
        );

        return Ok("Email đã được gửi.");
    }

   
}