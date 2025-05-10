using BlindTreasure.Domain.DTOs.AccountDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IAccountService
{
    Task<UserDto> RegisterUserAsync(UserRegistrationDto registrationDto);
}