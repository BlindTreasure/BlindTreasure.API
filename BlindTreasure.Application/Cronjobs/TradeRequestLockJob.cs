using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlindTreasure.Application.Cronjobs;
using Microsoft.Extensions.DependencyInjection;

public class TradeRequestLockJob : IHostedService, IDisposable
{
    private readonly ILogger<TradeRequestLockJob> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;  // Thêm IServiceScopeFactory
    private Timer _timer;

    public TradeRequestLockJob(ILogger<TradeRequestLockJob> logger, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;  // Tiêm IServiceScopeFactory
    }

    // Phương thức bắt đầu cronjob
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradeRequestLockJob started at {Time}", DateTime.UtcNow);

        // Khởi động cronjob kiểm tra mỗi 2 phút
        _timer = new Timer(CheckTradeRequests, null, TimeSpan.Zero, TimeSpan.FromMinutes(2));

        return Task.CompletedTask;
    }

    // Phương thức kiểm tra và cập nhật các TradeRequest
    private async void CheckTradeRequests(object state)
    {
        _logger.LogInformation("Cron job triggered at {Time}. Checking pending trade requests.", DateTime.UtcNow);

        // Tạo scope mới để lấy IUnitOfWork
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();  // Tiêm IUnitOfWork từ scope mới

            var tradeRequests = await unitOfWork.TradeRequests.GetAllAsync(t => t.Status == TradeRequestStatus.PENDING && !t.LockedAt.HasValue);

            if (tradeRequests.Any())
            {
                _logger.LogInformation("{Count} trade requests found with status PENDING and not locked.", tradeRequests.Count());
            }
            else
            {
                _logger.LogInformation("No trade requests found with status PENDING and not locked.");
            }

            foreach (var tradeRequest in tradeRequests)
            {
                _logger.LogInformation("Checking TradeRequest {TradeRequestId}, requested at {RequestedAt}.", tradeRequest.Id, tradeRequest.RequestedAt);

                // Kiểm tra xem thời gian yêu cầu đã vượt quá 2 phút chưa
                if ((DateTime.UtcNow - tradeRequest.RequestedAt).TotalMinutes > 2)
                {
                    _logger.LogWarning("TradeRequest {TradeRequestId} exceeded timeout (2 minutes), resetting to PENDING.", tradeRequest.Id);

                    // Nếu vượt quá 2 phút, cập nhật trạng thái của yêu cầu giao dịch thành PENDING
                    tradeRequest.Status = TradeRequestStatus.PENDING;

                    unitOfWork.TradeRequests.Update(tradeRequest);
                    await unitOfWork.SaveChangesAsync();

                    _logger.LogInformation("TradeRequest {TradeRequestId} has been reset to PENDING due to timeout.", tradeRequest.Id);
                }
                else
                {
                    _logger.LogInformation("TradeRequest {TradeRequestId} is still within the allowed time frame.", tradeRequest.Id);
                }
            }
        }
    }

    // Phương thức dừng cronjob
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TradeRequestLockJob is stopping at {Time}", DateTime.UtcNow);
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    // Phương thức giải phóng tài nguyên
    public void Dispose()
    {
        _logger.LogInformation("Disposing TradeRequestLockJob resources.");
        _timer?.Dispose();
    }
}
