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
    private readonly IMapperService _mapperService;
    private readonly IUnitOfWork _unitOfWork;

    public PromotionService(IUnitOfWork unitOfWork, ILoggerService loggerService, IMapperService mapperService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _mapperService = mapperService;
    }

    public async Task<PromotionDto> CreateGlobalPromotionAsync(CreatePromotionDto dto)
    {
        _loggerService.Info($"[CreateGlobalPromotionAsync] Staff tạo voucher toàn sàn: {dto.Code}");

        // Validate đầu vào
        if (dto.StartDate >= dto.EndDate)
            throw ErrorHelper.BadRequest("Thời gian bắt đầu phải nhỏ hơn thời gian kết thúc.");

        if (dto.DiscountType != DiscountType.Percentage && dto.DiscountType != DiscountType.Fixed)
            throw ErrorHelper.BadRequest("Loại khuyến mãi không hợp lệ. Chỉ chấp nhận: percentage, fixed.");

        if (dto.DiscountValue <= 0)
            throw ErrorHelper.BadRequest("Giá trị khuyến mãi phải lớn hơn 0.");

        // Tạo entity
        var promotion = new Promotion
        {
            Code = dto.Code.Trim().ToUpper(),
            Description = dto.Description,
            DiscountType = dto.DiscountType,
            DiscountValue = dto.DiscountValue,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            UsageLimit = dto.UsageLimit,
            Status = PromotionStatus.Approved,
            SellerId = null // voucher toàn sàn nên seller id để null
        };

        await _unitOfWork.Promotions.AddAsync(promotion);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info($"[CreateGlobalPromotionAsync] Tạo thành công voucher toàn sàn: {promotion.Code}");
        return _mapperService.Map<Promotion, PromotionDto>(promotion);
    }
}