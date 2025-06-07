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
            return Ok(ApiResult<Pagination<BlindBoxDetailDto>>.Success(result, "200",
                "Lấy danh sách Blind Box thành công."));
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
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200", "Lấy thông tin Blind Box thành công."));
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
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "201", "Tạo Blind Box thành công."));
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
    public async Task<ActionResult<BlindBoxDetailDto>> AddItems(Guid id, [FromBody] List<BlindBoxItemDto> items)
    {
        try
        {
            var result = await _blindBoxService.AddItemsToBlindBoxAsync(id, items);
            return Ok(ApiResult<BlindBoxDetailDto>.Success(result, "200", "Thêm item vào Blind Box thành công."));
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
            return Ok(ApiResult<object>.Success(result!, "200", "Gửi duyệt Blind Box thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Staff] Lấy danh sách Blind Box đang ở trạng thái chờ duyệt (PendingApproval)
    /// </summary>
    /// <returns>Danh sách Blind Box kèm item và tỷ lệ</returns>
    [HttpGet("pending-approval")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(List<BlindBoxDetailDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<List<BlindBoxDetailDto>>> GetPendingApproval()
    {
        try
        {
            var result = await _blindBoxService.GetPendingApprovalBlindBoxesAsync();
            return Ok(ApiResult<List<BlindBoxDetailDto>>.Success(result, "200",
                "Lấy danh sách Blind Box chờ duyệt thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<List<BlindBoxDetailDto>>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Staff] Phê duyệt Blind Box sau khi xác minh tỉ lệ drop-rate hợp lệ
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <returns>Kết quả thành công (true/false)</returns>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<bool>> Approve(Guid id)
    {
        try
        {
            var result = await _blindBoxService.ApproveBlindBoxAsync(id);
            return Ok(ApiResult<object>.Success(result!, "200", "Phê duyệt Blind Box thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    ///     [Staff] Từ chối Blind Box và ghi lý do từ chối
    /// </summary>
    /// <param name="id">Id của Blind Box</param>
    /// <param name="reason">Lý do từ chối</param>
    /// <returns>Kết quả thành công (true/false)</returns>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Staff")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<bool>> Reject(Guid id, [FromBody] string reason)
    {
        try
        {
            var result = await _blindBoxService.RejectBlindBoxAsync(id, reason);
            return Ok(ApiResult<object>.Success(result!, "200", "Từ chối Blind Box thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<bool>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}