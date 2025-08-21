using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.DTOs.Pagination;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Commons;
using Microsoft.AspNetCore.Http;

namespace BlindTreasure.Application.Interfaces;

public interface IChatMessageService
{
    // Quản lý trạng thái người dùng
    Task SetUserOnline(string userId);
    Task<bool> IsUserOnline(string userId);
    Task SetUserOffline(string userId);
    Task<string> UploadAndSendImageMessageAsync(Guid senderId, Guid receiverId, IFormFile imageFile);

    // Lưu trữ tin nhắn
    Task SaveMessageAsync(Guid senderId, Guid receiverId, string content);
    Task SaveAiMessageAsync(Guid customerId, string content);

    Task ShareInventoryItemAsync(Guid senderId, Guid receiverId, Guid inventoryItemId,
        string customMessage = "");

    // Lấy tin nhắn và hội thoại
    Task<ChatMessageDto?> GetMessageByIdAsync(Guid messageId);
    Task<Pagination<ChatMessageDto>> GetMessagesAsync(Guid user1Id, Guid user2Id, PaginationParameter pagination);
    Task<Pagination<ConversationDto>> GetConversationsAsync(Guid userId, PaginationParameter pagination);
    Task<ConversationDto> GetNewConversationByReceiverIdAsync(Guid currentUserId, Guid receiverId);

    // Đọc tin nhắn và hội thoại
    Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId);
    Task MarkConversationAsReadAsync(Guid currentUserId, Guid otherUserId);
    Task<int> GetUnreadMessageCountAsync(Guid userId);
}