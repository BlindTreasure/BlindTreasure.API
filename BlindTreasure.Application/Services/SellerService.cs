using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class SellerService : ISellerService
{
    private readonly IBlobService _blobService;
    private readonly IEmailService _emailService;
    private readonly ILoggerService _loggerService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerService(IUnitOfWork unitOfWork, ILoggerService loggerService, IEmailService emailService,
        IBlobService blobService)
    {
        _unitOfWork = unitOfWork;
        _loggerService = loggerService;
        _emailService = emailService;
        _blobService = blobService;
    }

    public async Task<SellerDto> UpdateSellerInfoAsync(Guid userId, UpdateSellerInfoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw ErrorHelper.BadRequest("Họ tên không được để trống.");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw ErrorHelper.BadRequest("Số điện thoại không được để trống.");

        if (dto.DateOfBirth == default)
            throw ErrorHelper.BadRequest("Ngày sinh không hợp lệ.");

        if (string.IsNullOrWhiteSpace(dto.CompanyName))
            throw ErrorHelper.BadRequest("Tên công ty không được để trống.");

        if (string.IsNullOrWhiteSpace(dto.TaxId))
            throw ErrorHelper.BadRequest("Mã số thuế không được để trống.");

        if (string.IsNullOrWhiteSpace(dto.CompanyAddress))
            throw ErrorHelper.BadRequest("Địa chỉ công ty không được để trống.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        // Cập nhật User
        seller.User.FullName = dto.FullName;
        seller.User.Phone = dto.PhoneNumber;
        seller.User.DateOfBirth = dto.DateOfBirth;

        // Cập nhật Seller
        seller.CompanyName = dto.CompanyName;
        seller.TaxId = dto.TaxId;
        seller.CompanyAddress = dto.CompanyAddress;
        seller.Status = SellerStatus.WaitingReview;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info($"[UpdateSellerInfoAsync] Seller {userId} đã cập nhật thông tin.");

        return ToSellerDto(seller);
    }

    public async Task<string> UploadSellerDocumentAsync(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw ErrorHelper.BadRequest("File không hợp lệ.");

        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        var fileName = $"seller-documentation/{userId}-{Guid.NewGuid()}_{file.FileName}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetFileUrlAsync(fileName);

        seller.CoaDocumentUrl = fileUrl;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info($"[UploadSellerDocumentAsync] Seller {userId} uploaded COA document: {fileName}");

        return fileUrl;
    }

    public async Task<SellerProfileDto> GetSellerProfileByIdAsync(Guid sellerId)
    {
        var seller = await GetSellerWithUserAsync(sellerId);
        return ToSellerProfileDto(seller);
    }
    
    public async Task<SellerProfileDto> GetSellerProfileByUserIdAsync(Guid userId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.UserId == userId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        return ToSellerProfileDto(seller);
    }


    public async Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination)
    {
        var query = _unitOfWork.Sellers.GetQueryable()
            .Where(s => !s.IsDeleted)
            .Include(s => s.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        var totalCount = await query.CountAsync();

        var sellers = await query
            .Skip((pagination.PageIndex - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        var items = sellers.Select(ToSellerDto).ToList();

        return new Pagination<SellerDto>(items, totalCount, pagination.PageIndex, pagination.PageSize);
    }


    //private method
    private static SellerDto ToSellerDto(Seller seller)
    {
        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        return new SellerDto
        {
            Id = seller.Id,
            Email = seller.User.Email,
            FullName = seller.User.FullName,
            Phone = seller.User.Phone,
            DateOfBirth = seller.User.DateOfBirth,
            CompanyName = seller.CompanyName,
            TaxId = seller.TaxId,
            CompanyAddress = seller.CompanyAddress,
            CoaDocumentUrl = seller.CoaDocumentUrl,
            Status = seller.Status,
            IsVerified = seller.IsVerified
        };
    }

    private static SellerProfileDto ToSellerProfileDto(Seller seller)
    {
        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        var user = seller.User;

        return new SellerProfileDto
        {
            SellerId = seller.Id,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.Phone ?? string.Empty,
            DateOfBirth = user.DateOfBirth,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status.ToString(),

            CompanyName = seller.CompanyName,
            TaxId = seller.TaxId,
            CompanyAddress = seller.CompanyAddress,
            CoaDocumentUrl = seller.CoaDocumentUrl,
            SellerStatus = seller.Status.ToString(),
            IsVerified = seller.IsVerified,
            RejectReason = seller.RejectReason
        };
    }

    private async Task<Seller> GetSellerWithUserAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Dữ liệu user không hợp lệ.");

        return seller;
    }
}