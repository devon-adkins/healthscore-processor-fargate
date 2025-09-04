using System;
using System.Threading;
using System.Threading.Tasks;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;
namespace HealthScore.Processor.Domain.Components;

public abstract class CronJobService : IHostedService, IDisposable
{
    private Timer _timer;
    private readonly CronExpression _expression;
    private readonly TimeZoneInfo _timeZoneInfo;
    protected readonly ILogger<CronJobService> Logger;
    
    protected CronJobService(ILogger<CronJobService> logger, string cronExpression, TimeZoneInfo timeZoneInfo)
    {
        _expression = CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
        _timeZoneInfo = timeZoneInfo;
        Logger = logger;

    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await ScheduleJob(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        await Task.CompletedTask;
    }

    protected virtual async Task DoWork(CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);  
    }

    private async Task ScheduleJob(CancellationToken cancellationToken)
    {
        var next = _expression.GetNextOccurrence(DateTimeOffset.UtcNow, _timeZoneInfo);
        if (next.HasValue)
        {
            var delay = next.Value - DateTimeOffset.UtcNow;
            _timer = new Timer(delay.TotalMilliseconds);
            _timer.Elapsed += async (_, _) =>
            {
                _timer.Dispose();  
                _timer = null;

                if (!cancellationToken.IsCancellationRequested)
                {
                    await DoWork(cancellationToken);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    await ScheduleJob(cancellationToken);    // reschedule next
                }
            };
            _timer.Start();
        }
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}