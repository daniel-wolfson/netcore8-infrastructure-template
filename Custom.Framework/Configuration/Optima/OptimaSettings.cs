using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Configuration.Optima
{
    public class OptimaSettings
    {
        public Guid OptimaSettingsId { get; set; }
        public List<CodesConversion> CodesConversion { get; set; }
        public List<SitesSetting> SitesSettings { get; set; }
    }

    public class RoomCodes
    {
        public Guid Id { get; set; }
        public string HotelCode { get; set; }
        public string GeneralCode { get; set; }
        public string G4Code { get; set; }
        public string? ClubApiCode { get; set; }
        public string? MarketPlaceCode { get; set; }
    }

    public class CodesConversion
    {
        public Guid CodesConversionId { get; set; } = Guid.NewGuid();
        public HotelCodes HotelCode { get; set; }
        public List<RoomCodes> Rooms { get; set; }
    }

    public class SitesSetting
    {
        public int Id { get; set; } // Add an Id for EF Core primary key requirement
        public int HomeRootNodeId { get; set; }
        [NotMapped]
        public CustomerIds OptimaCustomerId { get; set; }
        public int OptimaSettingsId { get; set; } // Foreign key
    }
}
