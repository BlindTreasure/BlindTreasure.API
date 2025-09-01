using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Mappers;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.AuthenDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PayoutDTOs;
using BlindTreasure.Domain.DTOs.ShipmentDTOs;
using BlindTreasure.Domain.DTOs.UserDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class AdminService : IAdminService
{
    private readonly IBlobService _blobService;
    private readonly ICacheService _cacheService;
    private readonly ILoggerService _logger;
    private readonly IPayoutService _payoutService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;

    public AdminService(
        IUnitOfWork unitOfWork,
        ILoggerService logger,
        ICacheService cacheService,
        IBlobService blobService,
        IPayoutService payoutService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _cacheService = cacheService;
        _blobService = blobService;
        _payoutService = payoutService;
        _emailService = emailService;
    }


    public async Task<UserDto?> GetUserDetailsByIdAsync(Guid userId)
    {
        _logger.Info($"[GetUserByIdAsync] Admin requests detail for user {userId}");

        var user = await GetUserById(userId, true);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[GetUserByIdAsync] User {userId} not found or deleted.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        var seller = new Seller();
        if (user.RoleName == RoleType.Seller)
        {
            seller = await GetSellerByUserIdAsync(user.Id);
            user.Seller = seller;
        }

        return UserMapper.ToUserDto(user);
    }

    public async Task<UserDto?> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        _logger.Info($"[UpdateProfileAsync] Update profile for user {userId}");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UpdateProfileAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại hoặc đã bị xóa.");
        }

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            user.Phone = dto.PhoneNumber;
        if (dto.DateOfBirth.HasValue)
            user.DateOfBirth = dto.DateOfBirth.Value;
        if (dto.Gender.HasValue)
            user.Gender = dto.Gender.Value;

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UpdateProfileAsync] Profile updated for user {user.Email}");
        var result = UserMapper.ToUserDto(user);
        return result;
    }

    public async Task<UpdateAvatarResultDto?> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        _logger.Info($"[UploadAvatarAsync] Bắt đầu cập nhật avatar cho user {userId}");

        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
        {
            _logger.Warn($"[UploadAvatarAsync] Không tìm thấy user {userId} hoặc đã bị xóa.");
            throw ErrorHelper.NotFound("Người dùng không tồn tại hoặc đã bị xóa.");
        }

        if (file == null || file.Length == 0)
        {
            _logger.Warn("[UploadAvatarAsync] File avatar không hợp lệ.");
            throw ErrorHelper.BadRequest("File ảnh không hợp lệ hoặc rỗng.");
        }

        // Sinh tên file duy nhất để tránh trùng (VD: avatar_userId_timestamp.png)
        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"avatars/avatar_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}{fileExtension}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetPreviewUrlAsync(fileName);
        if (string.IsNullOrEmpty(fileUrl))
        {
            _logger.Error($"[UploadAvatarAsync] Không thể lấy URL cho file {fileName}");
            throw ErrorHelper.Internal("Không thể tạo URL cho ảnh đại diện.");
        }

        user.AvatarUrl = fileUrl;
        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();

        // Ghi cache theo email và id
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UploadAvatarAsync] Đã cập nhật avatar thành công cho user {user.Email}");

        return new UpdateAvatarResultDto { AvatarUrl = fileUrl };
    }


    //Admin methods
    public async Task<Pagination<UserDto>> GetAllUsersAsync(UserQueryParameter param)
    {
        _logger.Info($"[GetAllUsersAsync] Admin requests user list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Users.GetQueryable()
            .Where(u => !u.IsDeleted)
            .AsNoTracking();

        // Filter
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var keyword = param.Search.Trim().ToLower();
            query = query.Where(u =>
                (!string.IsNullOrEmpty(u.FullName) && u.FullName.ToLower().Contains(keyword)) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(keyword)));
        }

        if (param.Status.HasValue)
            query = query.Where(u => u.Status == param.Status.Value);
        if (param.RoleName.HasValue)
            query = query.Where(u => u.RoleName == param.RoleName.Value);

        // Sort: UpdatedAt/CreatedAt theo hướng param.Desc
        if (param.Desc)
            query = query.OrderByDescending(b => b.UpdatedAt ?? b.CreatedAt);
        else
            query = query.OrderBy(b => b.UpdatedAt ?? b.CreatedAt);

        var total = await query.CountAsync();
        if (total == 0)
            _logger.Info("Không tìm thấy người dùng nào.");

        List<User> users;
        if (param.PageIndex == 0)
            users = await query.ToListAsync();
        else
            users = await query
                .Skip((param.PageIndex - 1) * param.PageSize)
                .Take(param.PageSize)
                .ToListAsync();

        var userDtos = users.Select(UserMapper.ToUserDto).ToList();
        return new Pagination<UserDto>(userDtos, total, param.PageIndex, param.PageSize);
    }


    public async Task<UserDto?> CreateUserAsync(UserCreateDto dto)
    {
        _logger.Info($"[CreateUserAsync] Admin creates user {dto.Email}");

        if (await UserExistsAsync(dto.Email))
        {
            _logger.Warn($"[CreateUserAsync] Email {dto.Email} already exists.");
            throw ErrorHelper.Conflict($"Email {dto.Email} đã tồn tại trong hệ thống.");
        }

        var hashedPassword = new PasswordHasher().HashPassword(dto.Password);
        var user = new User
        {
            Email = dto.Email,
            Password = hashedPassword,
            FullName = dto.FullName,
            Phone = dto.PhoneNumber,
            DateOfBirth = dto.DateOfBirth,
            AvatarUrl = dto.AvatarUrl,
            Status = UserStatus.Active,
            RoleName = dto.RoleName,
            IsEmailVerified = true
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        //await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        //await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[CreateUserAsync] User {user.Email} created by admin.");
        return UserMapper.ToUserDto(user);
    }

    public async Task<UserDto?> UpdateUserStatusAsync(Guid userId, UserStatus newStatus, string? reason = null)
    {
        _logger.Info($"[UpdateUserStatusAsync] Admin updates status for user {userId} to {newStatus}");

        var user = await GetUserById(userId);
        if (user == null)
        {
            _logger.Warn($"[UpdateUserStatusAsync] User {userId} not found.");
            throw ErrorHelper.NotFound($"Người dùng với ID {userId} không tồn tại.");
        }

        user.Status = newStatus;
        user.Reason = reason; // Gán lý do

        // Nếu ban/deactive thì soft remove, nếu active lại thì mở lại
        if (newStatus == UserStatus.Suspended || newStatus == UserStatus.Locked)
            user.IsDeleted = true;
        else if (newStatus == UserStatus.Active)
            user.IsDeleted = false;

        await _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.SetAsync($"user:{user.Email}", user, TimeSpan.FromHours(1));
        await _cacheService.SetAsync($"user:{user.Id}", user, TimeSpan.FromHours(1));

        _logger.Success($"[UpdateUserStatusAsync] User {user.Email} status updated to {newStatus} by admin.");
        return UserMapper.ToUserDto(user);
    }


    /// <summary>
    ///     Gets a user by id, optionally using cache.
    /// </summary>
    public async Task<User?> GetUserByEmail(string email, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = $"user:{email}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null) return cachedUser;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
            if (user != null)
                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
            return user;
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted);
    }

    /// <summary>
    ///     Gets a user by id, optionally using cache.
    /// </summary>
    public async Task<User?> GetUserById(Guid id, bool useCache = false)
    {
        if (useCache)
        {
            var cacheKey = $"user:{id}";
            var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
            if (cachedUser != null) return cachedUser;

            var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, x => x.Seller);
            if (user != null)
                await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromHours(1));
            return user;
        }

        return await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> TryCompleteOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            if (order.OrderDetails == null || !order.OrderDetails.Any())
                return false;

            var allDelivered = order.OrderDetails.All(od => od.Status == OrderDetailItemStatus.DELIVERED);
            var allInInventory3Days = order.OrderDetails.All(od =>
                od.Status == OrderDetailItemStatus.IN_INVENTORY &&
                od.UpdatedAt.HasValue &&
                (DateTime.UtcNow - od.UpdatedAt.Value).TotalDays >= 3);

            if (allDelivered || allInInventory3Days)
            {
                order.Status = OrderStatus.COMPLETED.ToString();
                order.CompletedAt = DateTime.UtcNow;
                await _unitOfWork.Orders.Update(order);
                _logger.Info($"Order {order.Id} marked as COMPLETED.");

                await _payoutService.AddCompletedOrderToPayoutAsync(order, cancellationToken);
                await _unitOfWork.SaveChangesAsync();

             //   await _emailService.SendOrderCompletedToBuyerAsync(order);

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            throw ErrorHelper.BadRequest(ex.Message);
        }
    }

    public async Task<Pagination<PayoutTransactionDto>> GetPayoutTransactionsAsync(
        PayoutTransactionQueryParameter param)
    {
        var query = _unitOfWork.PayoutTransactions.GetQueryable()
            .Where(pt => !pt.IsDeleted)
            .Include(pt => pt.Payout)
            .AsNoTracking();

        if (param.SellerId.HasValue)
            query = query.Where(pt => pt.SellerId == param.SellerId.Value);

        if (param.TransferredFrom.HasValue)
            query = query.Where(pt => pt.TransferredAt >= param.TransferredFrom.Value);

        if (param.TransferredTo.HasValue)
            query = query.Where(pt => pt.TransferredAt <= param.TransferredTo.Value);

        if (param.MinAmount.HasValue)
            query = query.Where(pt => pt.Amount >= param.MinAmount.Value);

        if (param.MaxAmount.HasValue)
            query = query.Where(pt => pt.Amount <= param.MaxAmount.Value);

        if (param.IsInitiatedBySystem.HasValue)
            query = param.IsInitiatedBySystem.Value
                ? query.Where(pt => pt.InitiatedBy == Guid.Empty)
                : query.Where(pt => pt.InitiatedBy != Guid.Empty);

        query = param.Desc
            ? query.OrderByDescending(pt => pt.TransferredAt ?? pt.CreatedAt)
            : query.OrderBy(pt => pt.TransferredAt ?? pt.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var dtos = items.Select(PayoutDtoMapper.ToPayoutTransactionDto).ToList();
        return new Pagination<PayoutTransactionDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task<PayoutTransactionDto?> GetPayoutTransactionByIdAsync(Guid id)
    {
        var entity = await _unitOfWork.PayoutTransactions.GetQueryable()
            .Include(pt => pt.Payout)
            .AsNoTracking()
            .FirstOrDefaultAsync(pt => pt.Id == id && !pt.IsDeleted);

        if (entity == null)
            return null;

        return PayoutDtoMapper.ToPayoutTransactionDto(entity);
    }

    public async Task<Pagination<ShipmentDto>> GetAllShipmentsAsync(ShipmentQueryParameter param)
    {
        _logger.Info($"[GetAllShipmentsAsync] Admin requests shipment list. Page: {param.PageIndex}, Size: {param.PageSize}");

        var query = _unitOfWork.Shipments.GetQueryable()
            .Include(s => s.OrderDetails)
            .Include(s => s.InventoryItems)
            .Where(s => !s.IsDeleted)
            .AsNoTracking();

        // Filter theo OrderCode (search)
        if (!string.IsNullOrWhiteSpace(param.Search))
            query = query.Where(s => s.OrderCode != null && s.OrderCode.Contains(param.Search));

        // Filter theo Status
        if (param.Status.HasValue)
            query = query.Where(s => s.Status == param.Status.Value);

        // Filter theo tổng phí vận chuyển
        if (param.MinTotalFee.HasValue)
            query = query.Where(s => s.TotalFee.HasValue && s.TotalFee.Value >= param.MinTotalFee.Value);
        if (param.MaxTotalFee.HasValue)
            query = query.Where(s => s.TotalFee.HasValue && s.TotalFee.Value <= param.MaxTotalFee.Value);

        // Filter theo EstimatedPickupTime
        if (param.FromEstimatedPickupTime.HasValue)
            query = query.Where(s => s.EstimatedPickupTime.HasValue && s.EstimatedPickupTime.Value >= param.FromEstimatedPickupTime.Value);
        if (param.ToEstimatedPickupTime.HasValue)
            query = query.Where(s => s.EstimatedPickupTime.HasValue && s.EstimatedPickupTime.Value <= param.ToEstimatedPickupTime.Value);

        // Sắp xếp theo UpdatedAt/CreatedAt
        query = param.Desc
            ? query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            : query.OrderBy(s => s.UpdatedAt ?? s.CreatedAt);

        var totalCount = await query.CountAsync();
        var shipments = param.PageIndex == 0
            ? await query.ToListAsync()
            : await query.Skip((param.PageIndex - 1) * param.PageSize).Take(param.PageSize).ToListAsync();

        var dtos = shipments.Select(ShipmentDtoMapper.ToShipmentDto).ToList();

        _logger.Info("[GetAllShipmentsAsync] Loaded shipment list for admin.");
        return new Pagination<ShipmentDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }




    // ----------------- PRIVATE HELPER METHODS -----------------

    /// <summary>
    ///     Checks if a user exists in cache or DB.
    /// </summary>
    private async Task<bool> UserExistsAsync(string email)
    {
        var cacheKey = $"user:{email}";
        var cachedUser = await _cacheService.GetAsync<User>(cacheKey);
        if (cachedUser != null) return true;

        var existingUser = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Email == email);
        return existingUser != null;
    }

    private async Task<Seller?> GetSellerByUserIdAsync(Guid userId)
    {
        return await _unitOfWork.Sellers.FirstOrDefaultAsync(u => u.UserId == userId);
    }
}