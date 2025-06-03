using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
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
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    public async Task<IActionResult> GetSellerDocument(Guid id)
    {
        try
        {
            var data = await _sellerService.GetSellerProfileByIdAsync(id);
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

    /// <summary>
    ///     Lấy danh sách sản phẩm của Seller (có phân trang).
    /// </summary>
    [HttpGet("products")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProductDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] ProductQueryParameter param)
    {
        try
        {
            var userId = _claimsService.GetCurrentUserId;

            var result = await _sellerService.GetAllProductsAsync(param, userId);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }


    /// <summary>
    ///     Lấy chi tiết sản phẩm theo Id seller.
    /// </summary>
    [HttpGet("products/{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var userId = _claimsService.GetCurrentUserId;

            var result = await _sellerService.GetProductByIdAsync(id, userId);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Lấy thông tin sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Seller tạo sản phẩm mới.
    /// </summary>
    [HttpPost("products")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
    public async Task<IActionResult> CreateProduct([FromForm] ProductSellerCreateDto dto, IFormFile? productImageUrl)
    {
        try
        {
            var result = await _sellerService.CreateProductAsync(dto, productImageUrl);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Tạo sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Seller cập nhật sản phẩm.
    /// </summary>
    [HttpPut("products/{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromForm] ProductUpdateDto dto, IFormFile? productImageUrl)
    {
        try
        {
            var result = await _sellerService.UpdateProductAsync(id, dto, productImageUrl);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Cập nhật sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Seller xóa mềm sản phẩm.
    /// </summary>
    [HttpDelete("products/{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        try
        {
            var result = await _sellerService.DeleteProductAsync(id);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Xóa sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}