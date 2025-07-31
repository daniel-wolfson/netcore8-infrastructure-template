using OpenTelemetry.Trace;
using System.Diagnostics;
using OpenTelemetry;

public class ConditionalSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Retrieve the current Activity
        var activity = Activity.Current;

        if (activity != null)
        {
            // Check if the activity contains the "isDebugMode" tag with value "true"
            foreach (var tag in activity.Tags)
            {
                if (tag.Key.Contains("isDebugMode", StringComparison.OrdinalIgnoreCase) && tag.Value?.ToLower() == "true")
                {
                    return new SamplingResult(true);
                }
            }

            // Alternatively, check in Baggage if it was set as a baggage item
            if (Baggage.Current.GetBaggage("isDebugMode") == "true")
            {
                return new SamplingResult(true);
            }
        }

        // Default to not sampling if the conditions aren't met
        return new SamplingResult(false);
    }
}
