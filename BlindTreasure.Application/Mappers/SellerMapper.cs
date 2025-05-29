using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Mappers;

public static class SellerMapper
{
    public static SellerDto ToSellerDto(Seller seller)
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
    
    public static SellerProfileDto ToSellerProfileDto(Seller seller)
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
}