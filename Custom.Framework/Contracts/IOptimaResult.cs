using Custom.Framework.Models.Base;

namespace Custom.Framework.Contracts
{
    public interface IOptimaResult
    {
        object? Data { get; set; }

        string? ApiAddress { get; set; }
        DateTime ApiEnd { get; set; }
        DateTime ApiStart { get; set; }
        string? ApiVersion { get; set; }
        OptimaMessage? Message { get; set; }
        string? RemoteServiceUrl { get; set; }

        bool Error { get; set; }
        bool IsCachedData { get; set; }
        bool Warning { get; set; }
        bool Empty { get; set; }
        bool IsSuccess { get; }

        public string? RequestUrl { get; set; }
        public object? RequestData { get; set; }
    }

    //public interface IOptimaResult<TFilterType> : IOptimaResult where TFilterType : class
    //{
    //    TFilterType? Data { get; set; }
    //}

    public interface IOptimaResult<TData>
    {
        string? ApiAddress { get; set; }
        DateTime ApiEnd { get; set; }
        DateTime ApiStart { get; set; }
        string? ApiVersion { get; set; }
        TData? Data { get; set; }
        bool Empty { get; set; }
        bool Error { get; set; }
        bool IsCachedData { get; set; }
        bool IsSuccess { get; }
        OptimaMessage? Message { get; set; }
        string? RemoteServiceUrl { get; set; }
        object? RequestData { get; set; }
        string? RequestUrl { get; set; }
        bool Warning { get; set; }
    }
}