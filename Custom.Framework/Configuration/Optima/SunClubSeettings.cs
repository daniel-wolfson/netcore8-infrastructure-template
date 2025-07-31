namespace Custom.Framework.Configuration.Optima
{
    public class SunClubSeettings
    {
        public decimal RegisterFee { get; set; } = 250.0m;
        public string RegisterPackageCode { get; set; } = "SCL_REGISTER";
        public decimal RegularRenewalFee { get; set; } = 100.0m;
        public string RegularRenewalPackageCode { get; set; } = "SCL_RENEWSPE";
        public decimal SpecialRenewalFee { get; set; } = 100.0m;
        public string SpecialRenewalPackageCode { get; set; } = "SCL_RENEWSPE";
    }
}