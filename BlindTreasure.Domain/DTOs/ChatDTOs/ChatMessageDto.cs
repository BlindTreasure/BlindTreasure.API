using BlindTreasure.Domain.DTOs.InventoryItemDTOs;
using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.DTOs.ChatDTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? SenderId { get; set; }
    public Guid? ReceiverId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderAvatar { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsCurrentUserSender { get; set; }
    public ChatMessageType MessageType { get; set; }
    
    // Thêm trường cho tin nhắn ảnh
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? FileMimeType { get; set; }
    public bool IsImage => MessageType == ChatMessageType.ImageMessage;
    
    // Thêm trường cho tin nhắn InventoryItem
    public Guid? InventoryItemId { get; set; }
    public InventoryItemDto? InventoryItem { get; set; }
    public bool IsInventoryItem => MessageType == ChatMessageType.InventoryItemMessage;
}
