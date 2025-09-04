using System;
using HealthScore.Processor.Domain.Interfaces;

namespace HealthScore.Processor.Domain.Components;

public class JobScheduler<T> : IJobScheduler<T>
{
    public string CronExpression { get; set; }
    public TimeZoneInfo TimeZoneInfo { get; set; }
}