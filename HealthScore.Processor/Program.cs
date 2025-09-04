using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Amazon.Runtime;
using AWS.Logger;
using HealthScore.Core;
using HealthScore.Core.Enums;
using HealthScore.Core.Extensions;
using HealthScore.Integration.Components;
using HealthScore.Integration.Extensions;
using HealthScore.Integration.Interfaces.Configurations;
using HealthScore.Processor.Domain.Configurations;
using HealthScore.Processor.Domain.Extensions;
using HealthScore.Processor.Domain.Interfaces.Configurations;
using HealthScore.Processor.Workers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

const HealthScoreServices currentService = HealthScoreServices.Processor;

try
{
    var builder = Host.CreateDefaultBuilder(args);
    builder.ConfigureAppConfiguration((context, configuration) =>
    {
        var projectName = currentService.ToProjectName();
        var basePath = Directory.GetParent(Directory.GetCurrentDirectory())?.FullName!;
        basePath = context.HostingEnvironment.IsDevelopment() ? $"{basePath}/{projectName}" : projectName;
        configuration.AddJsonFile(Path.Combine(basePath, "appsettings.json"))
            .AddJsonFile(Path.Combine(basePath, $"appsettings.{context.HostingEnvironment.EnvironmentName}.json"))
            .AddEnvironmentVariables();


    });

    builder.ConfigureServices((context, services) =>
    {
        var appSettings = context.Configuration.GetAppSettings(currentService);

        //Dependency Injection
        services.AddSingleton<IBaseAppSettings>((AppSettings)appSettings);
        services.AddSingleton<IAppSettings>((AppSettings)appSettings);
        services.AddHealthScoreMicroservices<WorkerContext>();
        services.AddHealthScoreHttpClients(appSettings);

        // The following line enables Cloudwatch telemetry collection.
        services.AddLogging(loggingBuilder =>
        {
            var awsOptions = new AWSLoggerConfig
            {
                Region = appSettings.Aws.Region,
                LogGroup = appSettings.Aws.LogGroup,
                Credentials = new BasicAWSCredentials(appSettings.Aws.AccessKeyId, appSettings.Aws.SecretAccessKey),
                LogStreamNamePrefix = $"{currentService}"
            };

            loggingBuilder.AddAWSProvider(awsOptions);
            loggingBuilder.SetMinimumLevel(LogLevel.Information);
        });

        //Cron jobs
        services.AddCronJob<MeasurementSyncWorker>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 * * * * *";
        });

        services.AddCronJob<MeasurementAveragesHourlyWorker>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 0 * * * *";
        });

        services.AddCronJob<MeasurementAveragesDailyWorker>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 0 0 * * *";
        });

        //Security access
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = appSettings.Jwt.Issuer;
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidIssuer = appSettings.Jwt.Issuer,
                    ValidTypes = Constants.Auth.AccessTokenOnly,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.Jwt.Key)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Constants.Policies.DefaultApiPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("scope", currentService.ToString());
            });
        });
    });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "{Service} terminated unexpectedly", currentService);
}
finally
{
    Log.CloseAndFlush();
}