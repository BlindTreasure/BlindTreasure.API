using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.API.Controllers;

[Route("api/chat")]
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IClaimsService _claimsService;

    public ChatController(
        IChatMessageService chatMessageService,
        IClaimsService claimsService)

    {
        _chatMessageService = chatMessageService;
        _claimsService = claimsService;
    }

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện của user hiện tại
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ConversationDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetConversations([FromQuery] PaginationParameter pagination)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            var result = await _chatMessageService.GetConversationsAsync(currentUserId, pagination);

            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Danh sách cuộc trò chuyện của bạn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy số lượng tin nhắn chưa đọc của user hiện tại
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResult<int>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetUnreadCount()
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            var count = await _chatMessageService.GetUnreadMessageCountAsync(currentUserId);
            return Ok(ApiResult<int>.Success(count, "200", "Số lượng tin nhắn chưa đọc đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<int>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy lịch sử tin nhắn giữa user hiện tại và 1 người dùng khác
    /// </summary>
    [HttpGet("history/{receiverId}")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ChatMessageDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetChatHistory(Guid receiverId, [FromQuery] PaginationParameter pagination)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            var result = await _chatMessageService.GetMessagesAsync(currentUserId, receiverId, pagination);

            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lịch sử tin nhắn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lấy danh sách cuộc trò chuyện của user hiện tại với một user khác
    /// </summary>
    [HttpGet("conversations/{receiverId}")]
    [ProducesResponseType(typeof(ApiResult<ConversationDto>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetConversationsByUserId(Guid receiverId)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            var result = await _chatMessageService.GetNewConversationByReceiverIdAsync(currentUserId, receiverId);

            return Ok(ApiResult<object>.Success(result, "200", "Danh sách cuộc trò chuyện của bạn đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Lịch sử chat với AI
    /// </summary>
    [HttpGet("history/ai")]
    [ProducesResponseType(typeof(ApiResult<Pagination<ChatMessageDto>>), 200)]
    [ProducesResponseType(typeof(ApiResult<object>), 400)]
    public async Task<IActionResult> GetChatHistoryWithAi([FromQuery] PaginationParameter pagination)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            var result = await _chatMessageService.GetMessagesAsync(currentUserId, Guid.Empty, pagination);

            return Ok(ApiResult<object>.Success(new
            {
                result,
                count = result.TotalCount,
                pageSize = result.PageSize,
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages
            }, "200", "Lịch sử chat với AI đã được tải thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Gửi tin nhắn văn bản đến một người dùng
    /// </summary>
    [HttpPost("send")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request)
    {
        try
        {
            var senderId = _claimsService.CurrentUserId;
            await _chatMessageService.SaveMessageAsync(senderId, request.ReceiverId, request.Content);
            return Ok(ApiResult.Success("200", "Tin nhắn đã được gửi thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    [HttpPost("send-image")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> SendImageMessage([FromForm] Guid receiverId, IFormFile imageFile)
    {
        try
        {
            var senderId = _claimsService.CurrentUserId;

            // Gọi service để xử lý tất cả logic
            await _chatMessageService.UploadAndSendImageMessageAsync(senderId, receiverId, imageFile);

            return Ok(ApiResult.Success("200", "Ảnh đã được gửi thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Chia sẻ InventoryItem qua chat
    /// </summary>
    /// <param name="receiverId">ID người nhận</param>
    /// <param name="inventoryItemId">ID vật phẩm</param>
    /// <param name="customMessage">Tin nhắn tùy chỉnh</param>
    /// <returns>ApiResult</returns>
    [HttpPost("share-inventory-item")]
    public async Task<IActionResult> ShareInventoryItem([FromForm] Guid receiverId,
        [FromForm] Guid inventoryItemId, [FromForm] string customMessage = "")
    {
        try
        {
            var senderId = _claimsService.CurrentUserId;
            await _chatMessageService.ShareInventoryItemAsync(senderId, receiverId, inventoryItemId, customMessage);
            return Ok(ApiResult.Success("200", "Thông tin vật phẩm đã được chia sẻ thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }


    /// <summary>
    /// Đánh dấu tất cả tin nhắn từ người gửi là đã đọc
    /// </summary>
    [HttpPost("mark-as-read/{fromUserId}")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> MarkAsRead(Guid fromUserId)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            await _chatMessageService.MarkMessagesAsReadAsync(fromUserId, currentUserId);
            return Ok(ApiResult.Success("200", "Tin nhắn đã được đánh dấu là đã đọc thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Đánh dấu tất cả tin nhắn trong một cuộc trò chuyện là đã đọc
    /// </summary>
    [HttpPost("mark-conversation-read/{otherUserId}")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> MarkConversationAsRead(Guid otherUserId)
    {
        try
        {
            var currentUserId = _claimsService.CurrentUserId;
            await _chatMessageService.MarkConversationAsReadAsync(currentUserId, otherUserId);
            return Ok(ApiResult.Success("200", "Cuộc trò chuyện đã được đánh dấu là đã đọc thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }

    /// <summary>
    /// Format kích thước file thành chuỗi dễ đọc
    /// </summary>
    /// <param name="bytes">Kích thước tính bằng bytes</param>
    /// <returns>Chuỗi định dạng (VD: 1.5 MB)</returns>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}