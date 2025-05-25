using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.Utils;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

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

        var fileName = $"seller-documentation/{userId}/{Guid.NewGuid()}_{file.FileName}";

        await using var stream = file.OpenReadStream();
        await _blobService.UploadFileAsync(fileName, stream);

        var fileUrl = await _blobService.GetFileUrlAsync(fileName);

        seller.CoaDocumentUrl = fileUrl;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        _loggerService.Info($"[UploadSellerDocumentAsync] Seller {userId} uploaded COA document: {fileName}");

        return fileUrl;
    }
}