using System.Text.RegularExpressions;
using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.PromotionDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class PromotionService : IPromotionService
{
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapperService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;

    public PromotionService(IUnitOfWork unitOfWork, ILoggerService loggerService, IMapperService mapperService,
        IClaimsService claimsService, IUserService userService, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _mapperService = mapperService;
        _claimsService = claimsService;
        _userService = userService;
        _emailService = emailService;
    }

    public async Task<Pagination<PromotionDto>> GetPromotionsAsync(PromotionQueryParameter param)
    {
        var query = _unitOfWork.Promotions.GetQueryable();

        if (param.SellerId.HasValue)
            query = query.Where(p => p.SellerId == param.SellerId.Value);

        if (param.Status.HasValue)
            query = query.Where(p => p.Status == param.Status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var dtos = items
            .Select(p => _mapperService.Map<Promotion, PromotionDto>(p))
            .ToList();

        return new Pagination<PromotionDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task<PromotionDto> CreatePromotionAsync(CreatePromotionDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, true);

        if (user == null)
        {
            _loggerService.Warn($"[CreatePromotion] Không tìm thấy user với ID: {currentUserId}");
            throw ErrorHelper.Unauthorized("Không tìm thấy thông tin người dùng.");
        }

        await ValidateCreatePromotionAsync(user);
        await ValidatePromotionInputAsync(dto);

        var promotion = await SetPromotionDataAsync(dto, user);
        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        return _mapperService.Map<Promotion, PromotionDto>(promotion);
    }

    public async Task<PromotionDto> ReviewPromotionAsync(ReviewPromotionDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, useCache: true);

        if (user == null || (user.RoleName != RoleType.Staff && user.RoleName != RoleType.Admin))
            throw ErrorHelper.Forbidden("Bạn không có quyền thực hiện hành động này.");

        var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == dto.PromotionId);
        if (promotion == null)
            throw ErrorHelper.NotFound("Không tìm thấy voucher.");

        if (promotion.Status != PromotionStatus.Pending)
            throw ErrorHelper.BadRequest("Chỉ có thể duyệt hoặc từ chối voucher đang chờ duyệt.");

        // Lấy thông tin seller để gửi email
        if (!promotion.SellerId.HasValue)
            throw ErrorHelper.BadRequest("Voucher toàn sàn không thể duyệt qua luồng này.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == promotion.SellerId.Value);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy seller của voucher.");

        var sellerUser = await _userService.GetUserById(seller.UserId, useCache: true);
        if (sellerUser == null)
            throw ErrorHelper.NotFound("Không tìm thấy tài khoản của seller.");

        // Xử lý duyệt hoặc từ chối
        if (dto.IsApproved)
        {
            promotion.Status = PromotionStatus.Approved;
            promotion.UpdatedAt = DateTime.UtcNow;

            _loggerService.Info($"[ReviewPromotion] Duyệt voucher {promotion.Code} bởi user {user.Id}");

            await _emailService.SendPromotionApprovedAsync(
                sellerUser.Email,
                sellerUser.FullName,
                promotion.Code
            );
        }
        else
        {
            promotion.Status = PromotionStatus.Rejected;
            promotion.RejectReason = dto.RejectReason?.Trim();
            promotion.UpdatedAt = DateTime.UtcNow;

            _loggerService.Info($"[ReviewPromotion] Từ chối voucher {promotion.Code} bởi user {user.Id}");

            await _emailService.SendPromotionRejectedAsync(
                sellerUser.Email,
                sellerUser.FullName,
                promotion.Code,
                dto.RejectReason ?? "Không xác định"
            );
        }

        await _unitOfWork.Promotions.Update(promotion);
        await _unitOfWork.SaveChangesAsync();

        return _mapperService.Map<Promotion, PromotionDto>(promotion);
    }


    #region private methods

    private async Task<Promotion> SetPromotionDataAsync(CreatePromotionDto dto, User user)
    {
        var promotion = new Promotion
        {
            Code = dto.Code.Trim().ToUpper(),
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
                throw ErrorHelper.BadRequest("Bạn chỉ được tạo tối đa 3 voucher đang chờ duyệt hoặc đã duyệt.");
        }
    }

    private async Task ValidatePromotionInputAsync(CreatePromotionDto dto)
    {
        // Validate format: 6 ký tự in hoa
        if (!Regex.IsMatch(dto.Code ?? "", @"^[A-Z]{6}$"))
            throw ErrorHelper.BadRequest("Mã voucher phải gồm đúng 6 ký tự in hoa (A-Z).");

        // Validate trùng mã
        var isExisted = await _unitOfWork.Promotions
            .GetQueryable()
            .AnyAsync(p => p.Code == dto.Code);

        if (isExisted)
            throw ErrorHelper.BadRequest("Mã voucher đã tồn tại. Vui lòng chọn mã khác.");

        // Validate discount
        if (dto.DiscountType == DiscountType.Percentage)
        {
            if (dto.DiscountValue <= 0 || dto.DiscountValue > 100)
                throw ErrorHelper.BadRequest("Giá trị giảm phần trăm phải lớn hơn 0 và không vượt quá 100.");
        }
        else if (dto.DiscountType == DiscountType.Fixed)
        {
            if (dto.DiscountValue <= 0)
                throw ErrorHelper.BadRequest("Giá trị giảm cố định phải lớn hơn 0.");
        }
    }

    #endregion
}