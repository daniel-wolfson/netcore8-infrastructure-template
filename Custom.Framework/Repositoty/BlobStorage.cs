using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Custom.Framework.Configuration;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog.Events;
using System.Text;

namespace Custom.Framework.Repositoty;

public class BlobStorage : ApiServiceBase, IBlobStorage
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _blobContainerClient;
    private readonly ILogger _logger;
    readonly ApiSettings _apiSettings;

    /// <summary>Constructor</summary>>
    public BlobStorage(IHttpContextAccessor httpContextAccessor, IOptions<ApiSettings> appSettings, ILogger logger)
        : base(httpContextAccessor)
    {
        _logger = logger;
        _apiSettings = appSettings.Value;
        _blobServiceClient = new BlobServiceClient(_apiSettings.AzureStorage.ConnectionString);
        _blobContainerClient = _blobServiceClient.GetBlobContainerClient(_apiSettings.AzureStorage.ContainerName);
    }

    /// <summary> GetBlobClient </summary>
    public BlobClient GetBlobClient(string folderPath, string blobName)
    {
        ApiThrowHelper.IfNullOrWhitespace(blobName);
        return _blobContainerClient.GetBlobClient($"{folderPath.ToUpper()}/{blobName}.json");
    }

    /// <summary>Exists</summary>
    public async Task<bool> ExistsAsync(string folderPath, string blobName)
    {
        var blockBlob = GetBlobClient(folderPath, blobName);
        var exists = await blockBlob.ExistsAsync();

        if (!exists)
            _logger.Warning("{TITLE} error: {BLOBNAME} not exists", ApiHelper.LogTitle(), $"{folderPath}/{blobName}.json");

        return exists;
    }

    /// <summary>Exists</summary>
    public bool Exists(string folderPath, string blobName)
    {
        var blockBlob = GetBlobClient(folderPath, blobName);
        var exists = blockBlob.Exists();

        if (!exists)
        {
            _logger.Write(LogEventLevel.Error,
                "{TITLE} error: {BLOBNAME} not exists", 
                ApiHelper.LogTitle(), $"{folderPath}/{blobName}.json");
        }

        return exists;
    }

    /// <summary>Delete a blob</summary>
    public async Task DeleteAsync(string folderPath, string blobName)
    {
        var blockBlob = GetBlobClient(folderPath, blobName);
        if (await blockBlob.ExistsAsync())
            await blockBlob.DeleteAsync();
    }


    /// <summary> Download and serialize from blob </summary>
    public async Task<IServiceResult<T?>> DownloadAsync<T>(string folderPath, string blobName)
    {
        try
        {
            if (!await ExistsAsync(folderPath, blobName))
                return ServiceResult<T?>.NotFound($"Blob {folderPath}/{blobName} not found");

            var blobClient = GetBlobClient(folderPath, blobName);
            BlobDownloadInfo download = await blobClient.DownloadAsync();

            var result = download.Content.GetValue<T>(blobName);

            return ServiceResult<T?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                   ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<T?>.Error(ex.Message);
        }
    }

    /// <summary> Download from blob and deserialize to object that it depends of dataType </summary>
    public async Task<IServiceResult<object?>> DownloadAsync(string folderPath, string blobName, Type dataType)
    {
        try
        {
            if (!await ExistsAsync(folderPath, blobName))
                return ServiceResult<object?>.NotFound($"Blob {folderPath}/{blobName} not found");

            var blobClient = GetBlobClient(folderPath, blobName);
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            var result = download.Content.GetValue(dataType);

            return ServiceResult<object?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<object?>.Error(ex.Message);
        }
    }

    /// <summary> Download from blob and deserialize to object that it based on dataType of blobName </summary>
    public async Task<IServiceResult<string?>> DownloadAsync(string folderPath, string blobName)
    {
        try
        {
            if (!await ExistsAsync(folderPath, blobName))
                return ServiceResult<string?>.NotFound($"Blob {folderPath}/{blobName} not found");

            var blobClient = GetBlobClient(folderPath, blobName);
            BlobDownloadInfo download = await blobClient.DownloadAsync();
            using var reader = new StreamReader(download.Content);
            var result = reader.ReadToEnd();
            return ServiceResult<string?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<string?>.Error(ex.Message);
        }
    }

    /// <summary> Download from blob and deserialize to object that it based on dataType of blobName </summary>
    public IServiceResult<T?> Download<T>(string folderPath, string blobName)
    {
        try
        {
            if (!Exists(folderPath, blobName))
                return ServiceResult<T?>.NotFound($"Blob {folderPath}/{blobName} not found");

            T? result = default;
            var blobClient = GetBlobClient(folderPath, blobName);
            if (blobClient.Exists())
            {
                BlobDownloadInfo download = blobClient.Download();
                using var reader = new StreamReader(download.Content);
                string content = reader.ReadToEnd();

                if (typeof(T) != typeof(string))
                    result = JsonConvert.DeserializeObject<T>(content);
                else
                    result = (T)Convert.ChangeType(reader.ReadToEnd(), typeof(T));
            }

            return ServiceResult<T?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<T?>.Error(ex.Message);
        }
    }

    /// <summary> Download from blob and deserialize to object that it based on dataType of blobName </summary>
    public IServiceResult<string?> Download(string folderPath, string blobName)
    {
        try
        {
            if (!Exists(folderPath, blobName))
                return ServiceResult<string?>.NotFound($"Blob {folderPath}/{blobName} not found");

            string? result = default;
            var blobClient = GetBlobClient(folderPath, blobName);
            if (blobClient.Exists())
            {
                BlobDownloadInfo download = blobClient.Download();
                using var reader = new StreamReader(download.Content);
                result = reader.ReadToEnd();
            }
            return ServiceResult<string?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<string?>.Error(ex.Message);
        }
    }

    /// <summary> Download from blob and deserialize to object that it based on dataType of blobName </summary>
    public IServiceResult<object?> Download(string folderPath, string blobName, Type dataType)
    {
        if (!Exists(folderPath, blobName))
            return ServiceResult<object?>.NotFound($"Blob {folderPath}/{blobName} not found");

        try
        {
            object? result = default;
            var blobClient = GetBlobClient(folderPath, blobName);
            BlobDownloadInfo download = blobClient.Download();
            using var reader = new StreamReader(download.Content);
            var jsonResult = reader.ReadToEnd();

            if (blobName == SettingKeys.UmbracoSettings.ToString())
                jsonResult = ApiConvertHelper.ToUmbracoList(jsonResult);

            result = JsonConvert.DeserializeObject(jsonResult, dataType);
            return ServiceResult<object?>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("{TITLE} exception: {Exception}. \nStackTrace: {StackTrace}\n",
               ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            return ServiceResult<object?>.Error(ex.Message);
        }
    }

    /// <summary>Uploads a stream to the named blob</summary>
    public async Task<bool> UploadAsync<T>(string folderName, string blobName, T data,
        Dictionary<string, string>? metadataList = null)
    {
        try
        {
            string? jsonData = default;
            if (data?.GetType() == typeof(string))
                jsonData = data.ToString();
            else
                jsonData = JsonConvert.SerializeObject(data);

            if (ApiJsonHelper.IsNullOrEmpty(jsonData))
            {
                _logger.Error("{TITLE} error: {BLOBNAME} data is empty", ApiHelper.LogTitle(), blobName);
                return false;
            }

            var exists = await ExistsAsync(folderName, blobName);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData!));
            metadataList = AddDefaultMetadata(metadataList);

            var conditions = exists
                ? new BlobRequestConditions { IfMatch = ETag.All }
                : new BlobRequestConditions { IfNoneMatch = new ETag("*") };

            var blobClient = GetBlobClient(folderName, blobName);
            var result = await blobClient.UploadAsync(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                Conditions = conditions,
                Metadata = metadataList
            });

            return !result.GetRawResponse().IsError;

        }
        catch (Exception ex)
        {
            throw new ApiBlobUploadException(ex);
        }
    }

    /// <summary>Uploads a stream to the named blob</summary>
    public bool Upload<T>(string folderName, string blobName, T data,
        Dictionary<string, string>? metadataList = null)
    {
        try
        {
            ApiThrowHelper.ThrowIfNull(data);

            string jsonData = string.Empty;
            if (typeof(T) == typeof(string))
                jsonData = data?.ToString() ?? string.Empty;
            else
                jsonData = JsonConvert.SerializeObject(data);

            ApiThrowHelper.ThrowIf(() => ApiJsonHelper.IsNullOrEmpty(jsonData), "jsonData is not valid");

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonData));
            metadataList = AddDefaultMetadata(metadataList);
            var blobClient = GetBlobClient(folderName, blobName);

            BlobRequestConditions conditions;
            var isExists = blobClient.Exists();

            if (!isExists)
                conditions = new BlobRequestConditions { IfNoneMatch = new ETag("*") }; // create new blob
            else
                conditions = new BlobRequestConditions { IfMatch = ETag.All }; // update exists blob

            var result = blobClient.Upload(stream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                Conditions = conditions,
                Metadata = metadataList
            });

            return !result.GetRawResponse().IsError;
        }
        catch (Exception ex)
        {
            throw new ApiException(ex);
        }
    }

    /// <summary>List all files by folderPath</summary>
    public async Task<List<BlobHierarchyItem>> GetListFilesAsync(string folderPath, string? prefix = null, BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None)
    {
        var result = new List<BlobHierarchyItem>();

        await foreach (var blobItem in _blobContainerClient.GetBlobsByHierarchyAsync(delimiter: "/", traits: traits, states: states, prefix: folderPath))
        //await foreach (BlobItem blobItem in _blobContainerClient.GetBlobsAsync(traits, states, prefix))
        {
            result.Add(blobItem);
        }

        return result;
    }

    #region private methods

    private string GetBlobFileName(string blobName)
    {
        return Path.HasExtension(blobName) ? blobName : $"{blobName}.json";
    }

    private static Dictionary<string, string> AddDefaultMetadata(Dictionary<string, string>? metadataList)
    {
        metadataList ??= [];
        if (!metadataList.ContainsKey("SettingType"))
            metadataList.Add("SettingType", SettingKeys.StaticData.ToString());
        if (!metadataList.ContainsKey("created"))
            metadataList.Add("Created", DateTime.UtcNow.ToString("s"));
        return metadataList;
    }

    #endregion private methods
}