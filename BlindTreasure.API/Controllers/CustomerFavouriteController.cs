using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CustomerFavouriteDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/customer-favourites")]
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
    public async Task<IActionResult> AddToFavourite([FromForm] AddFavouriteRequestDto request)
    {
        try
        {
            var result = await _customerFavouriteService.AddToFavouriteAsync(request);
            return Ok(ApiResult<CustomerFavouriteDto>.Success(result, message: "Sản phẩm đã được thêm vào danh sách yêu thích của bạn."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
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
            return Ok(ApiResult.Success(message: "Vật phẩm đã được xóa khỏi danh sách yêu thích của bạn."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetMyFavourites([FromQuery] FavouriteQueryParameter parameter)
    {
        try
        {
            var result = await _customerFavouriteService.GetUserFavouritesAsync(parameter);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Danh sách sản phẩm yêu thích của bạn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
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
            return Ok(ApiResult<bool>.Success(result, "200",
                result ? "Sản phẩm này đã có trong danh sách yêu thích của bạn." : "Sản phẩm này chưa có trong danh sách yêu thích của bạn."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}