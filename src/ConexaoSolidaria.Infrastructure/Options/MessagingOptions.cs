namespace ConexaoSolidaria.Infrastructure.Options;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public MessagingProvider Provider { get; set; } = MessagingProvider.RabbitMq;
    public RabbitMqOptions RabbitMq { get; set; } = new();
    public AwsMessagingOptions Aws { get; set; } = new();
}

public enum MessagingProvider
{
    RabbitMq = 0,
    Aws = 1
}

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Usuario { get; set; } = "guest";
    public string Senha { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public ushort PrefetchCount { get; set; } = 10;
}

public sealed class AwsMessagingOptions
{
    public string TopicName { get; set; } = "doacoes-recebidas";
    public string QueueName { get; set; } = "doacoes-recebidas";
    public string DeadLetterQueueName { get; set; } = "doacoes-recebidas-dlq";
    public int MaxNumberOfMessages { get; set; } = 10;
    public int WaitTimeSeconds { get; set; } = 10;
}
