namespace BlindTreasure.Domain.DTOs.ChatDTOs;

public class ConversationDto
{
    public Guid OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string OtherUserAvatar { get; set; } = string.Empty; // Thêm trường này
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
}