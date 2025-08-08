using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface IChatMessageService
{
    // Existing methods
    Task SaveMessageAsync(Guid senderId, Guid receiverId, string content);
    Task<Pagination<ChatMessageDto>> GetMessagesAsync(Guid user1Id, Guid user2Id, PaginationParameter pagination);
    Task<Pagination<ConversationDto>> GetConversationsAsync(Guid userId, PaginationParameter pagination);
    Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId);
    Task SaveAiMessageAsync(Guid customerId, string content);
    Task MarkConversationAsReadAsync(Guid currentUserId, Guid otherUserId);
    Task SetUserOnline(string userId);
    Task<bool> IsUserOnline(string userId);
    Task SetUserOffline(string userId);
    Task<int> GetUnreadMessageCountAsync(Guid userId);
    Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId);
    
    Task SaveImageMessageAsync(Guid senderId, Guid receiverId, 
        string imageUrl, string fileName, string fileSize, string mimeType);
        
    Task SaveInventoryItemMessageAsync(Guid senderId, Guid receiverId, 
        Guid inventoryItemId, string customMessage = "");
}