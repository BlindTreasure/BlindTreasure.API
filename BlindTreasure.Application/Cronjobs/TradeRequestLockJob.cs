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
    private async void CheckTradeRequests(object state)
    {
        _logger.LogInformation("Cron job triggered at {Time}. Checking accepted trade requests that are not locked.",
            DateTime.UtcNow);

        // Tạo scope mới để lấy IUnitOfWork
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            // Tìm các trade request có status ACCEPTED nhưng chưa được lock và đã timeout
            var tradeRequests = await unitOfWork.TradeRequests.GetAllAsync(
                t => t.Status == TradeRequestStatus.ACCEPTED &&
                     !t.LockedAt.HasValue &&
                     t.RespondedAt.HasValue,
                t => t.Listing,
                t => t.Listing.InventoryItem
            );

            if (tradeRequests.Any())
            {
                _logger.LogInformation("{Count} trade requests found with status ACCEPTED and not locked.",
                    tradeRequests.Count());
            }
            else
            {
                _logger.LogInformation("No trade requests found with status ACCEPTED and not locked.");
                return;
            }

            foreach (var tradeRequest in tradeRequests)
            {
                _logger.LogInformation("Checking TradeRequest {TradeRequestId}, accepted at {RespondedAt}.",
                    tradeRequest.Id, tradeRequest.RespondedAt);

                // Kiểm tra xem thời gian từ khi được accept đã vượt quá 2 phút chưa
                var timeElapsed = DateTime.UtcNow - tradeRequest.RespondedAt!.Value;
                if (timeElapsed.TotalMinutes > 2)
                {
                    _logger.LogWarning(
                        "TradeRequest {TradeRequestId} exceeded timeout (2 minutes since accepted), resetting to PENDING.",
                        tradeRequest.Id);

                    // Reset về PENDING do timeout
                    tradeRequest.Status = TradeRequestStatus.PENDING;
                    tradeRequest.RespondedAt = null; // Reset responded time
                    tradeRequest.OwnerLocked = false; // Reset lock status
                    tradeRequest.RequesterLocked = false; // Reset lock status

                    // Khôi phục trạng thái listing item nếu cần
                    if (true)
                    {
                        var listingItem = tradeRequest.Listing.InventoryItem;
                        if (listingItem.Status != InventoryItemStatus.Available)
                        {
                            _logger.LogInformation(
                                "Restoring listing item {ItemId} status to Available due to timeout.",
                                listingItem.Id);
                            listingItem.Status = InventoryItemStatus.Available;
                            listingItem.LockedByRequestId = null;
                            await unitOfWork.InventoryItems.Update(listingItem);
                        }
                    }

                    await unitOfWork.TradeRequests.Update(tradeRequest);
                    await unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("TradeRequest {TradeRequestId} has been reset to PENDING due to timeout.",
                        tradeRequest.Id);
                }
                else
                {
                    _logger.LogInformation(
                        "TradeRequest {TradeRequestId} is still within the allowed time frame. {MinutesLeft} minutes left.",
                        tradeRequest.Id, Math.Round(2 - timeElapsed.TotalMinutes, 1));
                }
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