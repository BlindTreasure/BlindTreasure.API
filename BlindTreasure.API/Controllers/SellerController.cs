using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Authorize(Roles = "Seller")]
[ApiController]
[Route("api/sellers")]
public class SellerController : ControllerBase
{
    private readonly ISellerService _sellerService;
    private readonly IClaimsService _claimsService;

    public SellerController(ISellerService sellerService, IClaimsService claimsService)
    {
        _sellerService = sellerService;
        _claimsService = claimsService;
    }

    [HttpPost("document")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> UploadDocument([FromForm] IFormFile file)
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
}
