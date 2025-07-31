using Azure.Storage.Blobs;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;

namespace Custom.Framework.HealthChecks
{
    public class AzureBlobApiHealthCheck(IOptions<ApiSettings> appSettingsOptions,
        BlobServiceClient blobServiceClient, ILogger logger,
        IConfiguration configuration, IServiceScopeFactory serviceScopeFactory) : IHealthCheck
    {
        private readonly ApiSettings _appSettings = appSettingsOptions.Value;
        private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
        private readonly ILogger _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IServiceScopeFactory _serviceFactory = serviceScopeFactory;

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            HealthCheckResult healthCheckResult;
            var data = new Dictionary<string, object>() {
                { "ContainerName", _appSettings.AzureStorage.ContainerName },
                { "Folder", $"{_appSettings.Version.ToUpper()}" },
                { "ConnectionString", _appSettings.AzureStorage.ConnectionString },
                { "BlobClientAssembly", typeof(BlobServiceClient).Assembly.FullName ?? "" }
            };

            try
            {
                var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_appSettings.AzureStorage.ContainerName);
                var blobFolderName = _appSettings.Version.ToUpper();

                // Attempt a read-only operation, like listing containers
                var blobResults = blobContainerClient.GetBlobs(prefix: blobFolderName, cancellationToken: cancellationToken);

                // If the operation succeeds, return a Healthy status
                bool isValid = blobResults.Any();
                if (isValid)
                {
                    var blobClient = blobContainerClient.GetBlobClient($"{blobFolderName}/OptimaSettings.json");
                    var blobExist = blobClient.Exists();
                    data.Add("OptimaSettings", $"{_appSettings.Version}/OptimaSettings is {(blobExist ? "healthy" : "unhealthy")}");

                    var settingKey = $"{SettingKeys.StaticData}:{SettingKeys.OptimaSettings}";
                    var optimaSettingsConfig = _configuration[settingKey];
                    var optimaSettingsConfigTimestamp = _configuration[$"{settingKey}:Timestamp"];

                    var scope = _serviceFactory.CreateScope();
                    var configurationProvider = scope.ServiceProvider.GetKeyedService<IConfigurationProvider>(SettingKeys.OptimaSettings) as ApiConfigurationProvider;
                    
                    configurationProvider?.LoadAsync(SettingKeys.OptimaSettings, EReasonTypes.Healthcheck, cancellationToken)
                        .GetAwaiter().GetResult();

                    var optimaSettingsConfigTimestampUpdated = _configuration[$"{settingKey}:Timestamp"];

                    _configuration[$"{settingKey}:Timestamp"] = "";

                    data.Add($"{settingKey}:Timestamp", optimaSettingsConfigTimestampUpdated ?? "");

                    healthCheckResult = HealthCheckResult.Healthy(
                        data: data.AsReadOnly(), description: $"state is healthy");

                    return Task.FromResult(healthCheckResult);
                }

                _logger.Error("{TITLE} folder {FOLDER} is empty", ApiHelper.LogTitle(), data["Folder"].ToString());

                healthCheckResult = HealthCheckResult.Unhealthy(
                    data: data.AsReadOnly(),
                    description: $"blob container exists, but it is empty");
            }
            catch (Exception ex)
            {
                _logger.Error("{TITLE} get blob from {Folder} exeption: {EX}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), data["Folder"].ToString(), 
                    ex.InnerException?.Message ?? ex.Message, ex.StackTrace);

                healthCheckResult = HealthCheckResult.Unhealthy(
                    exception: ex, data: data.AsReadOnly(),
                    description: $"blob container failed");
            }

            return Task.FromResult(healthCheckResult);
        }
    }
}