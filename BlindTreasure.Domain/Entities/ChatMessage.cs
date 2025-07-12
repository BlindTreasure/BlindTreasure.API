using BlindTreasure.Domain.Enums;

namespace BlindTreasure.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public Guid SenderId { get; set; }
    public Guid ReceiverId { get; set; }
    public string Content { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; } // Thêm dòng này
    public ChatMessageType MessageType { get; set; }
    public bool IsRead { get; set; } = false;

    // Navigation
    public User? Sender { get; set; }
    public User? Receiver { get; set; }
}