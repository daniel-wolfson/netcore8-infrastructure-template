using Newtonsoft.Json;

namespace Custom.Domain.Optima.Models.Availability
{
    public class ReqDefinitions
    {
        [JsonProperty("isCityCodeSearchOnly")]
        public bool IsCityCodeSearchOnly { get; set; }
    }
}