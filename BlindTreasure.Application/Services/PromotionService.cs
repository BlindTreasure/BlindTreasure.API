﻿using System.Text.RegularExpressions;
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
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IMapperService _mapperService;
    private readonly ISellerService _sellerService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserService _userService;

    public PromotionService(IUnitOfWork unitOfWork, ILoggerService loggerService, IMapperService mapperService,
        IClaimsService claimsService, IUserService userService, ISellerService sellerService,
        IEmailService emailService, ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _mapperService = mapperService;
        _claimsService = claimsService;
        _userService = userService;
        _emailService = emailService;
        _cacheService = cacheService;
        _sellerService = sellerService;
    }

    public async Task<Pagination<PromotionDto>> GetPromotionsAsync(PromotionQueryParameter param)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var currentUser = await _userService.GetUserById(currentUserId, true);
        if (currentUser == null)
            throw ErrorHelper.Unauthorized("Không tìm thấy thông tin người dùng.");

        var query = _unitOfWork.Promotions.GetQueryable().Where(p => !p.IsDeleted);
        var role = currentUser.RoleName;

        //Nếu người đăng nhập là seller
        if (role == RoleType.Seller)
        {
            // Promotion của chính shop seller + toàn sàn
            if (param.Status.HasValue)
                query = query.Where(p =>
                    (p.SellerId == param.SellerId && p.Status == param.Status) ||
                    p.CreatedByRole != RoleType.Seller);
            else
                query = query.Where(p =>
                    p.SellerId == param.SellerId ||
                    p.CreatedByRole != RoleType.Seller);

            // Include PromotionParticipants để check IsParticipant
            query = query.Include(p => p.PromotionParticipants);
        }
        else if (param.SellerId.HasValue)
        {
            // Staff / Customer xem promotion theo seller cụ thể
            query = query.Where(p =>
                p.SellerId == param.SellerId ||
                p.PromotionParticipants.Any(pp => pp.SellerId == param.SellerId));

            if (param.Status.HasValue) query = query.Where(p => p.Status == param.Status);

            if (role == RoleType.Customer) query = query.Where(p => p.Status == PromotionStatus.Approved);
        }
        else
        {
            // Staff/Customer xem tất cả promotion (không truyền SellerId)
            if (param.Status.HasValue) query = query.Where(p => p.Status == param.Status);

            if (role == RoleType.Customer) query = query.Where(p => p.Status == PromotionStatus.Approved);
        }

        var totalCount = await query.CountAsync();
        var orderedQuery = query.OrderBy(p =>
            p.Status == PromotionStatus.Pending ? 0 :
            p.Status == PromotionStatus.Approved ? 1 : 2);

        orderedQuery = param.Desc
            ? orderedQuery.ThenByDescending(p => p.CreatedAt)
            : orderedQuery.ThenBy(p => p.CreatedAt);

        var items = await orderedQuery
            .Skip((param.PageIndex - 1) * param.PageSize)
            .Take(param.PageSize)
            .ToListAsync();

        var dtos = items.Select(p =>
        {
            var dto = _mapperService.Map<Promotion, PromotionDto>(p);

            // Nếu là seller và là promotion global, check IsParticipant
            if (role == RoleType.Seller && p.CreatedByRole != RoleType.Seller)
                // Null check để tránh lỗi
                dto.IsParticipant =
                    p.PromotionParticipants?.Any(pp => pp.SellerId == param.SellerId && !pp.IsDeleted) ?? false;
            else if (role == RoleType.Seller && p.SellerId == param.SellerId)
                // Promotion riêng của seller thì IsParticipant = null
                dto.IsParticipant = null;
            else
                // Các role khác thì IsParticipant = null
                dto.IsParticipant = null;

            return dto;
        }).ToList();

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

        _loggerService.Success($"[DeleteBlindBoxAsync] Đã xoá Blind Box {promotion.Id}.");
        var result = _mapperService.Map<Promotion, PromotionDto>(promotion);

        return result;
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
        //if (promotion.SellerId.HasValue)
        //{
        //    var hasSellerItems = await _unitOfWork.OrderDetails.(od =>
        //        od.OrderId == order.Id && od.SellerId == promotion.SellerId);
        //    if (!hasSellerItems)
        //        throw ErrorHelper.BadRequest("Voucher không áp dụng cho đơn hàng này.");
        //}

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

    public async Task<ParticipantPromotionDto> ParticipatePromotionAsync(Guid id)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var currentUser = await _userService.GetUserById(currentUserId, true);
        if (currentUser == null)
            throw ErrorHelper.Unauthorized("Không tìm thấy thông tin người dùng.");
        if (currentUser.RoleName != RoleType.Seller)
            throw ErrorHelper.Unauthorized("Chỉ có seller mới có quyền tham gia voucher.");

        await ValidateParticipantPromotionAsync(currentUser, id);
        var participantPromotion = await SetParticipantPromotionDataAsync(currentUser.Id, id);
        await _unitOfWork.PromotionParticipants.AddAsync(participantPromotion);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveByPatternAsync("ParticipantPromotion:List:*");

        return await GetParticipantPromotionByIdAsync(participantPromotion.Id);
    }

    public async Task<ParticipantPromotionDto> WithdrawPromotionAsync(WithdrawParticipantPromotionDto param)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var currentUser = await _userService.GetUserById(currentUserId, true);
        var currentSeller = await _sellerService.GetSellerProfileByIdAsync(param.SellerId);

        if (currentUser == null || currentSeller.UserId != currentUserId)
            throw ErrorHelper.Unauthorized("Không tìm thấy thông tin người dùng.");
        if (currentUser.RoleName != RoleType.Seller)
            throw ErrorHelper.Unauthorized("Chỉ có seller mới có quyền rút khỏi chiến dịch voucher.");

        var promotionParticipant = await _unitOfWork.PromotionParticipants.FirstOrDefaultAsync(p =>
            p.PromotionId == param.PromotionId && p.SellerId == param.SellerId && !p.IsDeleted);
        if (promotionParticipant == null)
            throw ErrorHelper.NotFound("Seller chưa tham gia chiến dịch voucher, không thể rút");

        await _unitOfWork.PromotionParticipants.SoftRemove(promotionParticipant);
        await _unitOfWork.SaveChangesAsync();
        await _cacheService.RemoveAsync($"PromotionParticipant:Detail:{param.PromotionId}");
        await _cacheService.RemoveByPatternAsync("PromotionParticipant:List:*");

        _loggerService.Success($"[DeleteBlindBoxAsync] Đã rút khỏi chiến dịch voucher {promotionParticipant.Id}.");
        var result = _mapperService.Map<PromotionParticipant, ParticipantPromotionDto>(promotionParticipant);

        return result;
    }

    public async Task<List<SellerParticipantDto>> GetPromotionParticipantsAsync(
        SellerParticipantPromotionParameter param)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var user = await _userService.GetUserById(currentUserId, true);
        if (user?.RoleName != RoleType.Staff && user?.RoleName != RoleType.Admin)
            throw ErrorHelper.Forbidden("Không có quyền xem danh sách tham gia.");

        var participants = _unitOfWork.PromotionParticipants
            .GetQueryable()
            .Where(pp => pp.PromotionId == param.PromotionId && !pp.IsDeleted)
            .Include(pp => pp.Seller)
            .ThenInclude(s => s.User);

        var result = participants.Select(pp => new SellerParticipantDto
        {
            Id = pp.Seller.Id,
            Email = pp.Seller.User.Email,
            FullName = pp.Seller.User.FullName,
            Phone = pp.Seller.User.Phone,
            CompanyName = pp.Seller.CompanyName,
            TaxId = pp.Seller.TaxId,
            CompanyAddress = pp.Seller.CompanyAddress,
            IsVerified = pp.Seller.IsVerified,
            JoinedAt = pp.JoinedAt
        }).ToList();

        return result;
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

    private async Task<Promotion?> GetPromotionByIdRawAsync(Guid id)
    {
        return await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == id);
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

    private async Task ValidateParticipantPromotionAsync(User user, Guid promotionId)
    {
        if (user.RoleName == RoleType.Seller)
        {
            var seller =
                await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id && s.IsVerified && !s.IsDeleted);
            if (seller == null)
            {
                _loggerService.Warn("ValidateParticipantPromotion: Seller chưa xác minh hoặc bị xoá");
                throw ErrorHelper.Forbidden("Tài khoản không đủ điều kiện để tham gia chiến dịch.");
            }

            var hasWithdrawn = await _unitOfWork.PromotionParticipants.FirstOrDefaultAsync(pp =>
                pp.SellerId == seller.Id &&
                pp.PromotionId == promotionId &&
                pp.IsDeleted == true);

            if (hasWithdrawn != null)
                throw ErrorHelper.Conflict("Bạn đã rút khỏi chiến dịch này trước đó và không thể tham gia lại.");

            // Đếm số chiến dịch voucher toàn sàn đang active mà seller đã tham gia
            var activeParticipantCount = await _unitOfWork.PromotionParticipants.CountAsync(pp =>
                pp.SellerId == seller.Id &&
                pp.Promotion.CreatedByRole != RoleType.Seller && // Chỉ voucher toàn sàn
                pp.Promotion.Status == PromotionStatus.Approved &&
                pp.Promotion.EndDate >= DateTime.UtcNow && // Chưa hết hạn
                !pp.Promotion.IsDeleted);

            if (activeParticipantCount >= 2)
                throw ErrorHelper.BadRequest(
                    "Bạn chỉ được tham gia tối đa 2 chiến dịch voucher toàn sàn cùng lúc. Vui lòng chờ chiến dịch hiện tại kết thúc.");

            var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId);
            if (promotion.StartDate <= DateTime.Now)
                throw ErrorHelper.BadRequest("Không thể tham gia/rút khỏi chiến dịch đã bắt đầu.");
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

    private async Task<ParticipantPromotionDto> MapParticipantPromotionToDto(PromotionParticipant promotionParticipant)
    {
        var dto = _mapperService.Map<PromotionParticipant, ParticipantPromotionDto>(promotionParticipant);

        return dto;
    }

    private async Task<PromotionParticipant> SetParticipantPromotionDataAsync(Guid userId, Guid promotionId)
    {
        var promotion = await _unitOfWork.Promotions.FirstOrDefaultAsync(p => p.Id == promotionId);
        var seller =
            await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId && s.IsVerified && !s.IsDeleted);

        var participantPromotion = new PromotionParticipant
        {
            Seller = seller,
            Promotion = promotion,
            SellerId = seller.Id,
            PromotionId = promotion.Id,
            JoinedAt = DateTime.UtcNow
        };

        return participantPromotion;
    }

    public async Task<ParticipantPromotionDto> GetParticipantPromotionByIdAsync(Guid id)
    {
        var cacheKey = $"ParticipantPromotion:Detail:{id}";
        var cached = await _cacheService.GetAsync<ParticipantPromotionDto>(cacheKey);
        if (cached != null) return cached;

        var participantPromotion =
            await _unitOfWork.PromotionParticipants.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (participantPromotion == null)
            throw ErrorHelper.NotFound("Không tìm thấy voucher.");

        var result = await MapParticipantPromotionToDto(participantPromotion);

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    #endregion
}