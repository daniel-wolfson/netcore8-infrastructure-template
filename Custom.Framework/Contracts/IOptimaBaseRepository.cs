using Custom.Domain.Optima.Models.Base;
using Custom.Framework.Configuration;
using Custom.Framework.Models.Base;

namespace Custom.Framework.Contracts
{
    public interface IOptimaBaseRepository
    {
        Task<List<OptimaResult<List<TData>>>> SendBulkAsync<TData>(
            string apiHttpClientName, HttpMethod httpMethod, string path, OptimaRequest[] requests)
            where TData : OptimaData;

        Task<OptimaResult<TData>> SendAsync<TData>(string apiHttpClientName, HttpMethod httpMethod, string path, object request);
    }
}