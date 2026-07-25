namespace Solidary.Infrastructure.Messaging;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string ConsumerGroup { get; set; } = "solidary-worker";
}
