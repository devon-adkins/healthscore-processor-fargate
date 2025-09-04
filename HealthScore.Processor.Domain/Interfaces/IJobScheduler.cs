using System;

namespace HealthScore.Processor.Domain.Interfaces;

public interface IJobScheduler<T>
{
    string CronExpression { get; set; }
    TimeZoneInfo TimeZoneInfo { get; set; }
    
}