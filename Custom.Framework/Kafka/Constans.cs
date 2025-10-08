namespace Custom.Framework.Kafka
{
    public class Constans
    {
    }

    /// <summary>
    /// Default timeouts for test operations
    /// </summary>
    public static class Timeouts
    {
        private static readonly bool IsCI = 
            Environment.GetEnvironmentVariable("CI") == "true"
            || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true"
            || Environment.GetEnvironmentVariable("AZURE_PIPELINES") == "true"
            || Environment.GetEnvironmentVariable("JENKINS_URL") != null
            || Environment.GetEnvironmentVariable("TEAMCITY_VERSION") != null
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_NUMBER"));

        private static readonly bool IsTest = 
            AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.FullName?.Contains("testhost") == true
                              || assembly.FullName?.Contains("xunit") == true
                              || assembly.FullName?.Contains("nunit") == true
                              || assembly.FullName?.Contains("mstest") == true);

        private static readonly int Multiplier = (IsCI || IsTest) ? 3 : 1;
        public static readonly TimeSpan BrokerConnection = TimeSpan.FromSeconds(10 * Multiplier);
        public static readonly TimeSpan ConsumerInitialization = TimeSpan.FromSeconds(3 * Multiplier);
        public static readonly TimeSpan MessageDelivery = TimeSpan.FromSeconds(5 * Multiplier);
        public static readonly TimeSpan BatchDelivery = TimeSpan.FromSeconds(15 * Multiplier);
        public static readonly TimeSpan ExactlyOnceDelivery = TimeSpan.FromSeconds(15 * Multiplier);
        public static readonly TimeSpan MultipleProducersDelivery = TimeSpan.FromSeconds(20 * Multiplier);
        public static readonly TimeSpan ProducerFlush = TimeSpan.FromSeconds(5 * Multiplier);
        public static readonly TimeSpan TopicCreation = TimeSpan.FromSeconds(30 * Multiplier);
        public static readonly TimeSpan ConsumerUnsubscribeStop = TimeSpan.FromSeconds(10 * Multiplier);
        public static readonly TimeSpan RetryBackoffMs = TimeSpan.FromMilliseconds(1000 * Multiplier);
        public static readonly TimeSpan TransactionTimeoutMs = TimeSpan.FromMilliseconds(60000 * Multiplier);
    }
}
