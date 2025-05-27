using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.SellerDTOs;
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

    public async Task<string> GetSellerDocumentUrlAsync(Guid sellerId)
    {
        var seller = await _unitOfWork.Sellers.GetByIdAsync(sellerId);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (string.IsNullOrWhiteSpace(seller.CoaDocumentUrl))
            throw ErrorHelper.NotFound("Seller chưa upload tài liệu COA.");

        return seller.CoaDocumentUrl;
    }

    public async Task<Pagination<SellerDto>> GetAllSellersAsync(SellerStatus? status, PaginationParameter pagination)
    {
        var query = _unitOfWork.Sellers.GetQueryable()
            .Where(s => !s.IsDeleted)
            .Include(s => s.User)
            .AsQueryable();

        if (status.HasValue) query = query.Where(s => s.Status == status.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pagination.PageIndex - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(s => new SellerDto
            {
                Id = s.Id,
                Email = s.User.Email,
                FullName = s.User.FullName,
                CompanyName = s.CompanyName,
                CoaDocumentUrl = s.CoaDocumentUrl,
                Status = s.Status,
                IsVerified = s.IsVerified
            })
            .ToListAsync();

        return new Pagination<SellerDto>(items, totalCount, pagination.PageIndex, pagination.PageSize);
    }
}