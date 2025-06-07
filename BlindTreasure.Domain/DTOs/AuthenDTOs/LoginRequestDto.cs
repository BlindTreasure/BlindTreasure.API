using System.ComponentModel;

namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class LoginRequestDto
{
    [DefaultValue("trangiaphuc362003181@gmail.com")]
    public string? Email { get; set; }

    [DefaultValue("1@")] public string? Password { get; set; }
}

public class GoogleLoginRequestDto
{
    /// <summary>
    ///     Google ID Token trả về từ phía client (Google Sign-In).
    /// </summary>
    public string? Token { get; set; }
}