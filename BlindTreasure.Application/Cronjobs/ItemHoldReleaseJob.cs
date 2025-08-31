using BlindTreasure.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlindTreasure.Application.Cronjobs;

public class ItemHoldReleaseJob : BackgroundService
{
    private readonly ILogger<ItemHoldReleaseJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ItemHoldReleaseJob(
        IServiceProvider serviceProvider,
        ILogger<ItemHoldReleaseJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Item Hold Release Job đang chạy.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var tradingService = scope.ServiceProvider.GetRequiredService<ITradingService>();
                    await tradingService.ReleaseHeldItemsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy ItemHoldReleaseJob");
            }

            // Chờ 1 giờ trước khi chạy lại
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}