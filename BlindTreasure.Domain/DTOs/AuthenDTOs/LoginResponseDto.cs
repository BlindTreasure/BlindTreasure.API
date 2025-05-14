namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class LoginResponseDto
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
}