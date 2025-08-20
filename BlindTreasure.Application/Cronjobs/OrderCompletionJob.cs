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

                // Get all orders that are not completed/cancelled/expired
                var orders = await unitOfWork.Orders.GetQueryable()
                    .Where(o => o.Status == OrderStatus.PAID.ToString() || o.Status == OrderStatus.PENDING.ToString())
                    .Include(o => o.OrderDetails)
                    .ToListAsync();

                foreach (var order in orders)
                {
                    if (order.OrderDetails == null || !order.OrderDetails.Any())
                        continue;

                    var allDelivered = order.OrderDetails.All(od => od.Status == OrderDetailItemStatus.DELIVERED);
                    var allInInventory3Days = order.OrderDetails.All(od =>
                        od.Status == OrderDetailItemStatus.IN_INVENTORY &&
                        od.UpdatedAt.HasValue &&
                        (DateTime.UtcNow - od.UpdatedAt.Value).TotalDays >= 3);

                    if (allDelivered || allInInventory3Days)
                    {
                        order.Status = OrderStatus.COMPLETED.ToString();
                        order.CompletedAt = DateTime.UtcNow;
                        await unitOfWork.Orders.Update(order);
                        _logger.LogInformation("Order {OrderId} marked as COMPLETED.", order.Id);
                    }
                }

                await unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderCompletionJob: {Message}", ex.Message);
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}