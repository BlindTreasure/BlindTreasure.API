using BlindTreasure.Domain.DTOs.TradeHistoryDTOs;
using BlindTreasure.Domain.DTOs.TradeRequestDTOs;
using BlindTreasure.Infrastructure.Commons;

namespace BlindTreasure.Application.Interfaces;

public interface ITradingService
{
    /// <summary>
    /// Lấy danh sách các yêu cầu trao đổi cho một listing cụ thể
    /// </summary>
    Task<List<TradeRequestDto>> GetTradeRequestsAsync(Guid listingId);

    /// <summary>
    /// Tạo yêu cầu trao đổi mới cho một listing
    /// </summary>
    Task<TradeRequestDto> CreateTradeRequestAsync(Guid listingId, CreateTradeRequestDto request);

    /// <summary>
    /// Phản hồi yêu cầu trao đổi (chấp nhận hoặc từ chối)
    /// </summary>
    Task<TradeRequestDto> RespondTradeRequestAsync(Guid tradeRequestId, bool isAccepted);

    /// <summary>
    /// Lấy tất cả lịch sử giao dịch với phân trang và bộ lọc (dành cho admin)
    /// </summary>
    Task<Pagination<TradeHistoryDto>> GetAllTradeHistoriesAsync(TradeHistoryQueryParameter param);

    /// <summary>
    /// Khóa giao dịch khi cả hai bên đồng ý hoàn tất
    /// </summary>
    Task<TradeRequestDto> LockDealAsync(Guid tradeRequestId);

    /// <summary>
    /// Giải phóng các vật phẩm đã hết thời gian giữ và gửi thông báo
    /// </summary>
    Task ReleaseHeldItemsAsync();

    /// <summary>
    /// Lấy danh sách yêu cầu trao đổi của người dùng hiện tại
    /// </summary>
    Task<List<TradeRequestDto>> GetMyTradeRequestsAsync();

    /// <summary>
    /// Lấy lịch sử giao dịch của người dùng hiện tại với phân trang và bộ lọc
    /// </summary>
    Task<Pagination<TradeHistoryDto>> GetMyTradeHistoriesAsync(TradeHistoryQueryParameter param);
}