namespace BlindTreasure.Domain.DTOs.ChatDTOs;

public class ConversationDto
{
    public Guid OtherUserId { get; set; }
    public string OtherUserName { get; set; }
    public string OtherUserAvatar { get; set; } // Nếu có
    public string LastMessage { get; set; }
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
    public bool IsOnline { get; set; }
}