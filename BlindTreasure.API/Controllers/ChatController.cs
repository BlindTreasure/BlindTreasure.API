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
    private readonly IBlobService _blobService;
    private readonly IUserService _userService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IUnitOfWork _unitOfWork;

    public ChatController(
        IChatMessageService chatMessageService,
        IClaimsService claimsService,
        IBlobService blobService,
        IUserService userService,
        IHubContext<ChatHub> hubContext,
        IUnitOfWork unitOfWork)
    {
        _chatMessageService = chatMessageService;
        _claimsService = claimsService;
        _blobService = blobService;
        _userService = userService;
        _hubContext = hubContext;
        _unitOfWork = unitOfWork;
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
    /// Upload ảnh và gửi tin nhắn ảnh qua chat
    /// </summary>
    /// <param name="receiverId">ID người nhận</param>
    /// <param name="imageFile">File ảnh</param>
    /// <returns>ApiResult</returns>
    [HttpPost("send-image")]
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> SendImageMessage([FromForm] Guid receiverId, IFormFile imageFile)
    {
        try
        {
            var senderId = _claimsService.CurrentUserId;

            // Validate file
            if (imageFile == null || imageFile.Length == 0)
                return BadRequest(ApiResult.Failure("400", "Vui lòng chọn file ảnh."));

            // Kiểm tra định dạng file
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
                return BadRequest(ApiResult.Failure("400",
                    "Định dạng file không được hỗ trợ. Chỉ chấp nhận: jpg, jpeg, png, gif, webp"));

            // Kiểm tra kích thước file (max 10MB)
            const long maxFileSize = 10 * 1024 * 1024; // 10MB
            if (imageFile.Length > maxFileSize)
                return BadRequest(ApiResult.Failure("400", "File quá lớn. Kích thước tối đa: 10MB"));

            // Kiểm tra receiver có tồn tại không
            var receiver = await _userService.GetUserById(receiverId);
            if (receiver == null || receiver.IsDeleted)
                return NotFound(ApiResult.Failure("404", "Người nhận không tồn tại."));

            // Tạo tên file duy nhất
            var uniqueFileName = $"chat/{senderId}/{Guid.NewGuid()}{fileExtension}";

            // Upload ảnh lên MinIO
            using var stream = imageFile.OpenReadStream();
            await _blobService.UploadFileAsync(uniqueFileName, stream);

            // Lấy URL của ảnh
            var imageUrl = await _blobService.GetPreviewUrlAsync(uniqueFileName);

            // Tính kích thước file
            var fileSizeStr = FormatFileSize(imageFile.Length);

            // Lưu tin nhắn ảnh vào database
            await _chatMessageService.SaveImageMessageAsync(senderId, receiverId,
                imageUrl, imageFile.FileName, fileSizeStr, imageFile.ContentType);

            // Lấy thông tin người gửi
            var sender = await _userService.GetUserById(senderId);

            // Tạo message object để gửi qua SignalR
            var messageData = new
            {
                id = Guid.NewGuid().ToString(),
                senderId = senderId.ToString(),
                receiverId = receiverId.ToString(),
                senderName = sender?.FullName ?? "Unknown",
                senderAvatar = sender?.AvatarUrl ?? "",
                content = "[Hình ảnh]",
                imageUrl,
                fileName = imageFile.FileName,
                fileSize = fileSizeStr,
                mimeType = imageFile.ContentType,
                messageType = ChatMessageType.ImageMessage.ToString(),
                timestamp = DateTime.UtcNow,
                isRead = false
            };

            // Gửi qua SignalR cho cả sender và receiver
            await _hubContext.Clients.Users(new[] { senderId.ToString(), receiverId.ToString() })
                .SendAsync("ReceiveImageMessage", messageData);

            // Cập nhật số tin chưa đọc cho receiver
            var unreadCount = await _chatMessageService.GetUnreadMessageCountAsync(receiverId);
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);

            return Ok(ApiResult.Success("200", "Gửi ảnh thành công."));
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
    [ProducesResponseType(typeof(ApiResult), 200)]
    [ProducesResponseType(typeof(ApiResult), 400)]
    public async Task<IActionResult> ShareInventoryItem([FromForm] Guid receiverId,
        [FromForm] Guid inventoryItemId, [FromForm] string customMessage = "")
    {
        try
        {
            var senderId = _claimsService.CurrentUserId;

            // Kiểm tra receiver có tồn tại không
            var receiver = await _userService.GetUserById(receiverId);
            if (receiver == null || receiver.IsDeleted)
                return NotFound(ApiResult.Failure("404", "Người nhận không tồn tại."));

            // Kiểm tra InventoryItem có tồn tại và thuộc về sender không
            var inventoryItem = await _unitOfWork.InventoryItems
                .GetQueryable()
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == inventoryItemId);

            if (inventoryItem == null)
                return NotFound(ApiResult.Failure("404", "Vật phẩm không tồn tại."));

            if (inventoryItem.UserId != senderId)
                return Forbid(ApiResult.Failure("403", "Bạn không có quyền chia sẻ vật phẩm này.").ToString());

            // Lưu tin nhắn chia sẻ InventoryItem
            await _chatMessageService.SaveInventoryItemMessageAsync(senderId, receiverId, inventoryItemId,
                customMessage);

            // Lấy thông tin người gửi
            var sender = await _userService.GetUserById(senderId);

            // Chuẩn bị dữ liệu InventoryItem để gửi cho client
            var itemDto = new
            {
                id = inventoryItem.Id,
                productName = inventoryItem.Product?.Name ?? "Không xác định",
                productImage = inventoryItem.Product?.ImageUrls.FirstOrDefault(),
                tier = inventoryItem.Tier?.ToString() ?? "Không xác định",
                status = inventoryItem.Status.ToString(),
                location = inventoryItem.Location
            };

            // Nội dung hiển thị
            var content = string.IsNullOrEmpty(customMessage)
                ? $"[Chia sẻ vật phẩm: {inventoryItem.Product?.Name ?? "Không xác định"}]"
                : customMessage;

            // Tạo message object để gửi qua SignalR
            var messageData = new
            {
                id = Guid.NewGuid().ToString(),
                senderId = senderId.ToString(),
                receiverId = receiverId.ToString(),
                senderName = sender?.FullName ?? "Unknown",
                senderAvatar = sender?.AvatarUrl ?? "",
                content,
                inventoryItemId = inventoryItem.Id,
                inventoryItem = itemDto,
                messageType = ChatMessageType.InventoryItemMessage.ToString(),
                timestamp = DateTime.UtcNow,
                isRead = false
            };

            // Gửi qua SignalR cho cả sender và receiver
            await _hubContext.Clients.Users(new[] { senderId.ToString(), receiverId.ToString() })
                .SendAsync("ReceiveInventoryItemMessage", messageData);

            // Cập nhật số tin chưa đọc cho receiver
            var unreadCount = await _chatMessageService.GetUnreadMessageCountAsync(receiverId);
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("UnreadCountUpdated", unreadCount);

            return Ok(ApiResult.Success("200", "Chia sẻ vật phẩm thành công."));
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