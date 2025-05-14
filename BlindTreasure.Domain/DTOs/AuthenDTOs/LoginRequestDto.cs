using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class LoginRequestDto
{
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public string? Email { get; set; }

    [DefaultValue("Ccubin2003@")] public string? Password { get; set; }
}