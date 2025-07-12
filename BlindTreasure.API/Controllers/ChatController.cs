using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;
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
    ///     Lấy lịch sử tin nhắn giữa user hiện tại và 1 người dùng khác
    /// </summary>
    [HttpGet("history/{receiverId}")]
    public async Task<IActionResult> GetChatHistory(Guid receiverId, [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 20)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var messages = await _chatMessageService.GetMessagesAsync(currentUserId, receiverId, pageIndex, pageSize);
        return Ok(ApiResult<List<ChatMessageDto>>.Success(messages));
    }

    /// <summary>
    /// Lịch sử chat với AI
    /// </summary>
    [HttpGet("history/ai")]
    public async Task<IActionResult> GetChatHistoryWithAi([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 20)
    {
        var currentUserId = _claimsService.CurrentUserId;
        var messages = await _chatMessageService.GetMessagesAsync(currentUserId, Guid.Empty, pageIndex, pageSize);
        return Ok(ApiResult<List<ChatMessageDto>>.Success(messages));
    }

    /// <summary>
    ///     Gửi tin nhắn đến một người dùng
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request)
    {
        var senderId = _claimsService.CurrentUserId;
        await _chatMessageService.SaveMessageAsync(senderId, request.ReceiverId, request.Content);
        return Ok(ApiResult.Success("200", "Gửi tin nhắn thành công"));
    }

    /// <summary>
    /// Đánh dấu tất cả tin nhắn từ người gửi là đã đọc
    /// </summary>
    [HttpPost("mark-as-read/{fromUserId}")]
    public async Task<IActionResult> MarkAsRead(Guid fromUserId)
    {
        var currentUserId = _claimsService.CurrentUserId;
        await _chatMessageService.MarkMessagesAsReadAsync(fromUserId, currentUserId);
        return Ok(ApiResult.Success("200", "Đánh dấu đã đọc thành công"));
    }
}