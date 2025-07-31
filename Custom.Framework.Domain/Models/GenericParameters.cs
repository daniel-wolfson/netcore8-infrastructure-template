using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Custom.Domain.Optima.Models
{
    public class GenericParameters
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Stamp { get; set; }

        [JsonProperty("ignore Dynamic Flight")]
        public string IgnoreDynamicFlight { get; set; }

        [JsonProperty("ignore Subsidized Flight")]
        public string IgnoreSubsidizedFlight { get; set; }

        [JsonProperty("special Population")]
        public string SpecialPopulation { get; set; }

        [JsonProperty("terms of Condition Title")]
        public string TermsofConditionTitle { get; set; }

        [JsonProperty("discount Category")]
        public string discountCategory { get; set; }

        [JsonProperty("discount Filter")]
        public string DiscountFilter { get; set; }

        public string Tag { get; set; }
    }
}
