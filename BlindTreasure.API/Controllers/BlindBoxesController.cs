using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.BlindBoxDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/blind-boxes")]
public class BlindBoxesController : ControllerBase
{
    private readonly IBlindBoxService _blindBoxService;

    public BlindBoxesController(IBlindBoxService blindBoxService)
    {
        _blindBoxService = blindBoxService;
    }

    /// <summary>
    ///     Lấy danh sách tất cả Blind Box của seller hiện tại (phân trang)
    ///     Search là search by name
    /// </summary>
    /// <param name="param">Tham số phân trang (PageIndex, PageSize)</param>
    /// <returns>Danh sách BlindBox phân trang</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Pagination<BlindBoxDetailDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<Pagination<BlindBoxDetailDto>>> GetAll([FromQuery] BlindBoxQueryParameter param)
    {
        try
        {
            var result = await _blindBoxService.GetAllBlindBoxesAsync(param);
            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Danh sách Blind Box đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<Pagination<BlindBoxDetailDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }


    /// <summary>
    ///     Lấy chi tiết Blind Box theo Id
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <returns>Thông tin chi tiết Blind Box cùng danh sách item</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BlindBoxDetailDto>> GetById(Guid id)
    {
        try
        {
            var result = await _blindBoxService.GetBlindBoxByIdAsync(id);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200",
                "Thông tin chi tiết Blind Box đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Tạo mới Blind Box kèm upload ảnh đại diện
    /// </summary>
    /// <param name="dto">Dữ liệu Blind Box kèm file ảnh</param>
    /// <returns>Thông tin chi tiết Blind Box vừa tạo</returns>
    [HttpPost]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<BlindBoxDetailDto>> Create([FromForm] CreateBlindBoxDto dto)
    {
        try
        {
            var result = await _blindBoxService.CreateBlindBoxAsync(dto);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "201", "Blind Box đã được tạo thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BlindBoxDetailDto>> Update(Guid id, [FromForm] UpdateBlindBoxDto dto)
    {
        try
        {
            var result = await _blindBoxService.UpdateBlindBoxAsync(id, dto);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200", "Blind Box đã được cập nhật thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }


    /// <summary>
    ///     Thêm danh sách item vào Blind Box
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <param name="items">Danh sách item Blind Box cần thêm</param>
    /// <returns>Thông tin chi tiết Blind Box sau khi cập nhật</returns>
    [HttpPost("{id}/items")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<BlindBoxDetailDto>> AddItems(Guid id, [FromBody] List<BlindBoxItemRequestDto> items)
    {
        try
        {
            var result = await _blindBoxService.AddItemsToBlindBoxAsync(id, items);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200",
                "Vật phẩm đã được thêm vào Blind Box thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     Gửi Blind Box để chờ phê duyệt
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <returns>Trạng thái thành công (true/false)</returns>
    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<bool>> Submit(Guid id)
    {
        try
        {
            var result = await _blindBoxService.SubmitBlindBoxAsync(id);
            return Ok(ApiResult<object>.Success(result, "200", "Yêu cầu duyệt Blind Box đã được gửi thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Staff] Duyệt hoặc từ chối Blind Box (chỉ áp dụng cho trạng thái PendingApproval)
    /// </summary>
    /// <param name="id">ID của Blind Box</param>
    /// <param name="request">Yêu cầu duyệt hoặc từ chối</param>
    /// <returns>Chi tiết Blind Box sau khi xử lý</returns>
    [HttpPost("{id}/review")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<BlindBoxDetailDto>> Review(Guid id, [FromBody] BlindBoxReviewRequest request)
    {
        try
        {
            var result = await _blindBoxService.ReviewBlindBoxAsync(id, request.Approve, request.RejectReason);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200",
                request.Approve ? "Blind Box đã được phê duyệt." : "Blind Box đã bị từ chối."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Seller] Xoá toàn bộ item trong Blind Box
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <returns>Blind Box sau khi xoá hết item</returns>
    [HttpDelete("{id}/items")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BlindBoxDetailDto>> ClearItems(Guid id)
    {
        try
        {
            var result = await _blindBoxService.ClearItemsFromBlindBoxAsync(id);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200",
                "Tất cả vật phẩm trong Blind Box đã được xóa thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Seller] Xoá Blind Box (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Seller")]
    [ProducesResponseType(typeof(BlindBoxDetailDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<BlindBoxDetailDto>> Delete(Guid id)
    {
        try
        {
            var result = await _blindBoxService.DeleteBlindBoxAsync(id);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200", "Blind Box đã được xóa thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<BlindBoxDetailDto>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}