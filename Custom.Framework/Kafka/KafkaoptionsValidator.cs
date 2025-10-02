using Custom.Framework.Kafka;
using Microsoft.Extensions.Options;

public class KafkaoptionsValidator : IValidateOptions<KafkaOptions>
{
    public ValidateOptionsResult Validate(string? name, KafkaOptions settings)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.Common.BootstrapServers))
            errors.Add("Kafka.Common.BootstrapServers is empty.");

        var duplicateNames = settings.Consumers
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dn in duplicateNames) errors.Add($"Duplicate consumer Name: {dn}");

        if (errors.Count > 0) return ValidateOptionsResult.Fail(errors);

        return ValidateOptionsResult.Success;
    }
}