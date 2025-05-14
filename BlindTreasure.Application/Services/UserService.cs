using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class UserService : IUserService
{
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(IUnitOfWork unitOfWork, ILoggerService loggerService, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _logger = loggerService;
        _cacheService = cacheService;
    }

    public async Task<CurrentUserDto> GetUserDetails(Guid id)
    {
        _logger.Info($"[GetUserDetails] Start fetching user with ID: {id}");

        if (id == Guid.Empty)
            throw new Exception("400|ID người dùng không hợp lệ.");

        var cacheKey = $"user:{id}";
        var user = await _cacheService.GetAsync<User>(cacheKey);

        if (user == null)
        {
            user = await _unitOfWork.Users.GetByIdAsync(id);
            if (user == null)
            {
                _logger.Warn($"[GetUserDetails] Không tìm thấy user với ID: {id}");
                throw new Exception("404|Không tìm thấy người dùng.");
            }

            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
        }

        _logger.Info($"[GetUserDetails] Đã lấy thông tin user ID: {id}");

        return new CurrentUserDto
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.Phone,
            Gender = user.Gender,
            RoleName = user.RoleName,
            AvatarUrl = user.AvatarUrl,
            DateOfBirth = user.DateOfBirth
        };
    }
}