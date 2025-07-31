using Custom.Domain.Optima.Models.Base;
using Custom.Domain.Optima.Models.Main;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Domain.Optima.Models.Umbraco
{
    public class RoomData : OptimaData
    {
        public string Code { get; set; }
        public string CodeSource { get; set; }
        public string HotelCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public List<string> SpecialRequests { get; set; }
        public List<int> RoomFilters { get; set; }
        public string MoreInfo { get; set; }

        [ForeignKey("HotelCode")]
        public virtual HotelData Hotel { get; set; }  // Navigation property to HotelData
    }
}
