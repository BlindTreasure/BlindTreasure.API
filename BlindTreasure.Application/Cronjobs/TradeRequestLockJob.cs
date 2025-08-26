using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlindTreasure.Application.Cronjobs;

using Microsoft.Extensions.DependencyInjection;

public class TradeRequestLockJob : IHostedService, IDisposable
{
    private readonly ILogger<TradeRequestLockJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private Timer? _timer;

    public TradeRequestLockJob(ILogger<TradeRequestLockJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    // Phương thức bắt đầu cronjob
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradeRequestLockJob started at {Time}", DateTime.UtcNow);

        // Khởi động cronjob kiểm tra mỗi 2 phút
        _timer = new Timer(CheckTradeRequests!, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));

        return Task.CompletedTask;
    }

    // Phương thức kiểm tra và cập nhật các TradeRequest
    // Phương thức kiểm tra và cập nhật các TradeRequest
    private async void CheckTradeRequests(object state)
    {
        _logger.LogInformation("Cron job triggered at {Time}. Checking accepted trade requests that are not locked.",
            DateTime.UtcNow);

        // Tạo scope mới để lấy IUnitOfWork
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            try
            {
                // Tìm các trade request có status ACCEPTED nhưng chưa được lock và đã timeout
                var tradeRequests = await unitOfWork.TradeRequests.GetAllAsync(
                    t => t.Status == TradeRequestStatus.ACCEPTED &&
                         !t.LockedAt.HasValue &&
                         t.RespondedAt.HasValue,
                    t => t.Listing,
                    t => t.Listing.InventoryItem,
                    t => t.Listing.InventoryItem.Product
                );

                if (tradeRequests.Any())
                {
                    _logger.LogInformation("{Count} trade requests found with status ACCEPTED and not locked.",
                        tradeRequests.Count);
                }
                else
                {
                    _logger.LogInformation("No trade requests found with status ACCEPTED and not locked.");
                    return;
                }

                var timeoutMinutes = 10; // Thời gian timeout là 2 phút
                var updatedRequests = new List<TradeRequest>();

                foreach (var tradeRequest in tradeRequests)
                {
                    _logger.LogInformation("Checking TradeRequest {TradeRequestId}, accepted at {RespondedAt}.",
                        tradeRequest.Id, tradeRequest.RespondedAt);

                    // Tính toán thời gian đã trôi qua và thời gian còn lại
                    var timeElapsed = DateTime.UtcNow - tradeRequest.RespondedAt!.Value;
                    var remainingTime = TimeSpan.FromMinutes(timeoutMinutes) - timeElapsed;
                    var timeRemainingSeconds = remainingTime.TotalSeconds > 0 ? (int)remainingTime.TotalSeconds : 0;

                    // Luôn cập nhật TimeRemaining
                    tradeRequest.TimeRemaining = timeRemainingSeconds;

                    // Kiểm tra xem đã timeout chưa
                    if (timeElapsed.TotalMinutes > timeoutMinutes)
                    {
                        _logger.LogWarning(
                            "TradeRequest {TradeRequestId} exceeded timeout ({TimeoutMinutes} minutes since accepted), resetting to PENDING.",
                            tradeRequest.Id, timeoutMinutes);

                        // Reset trade request về PENDING do timeout
                        tradeRequest.Status = TradeRequestStatus.PENDING;
                        tradeRequest.RespondedAt = null; // Reset responded time
                        tradeRequest.OwnerLocked = false; // Reset owner lock status
                        tradeRequest.RequesterLocked = false; // Reset requester lock status
                        tradeRequest.TimeRemaining = 0; // Reset thời gian còn lại

                        // Khôi phục trạng thái listing item nếu cần
                        var listingItem = tradeRequest.Listing?.InventoryItem;
                        if (listingItem != null && listingItem.Status != InventoryItemStatus.Available)
                        {
                            _logger.LogInformation(
                                "Restoring listing item {ItemId} (Product: {ProductName}) status to Available due to timeout.",
                                listingItem.Id, listingItem.Product?.Name ?? "Unknown");

                            listingItem.Status = InventoryItemStatus.Available;
                            listingItem.LockedByRequestId = null;
                            await unitOfWork.InventoryItems.Update(listingItem);
                        }

                        _logger.LogInformation(
                            "TradeRequest {TradeRequestId} has been reset to PENDING due to timeout.",
                            tradeRequest.Id);
                    }
                    else
                    {
                        var minutesLeft = Math.Round(timeoutMinutes - timeElapsed.TotalMinutes, 1);
                        _logger.LogInformation(
                            "TradeRequest {TradeRequestId} is still within the allowed time frame. {MinutesLeft} minutes ({SecondsLeft} seconds) left.",
                            tradeRequest.Id, minutesLeft, timeRemainingSeconds);
                    }

                    // Thêm vào danh sách để cập nhật
                    updatedRequests.Add(tradeRequest);
                }

                // Cập nhật tất cả trade requests trong một lần
                if (updatedRequests.Any())
                {
                    await unitOfWork.TradeRequests.UpdateRange(updatedRequests);
                    await unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("Updated {Count} trade requests with new TimeRemaining values.",
                        updatedRequests.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking trade requests: {ErrorMessage}", ex.Message);
            }
        }
    }

    // Phương thức dừng cronjob
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradeRequestLockJob is stopping at {Time}", DateTime.UtcNow);
        _timer.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    // Phương thức giải phóng tài nguyên
    public void Dispose()
    {
        _logger.LogInformation("Disposing TradeRequestLockJob resources.");
        _timer.Dispose();
    }
}