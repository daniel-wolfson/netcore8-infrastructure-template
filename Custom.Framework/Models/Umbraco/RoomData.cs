namespace Custom.Framework.Models.Umbraco
{
    using System.ComponentModel.DataAnnotations;

    public class RoomData1
    {
        [Key]
        public string Code { get; set; }

        public string HotelCode { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public List<string> SpecialRequests { get; set; }

        public string RoomFilters { get; set; }

        public string MoreInfo { get; set; }
    }
}
