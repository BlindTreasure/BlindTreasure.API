using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.DTOs;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindTreasure.Application.Cronjobs
{
    public class InventoryItemHandlingJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<InventoryItemHandlingJob> _logger;
        private readonly TimeZoneInfo _vietnamTimeZone;
        private const int TARGET_HOUR = 1; // 1h sáng
        private static readonly TimeSpan DAILY_INTERVAL = TimeSpan.FromHours(24);

        public InventoryItemHandlingJob(IServiceProvider serviceProvider, ILogger<InventoryItemHandlingJob> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Khởi tạo timezone Việt Nam
            try
            {
                _vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Fallback cho Linux/macOS
                _vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("InventoryItemHandlingJob started at {StartTime}", DateTime.UtcNow);

            try
            {
                var initialDelay = CalculateInitialDelay();

                if (initialDelay > TimeSpan.Zero)
                {
                    var nextRunVietnamTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.Add(initialDelay), _vietnamTimeZone);
                    _logger.LogInformation("InventoryItemHandlingJob scheduled to run at {NextRun} Vietnam time (delay: {DelayMinutes:F1} minutes)",
                        nextRunVietnamTime, initialDelay.TotalMinutes);

                    await Task.Delay(initialDelay, stoppingToken);
                }
                else
                {
                    _logger.LogInformation("Running InventoryItemHandlingJob immediately as scheduled time has passed");
                }

                // Chạy job lần đầu và sau đó mỗi 24h
                while (!stoppingToken.IsCancellationRequested)
                {
                    await ExecuteJobAsync();

                    _logger.LogInformation("InventoryItemHandlingJob will run again in 24 hours");
                    await Task.Delay(DAILY_INTERVAL, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("InventoryItemHandlingJob was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error in InventoryItemHandlingJob: {Message}", ex.Message);
                throw; // Re-throw để service host có thể xử lý
            }
        }

        private TimeSpan CalculateInitialDelay()
        {
            var utcNow = DateTime.UtcNow;
            var vietnamNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, _vietnamTimeZone);

            // Tính thời gian 1h sáng tiếp theo theo giờ Việt Nam
            var nextRunVietnam = vietnamNow.Date.AddHours(TARGET_HOUR);

            // Nếu đã qua 1h sáng hôm nay thì chuyển sang 1h sáng ngày mai
            if (vietnamNow.Hour >= TARGET_HOUR)
            {
                nextRunVietnam = nextRunVietnam.AddDays(1);
            }

            // Convert về UTC để tính delay
            var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunVietnam, _vietnamTimeZone);
            var delay = nextRunUtc - utcNow;

            _logger.LogDebug("Current Vietnam time: {VietnamTime}, Next run Vietnam time: {NextRunVietnam}, Delay: {Delay}",
                vietnamNow, nextRunVietnam, delay);

            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        private async Task ExecuteJobAsync()
        {
            var executionStart = DateTime.UtcNow;
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(executionStart, _vietnamTimeZone);

            _logger.LogInformation("Starting InventoryItemHandlingJob execution at {VietnamTime} Vietnam time", vietnamTime);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryItemService>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var items = await unitOfWork.InventoryItems.GetQueryable()
                    .Include(i => i.Product).ThenInclude(p => p.Seller)
                    .Include(i => i.Listings)
                     .Where(i => !i.IsDeleted && i.Status == InventoryItemStatus.Available)
                    .ToListAsync();

                foreach (var item in items)
                {
                    await inventoryService.HandleInventoryItemLifecycleAsync(item);
                }

                var executionTime = DateTime.UtcNow - executionStart;
                _logger.LogInformation("InventoryItemHandlingJob completed successfully in {ExecutionTime:F1} seconds",
                    executionTime.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing InventoryItemHandlingJob: {Message}", ex.Message);

                // Không throw exception ở đây để job tiếp tục chạy lần sau
                // Chỉ log lỗi và tiếp tục
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("InventoryItemHandlingJob is stopping...");
            await base.StopAsync(cancellationToken);
            _logger.LogInformation("InventoryItemHandlingJob stopped");
        }
    }
}