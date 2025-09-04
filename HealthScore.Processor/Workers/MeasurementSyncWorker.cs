using HealthScore.Core;
using HealthScore.Core.Extensions;
using HealthScore.Core.Validation;
using HealthScore.Integration.DTOs;
using HealthScore.Integration.Helpers;
using HealthScore.Integration.Interfaces.Microservices;
using HealthScore.Processor.Domain.Components;
using HealthScore.Processor.Domain.Helpers;
using HealthScore.Processor.Domain.Interfaces;
using Serilog;

namespace HealthScore.Processor.Workers;

public class MeasurementSyncWorker : CronJobService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MeasurementSyncWorker(ILogger<MeasurementSyncWorker> logger,
        IJobScheduler<MeasurementSyncWorker> scheduler,
        IServiceScopeFactory scopeFactory
    )
        : base(logger, scheduler.CronExpression, scheduler.TimeZoneInfo)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task DoWork(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        try
        {
            Thread.CurrentPrincipal = await IdentityHelper.GetServicePrincipalUserAsync(_scopeFactory);
            var identityMicroservice = scope.ServiceProvider.GetService<IIdentityMicroservice>()!;
            var providersMicroservice = scope.ServiceProvider.GetService<IProvidersMicroservice>()!;
            var measurementsMicroservice = scope.ServiceProvider.GetService<IMeasurementsMicroservice>()!;
            var configurationsMicroservice = scope.ServiceProvider.GetService<IConfigurationsMicroservice>()!;

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
            var stats = await measurementsMicroservice.GetMeasurementsAsync(request);
            var totalPages = stats.TotalItems / stats.PageSize;
            var remainder = stats.TotalItems % stats.PageSize;
            if (remainder > 0) totalPages += 1;
            var currentPageSize = stats.TotalItems < stats.PageSize ? stats.TotalItems : stats.PageSize;

            Logger.LogInformation("{Items} measurements found. Will be processed in {TotalPages} batches of {PageSize} items per batch", stats.TotalItems, totalPages, currentPageSize);

            if (stats.TotalItems > 0)
            {
                //toDo: Parallel threads
                for (var page = 1; page <= totalPages; page++)
                {
                    var dataRequest = new IntegrationRequest<Dictionary<string, object>>(permissions)
                    {
                        Content = new Dictionary<string, object>
                        {
                            {"page", page},
                            {"pageSize", stats.PageSize},
                            {"externalStatus", Constants.DefaultExternalStatus},
                            {"sortBy", "Created"},
                            {"sortDirection", "Descending"}
                        }
                    };
                    var measurements = await measurementsMicroservice.GetMeasurementsAsync(dataRequest);
                    foreach (var measurement in measurements.Items)
                    {
                        //client types
                        var profileRequest = new IntegrationRequest<Dictionary<string, object>>(permissions)
                        {
                            Content = new Dictionary<string, object>
                        {
                            {"page", 1},
                            {"pageSize", 500},
                            {"clientId", measurement.ClientId}
                        }
                        };
                        var profilesResults = await configurationsMicroservice.GetClientMeasurementProfilesAsync(profileRequest);

                        try
                        {
                            //Get from providers
                            var providerRequest = new IntegrationRequest<ProviderMeasurementRequest>(permissions)
                            {
                                Content = new ProviderMeasurementRequest
                                { ExternalId = measurement.ExternalId, ProviderId = measurement.ProviderId }
                            };
                            var providerMeasurement = await providersMicroservice.GetMeasurementByIdAsync(providerRequest);

                            //Update measurement
                            var updateRequest = new IntegrationRequest<UpdateMeasurementRequest>(permissions)
                            {
                                Id = measurement.Id,
                                Content = providerMeasurement.ToUpdateMeasurementRequest(profilesResults.Items)
                            };
                            await measurementsMicroservice.UpdateMeasurementAsync(updateRequest);
                        }
                        catch (Exception e)
                        {
                            if (e.IsHealthScoreException(out FailedResult result))
                            {
                                //Update measurement
                                var updateRequest = new IntegrationRequest<UpdateMeasurementRequest>(permissions)
                                {
                                    Id = measurement.Id,
                                    Content = new UpdateMeasurementRequest()
                                    {
                                        MeasurementStatus = MeasurementHelper.SetDynamicMeasurementStatus(profilesResults.Items, measurement),
                                        ExternalStatus = $"ERROR_{result.StatusCode}"
                                    }
                                };
                                await measurementsMicroservice.UpdateMeasurementAsync(updateRequest);
                            }
                            Logger.LogWarning("{Message}", e.Message);
                        }
                    }
                    Logger.LogInformation("Processed batch {Page} of {TotalPages} containing {Items} measurements", page, totalPages, measurements.Items.Count);
                }
            }

        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "{Service} terminated unexpectedly", typeof(MeasurementSyncWorker));
        }
    }
}