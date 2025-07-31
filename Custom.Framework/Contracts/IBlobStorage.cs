using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Custom.Framework.Models;

namespace Custom.Framework.Contracts
{
    public interface IBlobStorage
    {
        BlobClient GetBlobClient(string folderName, string blobName);
        Task DeleteAsync(string folderName, string blobName);
        
        Task<IServiceResult<T?>> DownloadAsync<T>(string folderPath, string blobName);
        Task<IServiceResult<object?>> DownloadAsync(string folderPath, string blobName, Type resourceType);
        Task<IServiceResult<string?>> DownloadAsync(string folderPath, string blobName);
        IServiceResult<T?> Download<T>(string folderPath, string blobName);
        IServiceResult<string?> Download(string folderPath, string blobName);
        IServiceResult<object?> Download(string folderPath, string blobName, Type dataType);
        
        Task<bool> ExistsAsync(string folderPath, string blobName);
        bool Exists(string folderPath, string blobName);
        Task<bool> UploadAsync<T>(string folderPath, string blobName, T data, Dictionary<string, string>? metadataList = null);
        bool Upload<T>(string folderPath, string blobName, T data, Dictionary<string, string>? metadataList = null);
        Task<List<BlobHierarchyItem>> GetListFilesAsync(string folderPath, string? prefix = null, BlobTraits traits = BlobTraits.None, 
            BlobStates states = BlobStates.None);
    }
}