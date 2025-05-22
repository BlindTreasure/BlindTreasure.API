using BlindTreasure.Domain.DTOs.UserDTOs;

namespace BlindTreasure.Infrastructure.Interfaces;

public interface IOAuthService
{
    Task<UserDto> AuthenticateWithGoogle(string token);
}