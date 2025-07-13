using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Application.Utils;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHubContext<ChatHub> _hubContext;


    public ChatMessageService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger,
        IUnitOfWork unitOfWork, IHubContext<ChatHub> hubContext)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _hubContext = hubContext;
    }

    public async Task SaveAiMessageAsync(Guid userId, string content)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || user.IsDeleted)
            throw ErrorHelper.NotFound("Người nhận không tồn tại.");

        var message = new ChatMessage
        {
            SenderId = null,
            SenderType = ChatParticipantType.AI,
            ReceiverId = userId,
            ReceiverType = ChatParticipantType.User,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = ChatMessageType.AiToUser
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
    }


    public async Task SaveMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        var message = new ChatMessage
        {
            SenderId = senderId,
            SenderType = ChatParticipantType.User,
            ReceiverId = receiverId == Guid.Empty ? null : receiverId,
            ReceiverType = receiverId == Guid.Empty ? ChatParticipantType.AI : ChatParticipantType.User,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false,
            MessageType = receiverId == Guid.Empty ? ChatMessageType.UserToAi : ChatMessageType.UserToUser
        };


        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        var previewKey = GetLastMessageCacheKey(senderId, receiverId);
        await _cacheService.SetAsync(previewKey, message, TimeSpan.FromHours(1));

        _logger.Info($"[Chat] {senderId} → {receiverId}: {content}");
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid currentUserId, Guid targetId, int pageIndex, int pageSize)
    {
        IQueryable<ChatMessage> query;

        if (targetId == Guid.Empty)
        {
            // Chat giữa User và AI
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderType == ChatParticipantType.User && m.SenderId == currentUserId && m.ReceiverType == ChatParticipantType.AI)
                    ||
                    (m.SenderType == ChatParticipantType.AI && m.ReceiverType == ChatParticipantType.User && m.ReceiverId == currentUserId)
                );
        }
        else
        {
            // Chat giữa 2 người dùng
            query = _unitOfWork.ChatMessages.GetQueryable()
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == targetId && m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                    ||
                    (m.SenderId == targetId && m.ReceiverId == currentUserId && m.SenderType == ChatParticipantType.User && m.ReceiverType == ChatParticipantType.User)
                );
        }

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            SenderName = m.SenderType == ChatParticipantType.AI
                ? "BlindTreasure AI"
                : m.Sender?.FullName ?? "Unknown",
            Content = m.Content,
            SentAt = m.SentAt,
            IsRead = m.IsRead
        }).ToList();
    }



    public async Task MarkMessagesAsReadAsync(Guid fromUserId, Guid toUserId)
    {
        var unreadMessages = await _unitOfWork.ChatMessages.GetQueryable()
            .Where(m => m.SenderId == fromUserId &&
                        m.ReceiverId == toUserId &&
                        !m.IsRead)
            .ToListAsync();

        if (!unreadMessages.Any()) return;

        var now = DateTime.UtcNow;
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = now;
        }

        await _unitOfWork.ChatMessages.UpdateRange(unreadMessages);
        await _unitOfWork.SaveChangesAsync();

        // Gửi sự kiện SignalR cho sender
        await _hubContext.Clients.User(fromUserId.ToString()).SendAsync("MessageReadConfirmed", new
        {
            readerId = toUserId,
            messages = unreadMessages.Select(m => new
            {
                m.Id,
                m.ReadAt
            }).ToList()
        });
    }


    private static string GetLastMessageCacheKey(Guid user1Id, Guid user2Id)
    {
        var ids = new[] { user1Id, user2Id }.OrderBy(x => x).ToList();
        return $"chat:last:{ids[0]}:{ids[1]}";
    }
}