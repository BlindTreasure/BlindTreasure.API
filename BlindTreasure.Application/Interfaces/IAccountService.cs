using BlindTreasure.Domain.DTOs.AccountDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IAccountService
{
    Task<bool> RegisterUserAsync(UserRegistrationDto registrationDto);
}