using BlindTreasure.Domain.DTOs.UserDTOs;

namespace BlindTreasure.Application.Interfaces;

public interface IUserService
{
    Task<CurrentUserDto> GetUserDetails(Guid id);
}