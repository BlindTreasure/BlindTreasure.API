namespace BlindTreasure.Domain.DTOs.ChatDTOs;

public class SendChatMessageRequest
{
    public Guid ReceiverId { get; set; }
    public string Content { get; set; }
}