using Newtonsoft.Json;

namespace Custom.Framework.Models
{

    public class ApiErrorDetails
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }
        public string? RequestUrl { get; set; }
        public string? RequestData { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
