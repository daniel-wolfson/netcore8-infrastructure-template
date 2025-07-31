using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Base
{
    public class OptimaData : IOptimaData
    {
        public OptimaData()
        {
            LastUpdate = DateTime.Now;
        }

        [JsonProperty("lastUpdate")]
        public DateTime LastUpdate { get; set; }

        [JsonProperty("customerID")]
        public int? CustomerId { get; set; }

        [NotMapped]
        [JsonProperty("genericParameters")]
        public Dictionary<string, string>? GenericParameters { get; set; }
    }

    public interface IOptimaData
    {

        DateTime LastUpdate { get; set; }
        int? CustomerId { get; set; }
        Dictionary<string, string>? GenericParameters { get; set; }
    }
}