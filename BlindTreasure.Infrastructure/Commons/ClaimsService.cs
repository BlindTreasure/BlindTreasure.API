using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VaccinaCare.Domain.DTOs.UserDTOs;
using VaccinaCare.Repository.Interfaces;
using VaccinaCare.Repository.Utils;

namespace VaccinaCare.Repository.Commons;

public class ClaimsService : IClaimsService
{
    private readonly IUnitOfWork _unitOfWork;

    public ClaimsService(IHttpContextAccessor httpContextAccessor)
    {
        // Lấy ClaimsIdentity
        var identity = httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;

        var extractedId = AuthenTools.GetCurrentUserId(identity);
        if (Guid.TryParse(extractedId, out var parsedId))
            GetCurrentUserId = parsedId;
        else
            GetCurrentUserId = Guid.Empty;

        IpAddress = httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    public Guid GetCurrentUserId { get; }

    public string? IpAddress { get; }

    public async Task<CurrentUserDTO> GetCurrentUserDetailsAsync(ClaimsPrincipal user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user), "User claims cannot be null.");

        // Trích xuất UserId từ claims
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("User is not properly authenticated.");

        // Kiểm tra định dạng của UserId (Guid)
        if (!Guid.TryParse(userId, out var userGuid))
            throw new UnauthorizedAccessException("Invalid user identifier format.");

        // Lấy thông tin người dùng từ cơ sở dữ liệu
        var userEntity = await _unitOfWork.UserRepository.GetByIdAsync(userGuid);
        if (userEntity == null)
            throw new UnauthorizedAccessException("User not found.");

        // Trả về thông tin người dùng dưới dạng DTO
        return new CurrentUserDTO
        {
            FullName = userEntity.FullName,
            Email = userEntity.Email,
            Gender = userEntity.Gender,
            DateOfBirth = userEntity.DateOfBirth,
            ImageUrl = userEntity.ImageUrl,
            PhoneNumber = userEntity.PhoneNumber,
            RoleName = userEntity.RoleName
        };
    }
}