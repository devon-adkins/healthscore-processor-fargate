using HealthScore.Core;
using HealthScore.Core.Enums;
using HealthScore.Integration.DTOs;
using HealthScore.Integration.Interfaces.Microservices;
using HealthScore.Processor.Domain.Components;
using HealthScore.Processor.Domain.Helpers;
using HealthScore.Processor.Domain.Interfaces;
using HealthScore.Processor.Domain.Interfaces.Configurations;
using Serilog;

namespace HealthScore.Processor.Workers
{
    public class MeasurementAveragesHourlyWorker : CronJobService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MeasurementAveragesHourlyWorker> _logger;

        public MeasurementAveragesHourlyWorker(ILogger<MeasurementAveragesHourlyWorker> logger,
            IJobScheduler<MeasurementAveragesHourlyWorker> scheduler,
            IServiceScopeFactory scopeFactory
        )
            : base(logger, scheduler.CronExpression, scheduler.TimeZoneInfo)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task DoWork(CancellationToken cancellationToken)
        {
            Logger.LogInformation("{Service} running at: {timestamp}", nameof(MeasurementAveragesHourlyWorker), DateTime.UtcNow);

            await using var scope = _scopeFactory.CreateAsyncScope();
            try
            {
                Thread.CurrentPrincipal = await IdentityHelper.GetServicePrincipalUserAsync(_scopeFactory);
                var identityMicroservice = scope.ServiceProvider.GetService<IIdentityMicroservice>()!;
                var measurementsMicroservice = scope.ServiceProvider.GetService<IMeasurementsMicroservice>()!;
                var appSettings = scope.ServiceProvider.GetService<IAppSettings>()!;

                var permissions = await identityMicroservice.GetPermissionsAsync();
                var request = new IntegrationRequest<Dictionary<string, object>>(permissions)
                {
                    Content = new Dictionary<string, object>
                    {
                        {"page", 1},
                        {"pageSize", 100},
                        {"externalStatus", Constants.DefaultExternalStatus},
                        {"statsOnly", true}
                    }
                };

                var timePeriods = Enum.GetValues(typeof(TimePeriods))
                    .Cast<TimePeriods>()
                    .Where(w => w.ToString().ToLower().Contains("this"))
                    .ToList();

                foreach (var timePeriod in timePeriods)
                {
                    foreach (var accessCode in appSettings.AccessCodes)
                    {
                        await measurementsMicroservice.CalculateAverageMeasurments(request, accessCode, timePeriod.ToString());
                    }
                }

                Logger.LogInformation("{Service} finished at: {timestamp}", nameof(MeasurementAveragesHourlyWorker), DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "{Service} terminated unexpectedly", typeof(MeasurementAveragesHourlyWorker));
            }
        }
    }
}