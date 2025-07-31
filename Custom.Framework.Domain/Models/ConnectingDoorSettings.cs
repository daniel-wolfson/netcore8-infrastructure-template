using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models
{
    public class ConnectingDoorSettings
    {
        public int UmbracoSearchSettingsId { get; set; } = 1;
        public string HotelCode { get; set; }
        public int MinimumChildren4ConnectingDoorRoom { get; set; }

        [NotMapped]
        public Dictionary<string, int> AvailableUnits { get; set; }

        // Use this property as a placeholder for the JSON string
        [Column("AvailableUnits")]
        public string YourDictionaryJson
        {
            get => JsonConvert.SerializeObject(AvailableUnits);
            set => AvailableUnits = JsonConvert.DeserializeObject<Dictionary<string, int>>(value) ?? [];
        }
    }
}