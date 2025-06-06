﻿using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.EmailDTOs;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;

namespace BlindTreasure.Application.Services;

public class SellerVerificationService : ISellerVerificationService
{
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;

    public SellerVerificationService(IUnitOfWork unitOfWork, IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
    }

    public async Task<bool> VerifySellerAsync(Guid sellerId, SellerVerificationDto dto)
    {
        var seller = await _unitOfWork.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId, s => s.User);
        if (seller == null)
            throw ErrorHelper.NotFound("Không tìm thấy hồ sơ seller.");

        if (seller.User == null)
            throw ErrorHelper.Internal("Không tìm thấy thông tin người dùng của seller.");

        seller.IsVerified = dto.IsApproved;
        seller.Status = dto.IsApproved ? SellerStatus.Approved : SellerStatus.Rejected;
        seller.RejectReason = !dto.IsApproved ? dto.RejectReason : null;

        await _unitOfWork.Sellers.Update(seller);
        await _unitOfWork.SaveChangesAsync();

        // Gửi email chúc mừng nếu được duyệt
        if (dto.IsApproved)
            await _emailService.SendSellerApprovalSuccessAsync(new EmailRequestDto
            {
                To = seller.User.Email,
                UserName = seller.User.FullName
            });
        else if (!string.IsNullOrWhiteSpace(dto.RejectReason))
            await _emailService.SendSellerRejectionAsync(new EmailRequestDto
            {
                To = seller.User.Email,
                UserName = seller.User.FullName
            }, dto.RejectReason);

        return true;
    }
}