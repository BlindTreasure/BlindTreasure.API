using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.AuthenDTOs;

public class ResendOtpRequestDto
{
    public required string Email { get; set; }
    public OtpType Type { get; set; }
}