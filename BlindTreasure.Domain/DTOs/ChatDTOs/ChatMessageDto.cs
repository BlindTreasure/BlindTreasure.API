namespace BlindTreasure.Domain.DTOs.ChatDTOs;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public Guid? SenderId { get; set; }
    public Guid? ReceiverId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderAvatar { get; set; } = string.Empty; // Thêm avatar người gửi
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
    public bool IsCurrentUserSender { get; set; } // Thêm trường này
}