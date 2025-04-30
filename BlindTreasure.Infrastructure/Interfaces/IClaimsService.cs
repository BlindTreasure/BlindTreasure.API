using System.Security.Claims;
using VaccinaCare.Domain.DTOs.UserDTOs;

namespace VaccinaCare.Repository.Interfaces;

public interface IClaimsService
{
    public Guid GetCurrentUserId { get; }

    public string? IpAddress { get; }

    Task<CurrentUserDTO> GetCurrentUserDetailsAsync(ClaimsPrincipal user);
}