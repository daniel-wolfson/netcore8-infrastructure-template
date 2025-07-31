using Custom.Domain.Optima.Models.Customer;
using Custom.Domain.Optima.Models.Main;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.Models.Main;

namespace Custom.Framework.Contracts
{
    public interface IStaticDataRepository : IOptimaBaseRepository
    {
        Task<OptimaResult<TData>> GetAsync<TData>(SettingKeys settingKey, CancellationToken cancellationToken = default);
        Task<OptimaResult<TData>> ReadAsync<TData>(SettingKeys settingKey, CancellationToken cancellationToken = default);

        Task<OptimaResult<TData>> GetAvailabilityPackageShow<TData>(List<int> hotelIds, List<int> customerIds);
        Task<OptimaResult<List<PackageShowData>>> GetAvailabilityPackageShow(List<int> hotelIds, List<int> customerIds);

        Task<OptimaResult<List<ClerkData>>> GetClerkAsync(int hotelId, string promoCode);
        Task<OptimaResult<List<ClerkData>>> GetClerksAsync(List<int> hotelIds);
        Task<OptimaResult<TData>> GetClerksAsync<TData>(List<int> hotelIds);

        Task<List<HotelData>> GetUmbHotels();
        Task<OptimaResult<TData>> GetHotelIds<TData>();
        Task<OptimaResult<TData>> GetHotelCodes<TData>();
        Task<OptimaResult<List<HotelData>?>> GetHotelCodes();
        Task<OptimaResult<TData>> GetHotels<TData>(List<int> customerIds);
        Task<OptimaResult<TData>> GetCustomerIds<TData>();

        Task<OptimaResult<TData>> GetPlans<TData>(List<int> customerIds);
        Task<OptimaResult<List<PlanData>>> GetPlans(List<int> customerIds);

        Task<OptimaResult<TData>> GetPriceCodeCategories<TData>(List<int> customerIds);
        Task<OptimaResult<List<PriceCodeCategoryData>>> GetPriceCodeCategories(List<int> customerIds);

        Task<OptimaResult<TData>> GetPriceCodeCategoryTranslations<TData>(List<int> customerId);
        Task<OptimaResult<List<PriceCodeCategoryTranslationsData>?>> GetPriceCodeCategoryTranslations(List<int> customerId);

        Task<OptimaResult<TData>> GetPriceCodeCategoryLinks<TData>(List<int> customerIds);
        Task<OptimaResult<List<PriceCodeCategoryLinkData>>> GetPriceCodeCategoryLinks(List<int> customerIds);

        Task<OptimaResult<TData>> GetPriceCodes<TData>(List<int> customerIds);
        Task<OptimaResult<List<PriceCodeData>>> GetPriceCodes(List<int> customerIds);

        Task<OptimaResult<TData>> GetPriceCodeTranslations<TData>(List<int> customerIds);
        Task<OptimaResult<List<PriceCodeTranslationsData>>> GetPriceCodeTranslations(List<int> customerIds);

        Task<OptimaResult<TData?>> GetPriceGroups<TData>(List<int> customerIds);
        Task<OptimaResult<List<PriceGroupData>>> GetPriceGroups(List<int> customerIds);

        Task<OptimaResult<TData>> GetPackageTranslations<TData>(List<int> customerIds);
        Task<OptimaResult<List<PackageTranslationsData>?>> GetPackageTranslations(List<int> customerIds);
        Task<OptimaResult<List<PackageTranslationsData>>> GetPackageTranslations(TranslationsPackageRequest request);

        Task<OptimaResult<TData>> GetPolicies<TData>(List<int> customerIds);
        Task<OptimaResult<List<PolicyData>>> GetPolicies(List<int> customerIds);
    }
}