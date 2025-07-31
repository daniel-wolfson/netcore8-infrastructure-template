using Newtonsoft.Json;

namespace Custom.Framework.Models
{
    public class ApiValidationError
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Field { get; }

        public string Message { get; }

        public ApiValidationError(string field, string message)
        {
            Field = field;
            Message = message;
        }

        public override string ToString()
        {
            return $"Field: {Field}, Message: {Message}";
        }
    }
}
