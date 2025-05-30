﻿using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/sellers")]
public class SellerController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly ISellerService _sellerService;
    private readonly ISellerVerificationService _sellerVerificationService;

    public SellerController(ISellerService sellerService, IClaimsService claimsService,
        ISellerVerificationService sellerVerificationService)
    {
        _sellerService = sellerService;
        _claimsService = claimsService;
        _sellerVerificationService = sellerVerificationService;
    }

    /// <summary>
    ///     Staff xem list của Seller cung voi status
    /// </summary>
    [HttpGet]
    // [Authorize(Roles = "Staff, Admin")]
    [ProducesResponseType(typeof(ApiResult<Pagination<SellerDto>>), 200)]
    public async Task<IActionResult> GetAllSellers([FromQuery] SellerStatus? status,
        [FromQuery] PaginationParameter paging)
    {
        try
        {
            var result = await _sellerService.GetAllSellersAsync(status, paging);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách sellers thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<Pagination<SellerDto>>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Staff xem dc ho so cua seller Pending
    /// </summary>
    // [Authorize(Roles = "Admin,Staff")]
    [HttpGet("{sellerId}")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    public async Task<IActionResult> GetSellerDocument(Guid sellerId)
    {
        try
        {
            var data = await _sellerService.GetSellerProfileByIdAsync(sellerId);
            return Ok(ApiResult<object>.Success(data, "200", "Lấy thông tin của Seller thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    ///     Seller upload document files
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpPost("document")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        try
        {
            var userId = _claimsService.GetCurrentUserId;
            var fileUrl = await _sellerService.UploadSellerDocumentAsync(userId, file);
            return Ok(ApiResult<string>.Success(fileUrl, "200", "Tải tài liệu thành công, chờ xác minh."));
        }
        catch (Exception ex)
        {
            var status = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(status, error);
        }
    }

    [HttpPut("{sellerId}/verify")]
    public async Task<IActionResult> VerifySeller(Guid sellerId, [FromForm] SellerVerificationDto dto)
    {
        try
        {
            await _sellerVerificationService.VerifySellerAsync(sellerId, dto);

            var msg = dto.IsApproved
                ? "Seller đã được xác minh."
                : $"Seller đã bị từ chối. Lý do: {dto.RejectReason ?? "Không có"}";

            return Ok(ApiResult.Success("200", msg));
        }
        catch (Exception ex)
        {
            var status = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(status, error);
        }
    }
}