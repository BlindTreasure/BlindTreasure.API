using BlindTreasure.Application.Interfaces;
using BlindTreasure.Application.Interfaces.Commons;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlindTreasure.Application.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly ICacheService _cacheService;
    private readonly IClaimsService _claimsService;
    private readonly ILoggerService _logger;
    private readonly IMapperService _mapper;
    private readonly IUnitOfWork _unitOfWork;


    public ChatMessageService(ICacheService cacheService, IClaimsService claimsService, ILoggerService logger,
        IMapperService mapper, IUnitOfWork unitOfWork)
    {
        _cacheService = cacheService;
        _claimsService = claimsService;
        _logger = logger;
        _mapper = mapper;
        _unitOfWork = unitOfWork;
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

    public async Task<List<ChatMessage>> GetMessagesAsync(Guid user1Id, Guid user2Id, int pageIndex, int pageSize)
    {
        var query = _unitOfWork.ChatMessages.GetQueryable()
            .Where(m =>
                (m.SenderId == user1Id && m.ReceiverId == user2Id) ||
                (m.SenderId == user2Id && m.ReceiverId == user1Id))
            .OrderByDescending(m => m.SentAt)
            .Skip(pageIndex * pageSize)
            .Take(pageSize);

        return await query.ToListAsync();
    }

    private static string GetLastMessageCacheKey(Guid user1Id, Guid user2Id)
    {
        var ids = new[] { user1Id, user2Id }.OrderBy(x => x).ToList();
        return $"chat:last:{ids[0]}:{ids[1]}";
    }
}