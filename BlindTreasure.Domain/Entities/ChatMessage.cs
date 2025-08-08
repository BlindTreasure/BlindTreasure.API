using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid? SenderId { get; set; }
    public ChatParticipantType SenderType { get; set; }

    public Guid? ReceiverId { get; set; }
    public ChatParticipantType ReceiverType { get; set; }

    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; } = false;

    public ChatMessageType MessageType { get; set; }
    
    // Thêm trường mới cho tin nhắn ảnh
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? FileSize { get; set; }
    public string? FileMimeType { get; set; }
    
    // Thêm trường mới cho tin nhắn chia sẻ InventoryItem
    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    public User? Sender { get; set; }
    public User? Receiver { get; set; }
}