using BlindTreasure.Application.Interfaces;
using BlindTreasure.Domain.Entities;
using BlindTreasure.Domain.Enums;
using BlindTreasure.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlindTreasure.Application.Cronjobs;

public class OrderCompletionJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderCompletionJob> _logger;

    public OrderCompletionJob(IServiceProvider serviceProvider, ILogger<OrderCompletionJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderCompletionJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var adminService = scope.ServiceProvider.GetRequiredService<IAdminService>();

                // Get all orders that are not completed/cancelled/expired
                var orders = await unitOfWork.Orders.GetQueryable()
                    .Where(o => o.Status == OrderStatus.PAID.ToString() || o.Status == OrderStatus.PENDING.ToString())
                    .Include(o => o.OrderDetails)
                    .ToListAsync();

                var completedCount = 0;
                foreach (var order in orders)
                    if (await adminService.TryCompleteOrderAsync(order, stoppingToken))
                    {
                        completedCount++;
                        _logger.LogInformation("Order {OrderId} marked as COMPLETED by cronjob.", order.Id);
                    }

                _logger.LogInformation("OrderCompletionJob completed. {Count} changed orders updated.", completedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderCompletionJob: {Message}", ex.Message);
            }

            // Run every 1 minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}