using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;

namespace BlindTreasure.Infrastructure.Interfaces;

public interface IOAuthService
{
    Task<LoginResponseDto> AuthenticateWithGoogle(string token);
}