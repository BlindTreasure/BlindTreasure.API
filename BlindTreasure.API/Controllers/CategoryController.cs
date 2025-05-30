using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.CategoryDtos;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

/// <summary>
///     Quản lý danh mục sản phẩm. Chỉ Admin/Staff có quyền thao tác.
/// </summary>
[ApiController]
[Route("api/categories")]
[Authorize(Roles = "Admin,Staff")]
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
    /// <remarks>
    ///     Chỉ Admin/Staff được phép truy cập.
    /// </remarks>
    /// <param name="param">Thông tin phân trang</param>
    /// <returns>Danh sách category</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResult<Pagination<CategoryDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 404)]
    public async Task<IActionResult> GetAll([FromQuery] PaginationParameter param)
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

    /// <summary>
    ///     Lấy thông tin chi tiết một danh mục theo Id.
    /// </summary>
    /// <param name="id">Id của category</param>
    /// <returns>Thông tin category</returns>
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
    /// <remarks>
    ///     Tên danh mục phải duy nhất. ParentId có thể null (danh mục gốc).
    /// </remarks>
    /// <param name="dto">Thông tin tạo mới</param>
    /// <returns>Category vừa tạo</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 409)]
    public async Task<IActionResult> Create([FromBody] CategoryCreateDto dto)
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
    /// <remarks>
    ///     Không được tạo vòng lặp khi cập nhật ParentId. Tên phải duy nhất.
    /// </remarks>
    /// <param name="id">Id của category</param>
    /// <param name="dto">Thông tin cập nhật</param>
    /// <returns>Category đã cập nhật</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 400)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 404)]
    [ProducesResponseType(typeof(ApiResult<CategoryDto>), 409)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryUpdateDto dto)
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
    /// <remarks>
    ///     Không được xóa nếu còn sản phẩm hoặc category con liên quan.
    /// </remarks>
    /// <param name="id">Id của category</param>
    /// <returns>CategoryDto với trạng thái isDeleted đã cập nhật</returns>
    [HttpDelete("{id}")]
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