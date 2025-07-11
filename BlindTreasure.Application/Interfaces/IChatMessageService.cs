using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IChatMessageService
{
    Task SaveMessageAsync(Guid senderId, Guid receiverId, string content);
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid user1Id, Guid user2Id, int pageIndex, int pageSize);
    Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId);
}