using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly IClaimsService _claimsService;
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService, IClaimsService claimsService)
    {
        _notificationService = notificationService;
        _claimsService = claimsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10,
        NotificationType? type = null)
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var items = await _notificationService.GetNotificationsAsync(userId, pageIndex, pageSize, type);
            var totalCount = await _notificationService.CountNotificationsAsync(userId);

            return Ok(ApiResult<object>.Success(new
            {
                totalCount,
                pageIndex,
                pageSize,
                items
            }));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            var count = await _notificationService.GetUnreadNotificationsCount(userId);
            return Ok(ApiResult<int>.Success(count));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }


    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll()
    {
        try
        {
            var userId = _claimsService.CurrentUserId;
            await _notificationService.ReadAllNotifications(userId);
            return Ok(ApiResult.Success("200", "Đã đọc tất cả thông báo"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        try
        {
            var result = await _notificationService.ReadNotification(id);
            return Ok(ApiResult<object>.Success(result));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _notificationService.DeleteNotification(id);
            return Ok(ApiResult.Success("200", "Xóa thông báo thành công"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }
}