using Custom.Framework.Contracts;

namespace Custom.Framework.Models.Base
{
    public class OptimaResult : IOptimaResult
    {
        public OptimaResult()
        {
            Message = new OptimaMessage { Text = $"" };
            Data = default;
        }
        public OptimaResult(object? data, string message, string? status = null)
        {
            Message = new OptimaMessage { Text = $"{message}.{(!string.IsNullOrEmpty(status) ? $" Status:{status}" : "")}" };
            Data = data;
        }

        public object? Data { get; set; }
        public bool Error { get; set; }
        public bool Warning { get; set; }
        public bool Empty { get; set; }
        public OptimaMessage? Message { get; set; }
        public string? ApiAddress { get; set; }
        public string? RemoteServiceUrl { get; set; }
        public bool IsCachedData { get; set; }
        public DateTime ApiStart { get; set; }
        public DateTime ApiEnd { get; set; }
        public string? ApiVersion { get; set; }
        public virtual bool IsSuccess => Error == false;
        public string? RequestUrl { get; set; }
        public object? RequestData { get; set; }
    }

    public class OptimaResult<TData> : IOptimaResult<TData>
    {
        public OptimaResult() { }
        public OptimaResult(string message)
        {
            Data = default;
            Message = new OptimaMessage() { Text = message ?? "" };
        }
        public OptimaResult(TData? data)
        {
            Data = data;
            Message = new OptimaMessage { Text = "" };
            Error = false;
        }
        public OptimaResult(TData? data, string? message = "", bool error = false)
        {
            Data = data;
            Message = new OptimaMessage { Text = $"{message ?? ""}." };
            Error = error;
        }
        public OptimaResult(TData? data, string? message = "", bool error = false, string? status = null)
        {
            Message = new OptimaMessage { Text = $"{message ?? ""}.{(!string.IsNullOrEmpty(status) ? $" Status:{status}" : "")}" };
            Data = data;
            Error = error;
        }
        public OptimaResult(TData? data, string? message = "", string? status = null, bool error = false)
        {
            Message = new OptimaMessage { Text = $"{message ?? ""}.{(!string.IsNullOrEmpty(status) ? $" Status:{status}" : "")}" };
            Data = data;
            Error = error;
        }

        /// <summary>
        /// payload data of Optima
        /// </summary>
        public TData? Data { get; set; }

        public virtual bool IsSuccess => Error == false;

        /// <summary>
        /// Request url
        /// </summary>
        public string? RequestUrl { get; set; }

        /// <summary>
        /// Access the request data (strong Object for all posible of requests)
        /// </summary>
        public object? RequestData { get; set; }

        public bool Error { get; set; }
        public bool Warning { get; set; }
        public bool Empty { get; set; }
        public OptimaMessage? Message { get; set; }
        public string? ApiAddress { get; set; }
        public string? RemoteServiceUrl { get; set; }
        public bool IsCachedData { get; set; }
        public DateTime ApiStart { get; set; }
        public DateTime ApiEnd { get; set; }
        public string? ApiVersion { get; set; }
    }
}