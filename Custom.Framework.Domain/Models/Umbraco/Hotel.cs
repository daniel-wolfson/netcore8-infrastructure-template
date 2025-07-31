using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Umbraco
{
    public class Hotel
    {
        public int HotelID { get; set; }
        public Codes HotelCode { get; set; }
        public List<Codes> Rooms { get; set; }
        public int OptimaSettingsId { get; set; }
    }
}
