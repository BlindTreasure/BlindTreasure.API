using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "CustomerPolicy")]
public class CustomerFavouriteController : ControllerBase
{
    private readonly ICustomerFavouriteService _customerFavouriteService;

    public CustomerFavouriteController(ICustomerFavouriteService customerFavouriteService)
    {
        _customerFavouriteService = customerFavouriteService;
    }

    /// <summary>
    /// Thêm sản phẩm/blind box vào danh sách yêu thích
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddToFavourite([FromBody] AddFavouriteRequestDto request)
    {
        try
        {
            var result = await _customerFavouriteService.AddToFavouriteAsync(request);
            return Ok(ApiResult<CustomerFavouriteDto>.Success(result, message: "Đã thêm vào danh sách yêu thích."));
        }
        catch (Exception ex)
        {
            var statusCode = ex.Data["StatusCode"] as int? ?? 500;
            return StatusCode(statusCode, ApiResult<CustomerFavouriteDto>.Failure(
                statusCode.ToString(), ex.Message));
        }
    }

    /// <summary>
    /// Xóa item khỏi danh sách yêu thích
    /// </summary>
    [HttpDelete("{favouriteId}")]
    public async Task<IActionResult> RemoveFromFavourite(Guid favouriteId)
    {
        try
        {
            await _customerFavouriteService.RemoveFromFavouriteAsync(favouriteId);
            return Ok(ApiResult.Success(message: "Đã xóa khỏi danh sách yêu thích."));
        }
        catch (Exception ex)
        {
            var statusCode = ex.Data["StatusCode"] as int? ?? 500;
            return StatusCode(statusCode, ApiResult.Failure(statusCode.ToString(), ex.Message));
        }
    }

    /// <summary>
    /// Lấy danh sách yêu thích của user hiện tại
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyFavourites([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var result = await _customerFavouriteService.GetUserFavouritesAsync(page, pageSize);
            return Ok(ApiResult<FavouriteListResponseDto>.Success(result));
        }
        catch (Exception ex)
        {
            var statusCode = ex.Data["StatusCode"] as int? ?? 500;
            return StatusCode(statusCode, ApiResult<FavouriteListResponseDto>.Failure(
                statusCode.ToString(), ex.Message));
        }
    }

    /// <summary>
    /// Kiểm tra sản phẩm/blind box có trong danh sách yêu thích không
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckFavourite([FromQuery] Guid? productId, [FromQuery] Guid? blindBoxId)
    {
        try
        {
            var result = await _customerFavouriteService.IsInFavouriteAsync(productId, blindBoxId);
            return Ok(ApiResult<bool>.Success(result));
        }
        catch (Exception ex)
        {
            var statusCode = ex.Data["StatusCode"] as int? ?? 500;
            return StatusCode(statusCode, ApiResult<bool>.Failure(statusCode.ToString(), ex.Message));
        }
    }
}