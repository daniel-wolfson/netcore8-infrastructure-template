using Custom.Framework.Enums;
using Newtonsoft.Json;

namespace Custom.Framework.Models.Base
{
    /// <summary>
    /// DataResult - simple service generic result with data
    /// </summary>
    public class DataResult<T> : DataResult
    {
        public static implicit operator T(DataResult<T> result) => result.Data;
        public static implicit operator DataResult<T>(T value) => new(value);

        protected DataResult() { Data = default!; }

        public DataResult(T value) => Data = value;

        protected DataResult(T value, string successMessage)
            : this(value) => Message = successMessage;

        protected DataResult(ResultStatus status) : this() { Status = status; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public new T Data { get; init; }

        public Type ValueType => typeof(T);

        public ResultStatus Status { get; protected set; } = ResultStatus.Ok;
        public bool IsSuccess => Status is ResultStatus.Ok or ResultStatus.NoContent or ResultStatus.Created;

        public IEnumerable<string> GenericParametets { get; protected set; } = [];
    }

    /// <summary>
    /// DataResult - simple service result with data
    /// </summary>

    public class DataResult : Result
    {
        public static DataResult MakeError(string errorMsg)
        {
            return new DataResult("ErrorInfo", true, errorMsg, null, null);
        }

        public DataResult()
        {
        }

        public DataResult(string name, bool error, string? message, object? data = null, object? value = null)
            : base(name, error, message)
        {
            Data = data;
            Value = value;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? Data { get; set; } = null!;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? Value { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public object? ValidationErrors { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsValid { get; set; }
    }

    /// <summary>
    /// Result - simple service result without data
    /// </summary>
    public class Result
    {
        public Result()
        {
        }

        public Result(string name, bool error, string? message)
        {
            Name = name;
            Error = error;
            Message = message;
        }

        [JsonProperty(Order = 1)]
        public string Name { get; set; } = string.Empty;

        [JsonProperty(Order = 2)]
        public bool Error { get; set; }

        [JsonProperty(Order = 3)]
        public string? Message { get; set; } = null!;
    }
}