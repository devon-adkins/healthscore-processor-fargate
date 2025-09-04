namespace HealthScore.Processor.Domain.Configurations;

public class AwsConfiguration
{
    public string AccessKeyId { get; set; }
    public string SecretAccessKey { get; set; }
    public string Region { get; set; }
    public string LogGroup { get; set; }
}