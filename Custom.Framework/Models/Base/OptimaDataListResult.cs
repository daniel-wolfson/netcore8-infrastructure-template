namespace Custom.Framework.Models.Base
{
    public class OptimaDataListResult<TData> where TData : class
    {
        public OptimaDataListResult() { }

        public List<TData>? Data { get; set; }

        public OptimaDataListResult(string message) : this(message, default)
        {
        }
        public OptimaDataListResult(string message, TData? data, string? status = null)
        {
            Message = new OptimaMessage { Text = $"{message}.{(!string.IsNullOrEmpty(status) ? $" Status:{status}" : "")}" };
            Data = [data];
        }

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
}