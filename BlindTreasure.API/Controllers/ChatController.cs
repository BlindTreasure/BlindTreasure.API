using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Infrastructure.Commons;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlindTreasure.API.Controllers;

[Route("api/chat")]
[ApiController]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IClaimsService _claimsService;

    public ChatController(IChatMessageService chatMessageService, IClaimsService claimsService)
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
            }, "200", "Lấy danh sách cuộc trò chuyện thành công."));
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
            return Ok(ApiResult<int>.Success(count, "200", "Lấy số tin nhắn chưa đọc thành công."));
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
            }, "200", "Lấy lịch sử tin nhắn thành công."));
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
            }, "200", "Lấy lịch sử chat với AI thành công."));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var errorResponse = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, errorResponse);
        }
    }

    /// <summary>
    /// Gửi tin nhắn đến một người dùng
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
            return Ok(ApiResult.Success("200", "Gửi tin nhắn thành công"));
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
            return Ok(ApiResult.Success("200", "Đánh dấu đã đọc thành công"));
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
            return Ok(ApiResult.Success("200", "Đánh dấu cuộc trò chuyện đã đọc thành công"));
        }
        catch (Exception ex)
        {
            var statusCode = ExceptionUtils.ExtractStatusCode(ex);
            var error = ExceptionUtils.CreateErrorResponse<object>(ex);
            return StatusCode(statusCode, error);
        }
    }
}