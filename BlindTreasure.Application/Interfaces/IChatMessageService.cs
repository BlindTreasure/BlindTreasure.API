using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;

namespace BlindTreasure.Application.Interfaces;

public interface IChatMessageService
{
    // Existing methods
    Task SaveMessageAsync(Guid senderId, Guid receiverId, string content);
    Task<List<ChatMessageDto>> GetMessagesAsync(Guid user1Id, Guid user2Id, int pageIndex, int pageSize);
    Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId);
    Task SaveAiMessageAsync(Guid customerId, string content);

    // Missing methods cần thêm
    Task SetUserOnline(string userId);
    Task<bool> IsUserOnline(string userId);
    Task SetUserOffline(string userId);
    Task<List<ConversationDto>> GetConversationsAsync(Guid userId, int pageIndex = 0, int pageSize = 20);
    Task<int> GetUnreadMessageCountAsync(Guid userId);
    Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId);
}