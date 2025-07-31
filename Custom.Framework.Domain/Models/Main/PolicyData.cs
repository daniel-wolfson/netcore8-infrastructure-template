using Custom.Domain.Optima.Models.Base;

namespace Custom.Domain.Optima.Models.Main
{
    public class PolicyData : OptimaData
    {
        public int HotelID { get; set; }
        public string PolicyCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool MustProvidePayment { get; set; }
        public string CancellationPolicyCode { get; set; }
        public string GuaranteePolicyCode { get; set; }
        public string DepositePolicyCode { get; set; }
        public int Priority { get; set; }
        public bool IsDeleted { get; set; }
    }
}