using BlindTreasure.Application.Interfaces.Commons;
using Microsoft.Extensions.Hosting;

namespace BlindTreasure.Application.Services.Commons;

public class WorkerService : BackgroundService
{
    private readonly ILoggerService _loggerService;

    public WorkerService(ILoggerService loggerService)
    {
        _loggerService = loggerService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _loggerService.Info("Background service started.");

        // Execute the task once
        await PerformTaskAsync(stoppingToken);

        _loggerService.Info("Background service completed.");
    }

    private async Task PerformTaskAsync(CancellationToken stoppingToken)
    {
        _loggerService.Info($"Task executed at: {DateTime.Now}");
        // Replace with actual task logic
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
        _loggerService.Info("Background service stopped.");
        return base.StopAsync(stoppingToken);
    }}