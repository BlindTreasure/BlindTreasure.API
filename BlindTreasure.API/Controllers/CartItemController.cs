using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Services;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CartItemDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [Authorize] // Yêu cầu user đăng nhập

    public class CartItemController : ControllerBase
    {
        private readonly ICartItemService _cartItemService;

        public CartItemController(ICartItemService cartItemService)
        {
            _cartItemService = cartItemService;
        }


        /// <summary>
        /// Lấy toàn bộ giỏ hàng của user hiện tại.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResult<CartDto>), 200)]
        public async Task<IActionResult> GetCart()
        {
            try
            {
                var cart = await _cartItemService.GetCurrentUserCartAsync();
                return Ok(ApiResult<CartDto>.Success(cart));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<CartDto>(ex);
                return StatusCode(statusCode, errorResponse);

            }
        }

        /// <summary>
        /// Cập nhật số lượng một item trong giỏ hàng.
        /// </summary>
        [HttpPut]
        [ProducesResponseType(typeof(ApiResult<CartDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> UpdateCartItem([FromBody] UpdateCartItemDto dto)
        {
            try
            {
                var result = await _cartItemService.UpdateCartItemAsync(dto);
                return Ok(ApiResult<CartDto>.Success(result, "200", "Cập nhật giỏ hàng thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<CartDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }


        }


        /// <summary>
        /// Xóa một item khỏi giỏ hàng.
        /// </summary>
        [HttpDelete("{cartItemId}")]
        [ProducesResponseType(typeof(ApiResult<CartDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 404)]
        public async Task<IActionResult> RemoveCartItem(Guid cartItemId)
        {
            try
            {
                var result = await _cartItemService.RemoveCartItemAsync(cartItemId);
                return Ok(ApiResult<CartDto>.Success(result, "200", "Xóa item khỏi giỏ hàng thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<CartDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng của user hiện tại.
        /// </summary>
        [HttpDelete("clear")]
        [ProducesResponseType(typeof(ApiResult<object>), 200)]
        public async Task<IActionResult> ClearCart()
        {
            await _cartItemService.ClearCartAsync();
            return Ok(ApiResult<object>.Success(null, "200", "Đã xóa toàn bộ giỏ hàng."));
        }

        /// <summary>
        /// Thêm sản phẩm hoặc blind box vào giỏ hàng.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResult<CartDto>), 200)]
        [ProducesResponseType(typeof(ApiResult<object>), 400)]
        public async Task<IActionResult> AddToCart([FromBody] AddCartItemDto dto)
        {
            try
            {
                var result = await _cartItemService.AddToCartAsync(dto);
                return Ok(ApiResult<CartDto>.Success(result, "200", "Thêm vào giỏ hàng thành công."));
            }
            catch (Exception ex)
            {
                var statusCode = ExceptionUtils.ExtractStatusCode(ex);
                var errorResponse = ExceptionUtils.CreateErrorResponse<CartDto>(ex);
                return StatusCode(statusCode, errorResponse);
            }
        }



    }
}
