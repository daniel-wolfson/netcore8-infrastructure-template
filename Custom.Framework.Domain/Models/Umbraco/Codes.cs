using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Umbraco
{
    public class Codes
    {
        public string GeneralCode { get; set; }
        public string G4Code { get; set; }
        public string ClubApiCode { get; set; }
        public string MarketPlaceCode { get; set; }

        public int HotelID { get; set; }
    }
}
