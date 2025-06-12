using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class PromotionService : IPromotionService
{
    private readonly ILoggerService _loggerService;
    private readonly IClaimsService _claimsService;
    private readonly IUserService _userService;
    private readonly IMapperService _mapperService;
    private readonly IUnitOfWork _unitOfWork;

    public PromotionService(IUnitOfWork unitOfWork, ILoggerService loggerService, IMapperService mapperService,
        IClaimsService claimsService, IUserService userService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _mapperService = mapperService;
        _claimsService = claimsService;
        _userService = userService;
    }

    public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, useCache: true);

        if (user == null)
        {
            _loggerService.Warn($"[CreatePromotion] Không tìm thấy user với ID: {currentUserId}");
            throw ErrorHelper.Unauthorized("Không tìm thấy thông tin người dùng.");
        }

        _loggerService.Info(
            $"[CreatePromotion] Bắt đầu xử lý tạo promotion bởi userId: {currentUserId}, role: {user.RoleName}");

        await ValidateCreatePromotionAsync(user);
        _loggerService.Info($"[CreatePromotion] Passed validation cho userId: {currentUserId}");

        var promotion = await SetPromotionDataAsync(dto, user);
        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Success(
            $"[CreatePromotion] Tạo promotion thành công. Code: {promotion.Code}, UserId: {currentUserId}, Role: {user.RoleName}");

        return _mapperService.Map<Promotion, PromotionDto>(promotion);
    }

    private async Task<Promotion> SetPromotionDataAsync(CreatePromotionDto dto, User user)
    {
        var promotion = new Promotion
        {
            Code = dto.Code,
            Description = dto.Description,
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            UsageLimit = dto.UsageLimit
        };

        switch (user.RoleName)
        {
            case RoleType.Admin:
            case RoleType.Staff:
                promotion.Status = PromotionStatus.Approved;
                promotion.SellerId = null;
                break;

            case RoleType.Seller:
                var seller =
                    await _unitOfWork.Sellers.FirstOrDefaultAsync(s =>
                        s.UserId == user.Id && s.IsVerified && !s.IsDeleted);

                promotion.SellerId = seller?.Id;
                promotion.Status = PromotionStatus.Pending;
                break;

            default:
                throw ErrorHelper.Forbidden("Không có quyền tạo voucher.");
        }

        return promotion;
    }

    private async Task ValidateCreatePromotionAsync(User user)
    {
        if (user.RoleName == RoleType.Seller)
        {
            var seller =
                await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsVerified && !s.IsDeleted);

            if (seller == null)
            {
                _loggerService.Warn("ValidateCreatePromotion: Seller chưa xác minh hoặc bị xoá");
                throw ErrorHelper.Forbidden("Tài khoản không đủ điều kiện để tạo voucher.");
            }

            var count = await _unitOfWork.Promotions.CountAsync(p =>
                p.SellerId == seller.Id &&
                (p.Status == PromotionStatus.Pending || p.Status == PromotionStatus.Approved));

            if (count >= 3)
            {
                throw ErrorHelper.BadRequest("Bạn chỉ được tạo tối đa 3 voucher đang chờ duyệt hoặc đã duyệt.");
            }
        }
    }
}