using HealthScore.Core.Enums;
using HealthScore.Core.Models;
using HealthScore.Integration.Configurations;
using HealthScore.Processor.Domain.Components;
using HealthScore.Processor.Domain.Configurations;
using HealthScore.Processor.Domain.Interfaces;
using HealthScore.Processor.Domain.Interfaces.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace HealthScore.Processor.Domain.Extensions
{
    public static class ProgramExtensions
    {
        public static IAppSettings GetAppSettings(this IConfiguration configuration, HealthScoreServices currentService)
        {
            var appName = configuration["AppName"];
            var jwt = configuration.GetSection("Jwt");
            var rabbitMq = configuration.GetSection("RabbitMQ");
            var healthScoreSystem = configuration.GetSection("HealthScoreSystem");
            var client = healthScoreSystem.GetSection("Client");
            var identity = healthScoreSystem.GetSection("Identity");
            var microservices = healthScoreSystem.GetSection("Microservices");
            var permissionKeyDurationMinutes =  healthScoreSystem["PermissionKeyDurationMinutes"]; 
            var cron = configuration.GetSection("Cron");
            var accessCodes = configuration.GetSection("AccessCodes").Get<List<string>>();

            return new AppSettings
            {
                RabbitMq = new MessageBroker
                {
                    Host = rabbitMq["Host"],
                    UserName = rabbitMq["UserName"],
                    Password = rabbitMq["Password"]
                },
                Jwt = new Jwt()
                {
                    Key = jwt["Key"],
                    Issuer = jwt["Issuer"]
                },
                AppName = appName,
                CurrentService = currentService,
                HealthScoreSystem = new HealthScoreSystem()
                {
                    PermissionKeyDurationMinutes = Convert.ToInt32(permissionKeyDurationMinutes),
                    Client = new Client()
                    {
                        Id = client["Id"],
                        Secret = client["Secret"]
                    },
                    Identity = new Identity()
                    {
                        Token = identity["Token"]
                    },
                    Microservices = new Microservices()
                    {
                        IdentityBaseUrl = microservices["IdentityBaseUrl"],
                        ClientsBaseUrl = microservices["ClientsBaseUrl"],
                        PatientsBaseUrl = microservices["PatientsBaseUrl"],
                        AccountsBaseUrl = microservices["AccountsBaseUrl"],
                        ConfigurationsBaseUrl = microservices["ConfigurationsBaseUrl"],
                        CommunicationsBaseUrl = microservices["CommunicationsBaseUrl"],
                        MeasurementsBaseUrl = microservices["MeasurementsBaseUrl"],
                        ProvidersBaseUrl = microservices["ProvidersBaseUrl"],
                        ContentBaseUrl = microservices["ContentBaseUrl"]
                    },
                },
                AccessCodes = accessCodes
            };
        }
        
        public static IServiceCollection AddCronJob<T>(this IServiceCollection services, Action<IJobScheduler<T>> options) where T : CronJobService
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options), @"Please provide Schedule Configurations.");
            }
            var config = new JobScheduler<T>();
            options.Invoke(config);
            if (string.IsNullOrWhiteSpace(config.CronExpression))
            {
                throw new ArgumentNullException(nameof(JobScheduler<T>.CronExpression), @"Empty Cron Expression is not allowed.");
            }

            services.AddSingleton<IJobScheduler<T>>(config);
            services.AddHostedService<T>();
            return services;
        }
    }
}