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
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapperService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;
    private readonly ICacheService _cacheService;

    public PromotionService(IUnitOfWork unitOfWork, ILoggerService loggerService, IMapperService mapperService,
        IClaimsService claimsService, IUserService userService, IEmailService emailService, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _mapperService = mapperService;
        _claimsService = claimsService;
        _userService = userService;
        _emailService = emailService;
        _cacheService = cacheService;
    }

    public async Task<Pagination<PromotionDto>> GetPromotionsAsync(PromotionQueryParameter param)
    {
        var query = _unitOfWork.Promotions.GetQueryable().Where(p => !p.IsDeleted); // THÊM WHERE

        if (param.SellerId.HasValue)
            query = query.Where(p => p.SellerId == param.SellerId.Value);

        if (param.Status.HasValue)
            query = query.Where(p => p.Status == param.Status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p =>
                p.Status == PromotionStatus.Pending ? 0 :
                p.Status == PromotionStatus.Approved ? 1 :
                2)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();


        var dtos = items
            .Select(p => _mapperService.Map<Promotion, PromotionDto>(p))
            .ToList();

        return new Pagination<PromotionDto>(dtos, totalCount, param.PageIndex, param.PageSize);
    }

    public async Task<PromotionDto> GetPromotionByIdAsync(Guid id)
    {
        var cacheKey = $"Promotion:Detail:{id}";
        var cached = await _cacheService.GetAsync<PromotionDto>(cacheKey);
        if (cached != null) return cached;

        var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (promotion == null)
            throw ErrorHelper.NotFound("Không tìm thấy voucher.");

        var result = await MapPromotionToDto(promotion);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
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
        await _cacheService.RemoveByPatternAsync("Promotion:List:*");

        return await GetPromotionByIdAsync(promotion.Id);
    }

    public async Task<PromotionDto> UpdatePromotionAsync(Guid id, CreatePromotionDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, true);

        if (user == null || (user.RoleName != RoleType.Staff && user.RoleName != RoleType.Admin))
            throw ErrorHelper.Forbidden("Bạn không có quyền cập nhật voucher.");

        var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (promotion == null)
            throw ErrorHelper.NotFound("Không tìm thấy voucher.");

        await ValidatePromotionInputAsync(dto);

        promotion.Code = dto.Code.Trim().ToUpper();
        promotion.Description = dto.Description;
        promotion.DiscountType = dto.DiscountType;
        promotion.DiscountValue = dto.DiscountValue;
        promotion.StartDate = dto.StartDate;
        promotion.EndDate = dto.EndDate;
        promotion.UsageLimit = dto.UsageLimit;
        promotion.UpdatedAt = DateTime.UtcNow;
        promotion.CreatedByRole = user.RoleName;

        await _unitOfWork.Promotions.Update(promotion);
        await _unitOfWork.SaveChangesAsync();

        // Xóa cache detail và list
        await _cacheService.RemoveAsync($"Promotion:Detail:{id}");
        await _cacheService.RemoveByPatternAsync("Promotion:List:*");

        return await GetPromotionByIdAsync(promotion.Id);
    }


    public async Task<PromotionDto> DeletePromotionAsync(Guid id)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, true);

        if (user == null || (user.RoleName != RoleType.Staff && user.RoleName != RoleType.Admin))
            throw ErrorHelper.Forbidden("Bạn không có quyền xoá voucher.");

        var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (promotion == null)
            throw ErrorHelper.NotFound("Không tìm thấy voucher.");

        await _unitOfWork.Promotions.SoftRemove(promotion);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveAsync($"Promotion:Detail:{id}");
        await _cacheService.RemoveByPatternAsync("Promotion:List:*");
        // Gọi lại hàm get by id để trả về dto
        return await GetPromotionByIdAsync(id);
    }

    public async Task<PromotionDto> ReviewPromotionAsync(ReviewPromotionDto dto)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, true);

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

        var sellerUser = await _userService.GetUserById(seller.UserId, true);
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
        await _cacheService.RemoveAsync($"Promotion:Detail:{promotion.Id}");
        await _cacheService.RemoveByPatternAsync("Promotion:List:*");
        return await GetPromotionByIdAsync(promotion.Id);
    }

    public async Task<PromotionApplicationResultDto> ApplyVoucherAsync(string voucherCode, Guid orderId)
    {
        // 1. Tìm order
        var order = await _unitOfWork.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null)
            throw ErrorHelper.NotFound("Không tìm thấy đơn hàng.");

        // 2. Tìm promotion theo mã
        var promotion = await _unitOfWork.Promotions
            .FirstOrDefaultAsync(p => p.Code == voucherCode.Trim());

        if (promotion == null)
            throw ErrorHelper.BadRequest("Mã voucher không tồn tại.");

        // 3. Kiểm tra trạng thái
        if (promotion.Status != PromotionStatus.Approved)
            throw ErrorHelper.BadRequest("Voucher chưa được duyệt.");

        // 4. Kiểm tra thời hạn
        var now = DateTime.UtcNow;
        if (now < promotion.StartDate || now > promotion.EndDate)
            throw ErrorHelper.BadRequest("Voucher đã hết hạn hoặc chưa bắt đầu.");

        // 5. Kiểm tra usage
        var usageCount = await _unitOfWork.Orders.CountAsync(o =>
            o.CreatedAt >= promotion.StartDate &&
            o.CreatedAt <= promotion.EndDate &&
            o.Status != "Cancelled" &&
            o.UserId == order.UserId); // có thể tracking thêm bảng OrderPromotion nếu có

        if (promotion.UsageLimit.HasValue && usageCount >= promotion.UsageLimit.Value)
            throw ErrorHelper.BadRequest("Voucher đã được sử dụng quá giới hạn.");


        // // 6. Kiểm tra phạm vi
        // if (promotion.SellerId.HasValue)
        // {
        //     var hasSellerItems = await _unitOfWork.OrderDetails.(od =>
        //         od.OrderId == order.Id && od.SellerId == promotion.SellerId);
        //     if (!hasSellerItems)
        //         throw ErrorHelper.BadRequest("Voucher không áp dụng cho đơn hàng này.");
        // }

        // 7. Tính giảm giá
        decimal discountAmount = 0;
        if (promotion.DiscountType == DiscountType.Percentage)
            discountAmount = Math.Round(order.TotalAmount * (promotion.DiscountValue / 100m), 2);
        else if (promotion.DiscountType == DiscountType.Fixed) discountAmount = promotion.DiscountValue;

        discountAmount = Math.Min(discountAmount, order.TotalAmount);
        var finalAmount = order.TotalAmount - discountAmount;

        return new PromotionApplicationResultDto
        {
            PromotionCode = promotion.Code,
            OriginalAmount = order.TotalAmount,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            Message = "Áp dụng voucher thành công."
        };
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
            UsageLimit = dto.UsageLimit > 0 ? dto.UsageLimit : null,
            CreatedByRole = user.RoleName
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
                p.Status == PromotionStatus.Pending);

            if (count >= 3)
                throw ErrorHelper.BadRequest("Bạn chỉ được tạo tối đa 3 voucher đang chờ duyệt hoặc đã duyệt.");
        }
    }

    private async Task ValidatePromotionInputAsync(CreatePromotionDto dto)
    {
        // Validate format: 6 ký tự in hoa
        if (!Regex.IsMatch(dto.Code, @"^[A-Z]{6}$"))
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


    private async Task<PromotionDto> MapPromotionToDto(Promotion promotion)
    {
        var dto = _mapperService.Map<Promotion, PromotionDto>(promotion);

        // Lấy user tạo promotion
        var user = await _unitOfWork.Users.FirstOrDefaultAsync(u => u.Id == promotion.CreatedBy);
        if (user != null)
            dto.CreatedByRole = user.RoleName;
        else
            dto.CreatedByRole = null;

        return dto;
    }

    #endregion
}