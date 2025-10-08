using Custom.Framework.Kafka;
using Microsoft.Extensions.Options;

public class KafkaOptionsValidator : IValidateOptions<KafkaOptions>
{
    public ValidateOptionsResult Validate(string? name, KafkaOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Common.BootstrapServers))
            errors.Add("Kafka.Common.BootstrapServers required.");

        var duplicateNames = options.Consumers
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dn in duplicateNames) 
            errors.Add($"Duplicate consumer Name: {dn}");

        options.Producers.ForEach(p =>
        {
            if (p.Topics == null || p.Topics.Length == 0)
                errors.Add($"Producer {p.Name} must have at least one topic defined.");
        });
        options.Consumers.ForEach(p =>
        {
            if (p.Topics == null || p.Topics.Length == 0)
                errors.Add($"Consumer {p.Name} must have at least one topic defined.");
        });

        if (errors.Count > 0)
        {
            return ValidateOptionsResult.Fail(errors);
        }

        return ValidateOptionsResult.Success;
    }
}