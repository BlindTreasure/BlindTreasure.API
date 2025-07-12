using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Application.SignalR.Hubs;
using BlindTreasure.Domain.DTOs.ChatDTOs;
using BlindTreasure.Domain.Entities;
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

    public async Task SaveMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        var message = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        // Cache preview
        var previewKey = GetLastMessageCacheKey(senderId, receiverId);
        await _cacheService.SetAsync(previewKey, message, TimeSpan.FromHours(1));

        _logger.Info($"[Chat] {senderId} → {receiverId}: {content}");
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(Guid user1Id, Guid user2Id, int pageIndex, int pageSize)
    {
        var query = _unitOfWork.ChatMessages.GetQueryable()
            .Where(m =>
                (m.SenderId == user1Id && m.ReceiverId == user2Id) ||
                (m.SenderId == user2Id && m.ReceiverId == user1Id))
            .OrderByDescending(m => m.SentAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize);

        var messages = await query.ToListAsync();

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            SenderId = m.SenderId,
            ReceiverId = m.ReceiverId,
            SenderName = m.Sender?.FullName ?? "Unknown",
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