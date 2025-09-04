using System.Collections.Generic;
using HealthScore.Integration.Interfaces.Configurations;
using HealthScore.Processor.Domain.Configurations;

namespace HealthScore.Processor.Domain.Interfaces.Configurations
{
    public interface IAppSettings : IBaseAppSettings
    {
        public List<string> AccessCodes { get; set; }
        public AwsConfiguration Aws { get; set; }
    }
}