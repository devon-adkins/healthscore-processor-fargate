using System.Collections.Generic;
using HealthScore.Core.Enums;
using HealthScore.Core.Models;
using HealthScore.Integration.Configurations;
using HealthScore.Processor.Domain.Interfaces.Configurations;

namespace HealthScore.Processor.Domain.Configurations
{
    public class AppSettings : IAppSettings
    {
        public HealthScoreServices CurrentService { get; set; }
        public string AppName { get; set; } = string.Empty;
        public List<string> AccessCodes { get; set; }
        public MessageBroker RabbitMq { get; set; } = new();
        public Jwt Jwt { get; set; } = new();
        public HealthScoreSystem HealthScoreSystem { get; set; } = new();
        public AwsConfiguration Aws { get; set; } = new();
    }
}