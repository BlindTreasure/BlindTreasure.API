using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.SellerDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ApiController]
[Route("api/admin")] // hoặc "api/seller-verification"
public class AdminController : ControllerBase
{
    private readonly ISellerVerificationService _sellerVerificationService;

    public AdminController(ISellerVerificationService sellerVerificationService)
    {
        _sellerVerificationService = sellerVerificationService;
    }

    [HttpPut("sellers/{id}/verify")]
    public async Task<IActionResult> VerifySeller(Guid id, [FromBody] SellerVerificationDto dto)
    {
        try
        {
            await _sellerVerificationService.VerifySellerAsync(id, dto.IsApproved);
            var msg = dto.IsApproved ? "Seller đã được xác minh." : "Seller đã bị từ chối.";
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