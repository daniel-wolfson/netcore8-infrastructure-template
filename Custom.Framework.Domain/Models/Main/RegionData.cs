using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class RegionData : OptimaData
    {
        public Guid ID { get; set; }
        public int RegionID { get; set; }
        public int ChainID { get; set; }
        public string RegionName { get; set; }
        public string PmsRegionName { get; set; }
        public string PmsRegionCode { get; set; }
    }
}