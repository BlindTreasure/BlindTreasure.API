using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.DTOs.ProductDTOs;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/products")]
[ApiController]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }


    /// <summary>
    ///     Lấy danh sách sản phẩm dùng chung mọi role (có phân trang).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<ProductDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] ProductQueryParameter param)
    {
        try
        {
            var result = await _productService.GetAllAsync(param);
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
    ///     Lấy chi tiết sản phẩm theo Id dùng chung cho mọi role.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _productService.GetByIdAsync(id);
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
    ///     đăng ký sản phẩm mới cho seller, có field user id. Dùng cho các role quản trị
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
    public async Task<IActionResult> Create([FromForm] ProductCreateDto dto, IFormFile? productImageUrl)
    {
        try
        {
            var result = await _productService.CreateAsync(dto, productImageUrl);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Tạo sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    // <summary>
    /// Cập nhật sản phẩm  dùng cho các role quản trị như admin, staff (seller).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> Update(Guid id, [FromForm] ProductUpdateDto dto, IFormFile? productImageUrl)
    {
        try
        {
            var result = await _productService.UpdateAsync(id, dto, productImageUrl);
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
    ///     Xóa mềm sản phẩm dùng cho các role quản trị như admin, staff (seller).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<ProductDto>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _productService.DeleteAsync(id);
            return Ok(ApiResult<ProductDto>.Success(result, "200", "Xóa sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<ProductDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
    /// <summary>
    /// Cập nhật ảnh sản phẩm. Backup
    /// </summary>
    [HttpPut("{id}/image")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResult<string>), 200)]
    [ProducesResponseType(typeof(ApiResult<string>), 400)]
    [ProducesResponseType(typeof(ApiResult<string>), 404)]
    public async Task<IActionResult> UpdateProductImage(Guid id, IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResult.Failure("400", "File không hợp lệ."));

        try
        {
            var imageUrl = await _productService.UploadProductImageAsync(id, file);
            if (string.IsNullOrEmpty(imageUrl))
                return BadRequest(ApiResult.Failure("400", "Không thể cập nhật ảnh sản phẩm."));

            return Ok(ApiResult<string>.Success(imageUrl, "200", "Cập nhật ảnh sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<string>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}