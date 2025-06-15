using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/categories")]
// [Authorize(Roles = "Admin,Staff")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    ///     Lấy danh sách danh mục (có phân trang).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<CategoryDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] CategoryQueryParameter param)
    {
        try
        {
            var result = await _categoryService.GetAllAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lấy danh sách category thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpGet("with-products")]
    [ProducesResponseType(typeof(ApiResult<List<CategoryWithProductsDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoriesWithProducts()
    {
        try
        {
            var result = await _categoryService.GetCategoriesWithAllProductsAsync();
            return Ok(ApiResult<List<CategoryWithProductsDto>>.Success(result, "200",
                "Lấy danh sách danh mục kèm sản phẩm thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Lấy thông tin chi tiết một danh mục theo Id.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _categoryService.GetByIdAsync(id);
            return Ok(ApiResult<CategoryDto>.Success(result, "200", "Lấy thông tin category thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<CategoryDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Tạo mới một danh mục sản phẩm.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 409)]
    public async Task<IActionResult> Create([FromForm] CategoryCreateDto dto)
    {
        try
        {
            var result = await _categoryService.CreateAsync(dto);
            return Ok(ApiResult<CategoryDto>.Success(result, "200", "Tạo category thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<CategoryDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Cập nhật thông tin một danh mục.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 404)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 409)]
    public async Task<IActionResult> Update(Guid id, [FromForm] CategoryUpdateDto dto)
    {
        try
        {
            var result = await _categoryService.UpdateAsync(id, dto);
            return Ok(ApiResult<CategoryDto>.Success(result, "200", "Cập nhật category thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<CategoryDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Xóa một danh mục sản phẩm.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var result = await _categoryService.DeleteAsync(id);
            return Ok(ApiResult<CategoryDto>.Success(result, "200", "Xóa category thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<CategoryDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}