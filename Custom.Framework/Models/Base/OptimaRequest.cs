using Newtonsoft.Json;

namespace Custom.Framework.Models.Base
{
    public class OptimaRequest
    {
        public string UserName { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        /// <summary> current customerID </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? CustomerID { get; set; }

        /// <summary> IsLocal, by default true </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? IsLocal { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<int>? HotelIDList { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? HotelID { get; set; }
    }
}