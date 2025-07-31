using Custom.Domain.Optima.Models.Availability;
using Custom.Domain.Optima.Models.Customer;
using Custom.Domain.Optima.Models.Main;
using Custom.Domain.Optima.Models.Umbraco;
using Custom.Framework.Configuration.Models;
using Custom.Framework.Configuration.Optima;
using Custom.Framework.Configuration.Umbraco;
using Custom.Framework.Contracts;
using Custom.Framework.Core;
using Custom.Framework.Exceptions;
using Custom.Framework.Helpers;
using Custom.Framework.Models;
using Custom.Framework.Models.Base;
using Custom.Framework.Models.Errors;
using Custom.Framework.StaticData.Contracts;
using Custom.Framework.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Text.Json;


namespace Custom.Framework.StaticData.DbContext
{
    /// <summary> 
    /// POC - ApiSettings DbContext 
    /// it will contains all static settings table and them links into local memory of EF
    /// </summary>
    public class OptimaContext : Microsoft.EntityFrameworkCore.DbContext
    {
        private readonly ConfigurationOptions _configurationOptions;
        private readonly IServiceScopeFactory _serviceFactory;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IStaticDataService _staticData;
        private readonly TimeSpan _defaultTtl; // Default TTL for cached values
        private readonly ConcurrentDictionary<string, DateTime> _cacheExpiry; // Expiry times for entities

        public OptimaContext(
                ILogger logger,
                IConfiguration configuration,
                IOptions<IApiSettings> apisettings,
                DbContextOptions<OptimaContext> options,
                ConfigurationOptions configurationOptions,
                IServiceScopeFactory serviceFactory,
                IStaticDataService staticData) : base(options)
        {
            _logger = logger;
            _configuration = configuration;
            _configurationOptions = configurationOptions;
            _serviceFactory = serviceFactory;
            _staticData = staticData;
            _defaultTtl = TimeSpan.FromSeconds(apisettings.Value.Optima.CacheMemoryReloadTTL);
            UseMemoryCache = apisettings.Value.Optima.UseMemoryCache;
        }

        public StaticDataCollection<EntityData> StaticData => _staticData.DataContext;
        public List<ErrorInfo> EntityErrors { get; set; } = [];

        /// <summary>
        /// UseMemoryCache - use memory cache, default is value UseMemoryCache from app settings
        /// </summary>
        public bool UseMemoryCache { get; set; }

        public DbSet<UmbracoSettings> UmbracoSearchSettings { get; set; }
        public DbSet<OptimaSettings> OptimaSettings { get; set; }
        public DbSet<PmsSettings> PmsSettings { get; set; }
        public DbSet<CodesConversion> CodesConversion { get; set; }
        public DbSet<HotelCodes> HotelCodes { get; set; }
        public DbSet<RoomCodes> RoomCodes { get; set; }
        public DbSet<CurrencyRate> CurrencyRates { get; set; }

        public DbSet<HotelData> Hotels { get; set; }
        public DbSet<RegionData> Regions { get; set; }
        public DbSet<PackageShowData> PackageShow { get; set; }
        public DbSet<ClerkData> Clerks { get; set; }
        public DbSet<RoomData> Rooms { get; set; }
        public DbSet<PlanData> Plans { get; set; }
        public DbSet<PriceGroupData> PriceGroups { get; set; }
        public DbSet<PriceCodeData> PriceCodes { get; set; }

        public DbSet<PriceCodeTranslationsData> PriceCodeTranslations { get; set; }
        public DbSet<PriceCodeCategoryData> PriceCodeCategories { get; set; }
        public DbSet<PriceCodeCategoryLinkData> PriceCodeCategoryLinks { get; set; }
        public DbSet<PackageTranslationsData> PackageTranslations { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            try
            {
                using var scope = _serviceFactory.CreateScope();
                var optimaMapper = scope.ServiceProvider.GetService<IApiOptimaDataMapper>()
                    ?? throw new ApiException(ServiceStatus.FatalError, "ApiOptimaDataMapper is not defined");

                // optimaSettings
                var optimaSettings = StaticData.GetValueOrDefault<OptimaSettings>(SettingKeys.OptimaSettings)
                    ?? throw new ApiException(ServiceStatus.FatalError, "OptimaSettings is not defined");

                optimaSettings.CodesConversion = optimaSettings.CodesConversion
                    .Where(x => !string.IsNullOrEmpty(x.HotelCode?.G4Code)
                        && !string.IsNullOrEmpty(x.HotelCode?.GeneralCode))
                    .ToList();

                //UmbracoSettings
                var rootNodeIds = StaticData.GetValueOrDefault<IEnumerable<int>>(SettingKeys.RootNodeIds)?.ToList() ?? [];
                var umbracoSettings = StaticData.GetValueOrDefault<List<UmbracoSettings>>(SettingKeys.UmbracoSettings)?
                    .Where(x => rootNodeIds.Contains(x.Id))
                    ?? throw new ApiException("UmbracoSettings is not defined"); ;

                modelBuilder.Entity<UmbracoSettings>(
                    ent =>
                    {
                        ent.HasKey(x => x.Id);
                        ent.Property(x => x.Id).ValueGeneratedOnAdd();//.UseIdentityColumn(seed: 1, increment: 1);
                        ent.OwnsMany(p => p.RoomsFilter).Ignore(x => x.Rooms);
                        ent.OwnsMany(p => p.AvailableHotelsAndRooms);
                        //ent.OwnsMany(p => p.AlternativeHotelsOptions);
                        ent.OwnsMany(p => p.ConnectingDoorSettings);
                        ent.OwnsMany(p => p.RoomSeparateCombinations).Ignore(x => x.Combinations);
                        //ent.Ignore(x => x.AlternativeHotelsOptions);
                        ent.Ignore(x => x.AvailableHotelsAndRooms);
                        ent.Ignore(p => p.ConnectingDoorSettings);
                        ent.Ignore(p => p.RoomSeparateCombinations);
                        ent.Ignore(p => p.RoomsFilter);
                        ent.HasData(umbracoSettings);
                    });

                // CodesConversions
                var hotelCodesAndRooms = optimaSettings.CodesConversion
                    .Select(x =>
                    {
                        x.CodesConversionId = Guid.NewGuid();
                        x.HotelCode.Id = x.CodesConversionId;
                        x.HotelCode.HotelCode = x.HotelCode.GeneralCode;
                        var HotelCode = x.HotelCode.HotelCode;
                        var Rooms = x.Rooms.Select(r =>
                        {
                            r.HotelCode = HotelCode;
                            r.Id = Guid.NewGuid();
                            return r;
                        }).ToList();
                        return (x.HotelCode, Rooms);
                    }).ToList();

                var hotelCodes = hotelCodesAndRooms.Select(x => x.HotelCode).ToList();
                var roomCodes = hotelCodesAndRooms.SelectMany(x => x.Rooms).ToList();

                // HotelCodes
                modelBuilder.Entity<HotelCodes>(
                    entity =>
                    {
                        entity.HasKey(x => x.Id);
                        entity.HasData(hotelCodes);

                    });

                // RoomCodes
                modelBuilder.Entity<RoomCodes>(
                    entity =>
                    {
                        entity.HasKey(x => x.Id);
                        entity.HasData(roomCodes);
                    });

                // PriceGroups
                var priceGroups = StaticData.GetValueOrDefault<List<PriceGroupData>>(SettingKeys.PriceGroups)
                    .Where(x => !string.IsNullOrEmpty(x.PriceGroupId))?.ToList() ?? [];
                if (priceGroups.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PriceGroups.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PriceGroupData)} empty"));
                modelBuilder.Entity<PriceGroupData>(
                    entity =>
                    {
                        entity.HasKey(x => new { x.PriceGroupId, x.HotelID, x.CustomerId });
                        entity.HasData(priceGroups);
                    });

                var clerks = StaticData.GetValueOrDefault<List<ClerkData>>(SettingKeys.Clerks)?.ToList() ?? [];
                if (clerks.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Clerks.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(ClerkData)} empty"));
                modelBuilder.Entity<ClerkData>(
                    entity =>
                    {
                        entity.HasKey(x => new { x.ClerkKey, x.HotelID });
                        entity.HasData(clerks);
                    });

                // Configure CodesConversion - Not delete
                //modelBuilder.Entity<Hotel>(entity =>
                //{
                //    entity.HasKey(e => e.HotelID);
                //    entity.OwnsOne(e => e.HotelCode);
                //    entity.OwnsMany(e => e.Rooms);
                //    //      .WithOne()
                //    //      .HasForeignKey(r => r.HotelID); // Or another key as appropriate
                //});

                // Rooms
                var roomData = umbracoSettings
                    .SelectMany(x => x.AvailableHotelsAndRooms
                        .Where(ap => !string.IsNullOrEmpty(ap.HotelCode)) // Ensure HotelCode is not null or empty
                        .SelectMany(ap => ap.RoomCodes
                            .Select(rc =>
                                new RoomData
                                {
                                    HotelCode = ap.HotelCode,
                                    Code = rc,
                                    RoomFilters =
                                        umbracoSettings
                                            .SelectMany(us => us.RoomsFilter)
                                            .Where(rf => rf.HotelCode == ap.HotelCode)
                                            .SelectMany(rf => rf.Rooms)
                                            .Where(r => r.RoomCode == rc)
                                            .Select(r => r.Adults * 100 + r.Children * 10 + r.Infants).ToList()
                                })))
                    .ToList();

                if (roomData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Rooms.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(RoomData)} empty"));

                modelBuilder.Entity<RoomData>(ent =>
                {
                    // Configure properties
                    ent.HasKey(p => new { p.HotelCode, p.CustomerId });
                    ent.Property(p => p.Code);
                    ent.Property(p => p.Name);
                    ent.Property(p => p.Description);
                    ent.Property(p => p.HotelCode);
                    ent.Property(p => p.RoomFilters);
                    ent.Property(p => p.CodeSource);
                    ent.Property(p => p.ImageUrl).IsRequired(false);
                    ent.Property(p => p.MoreInfo).IsRequired(false);

                    // Configure the relationship
                    ent.HasOne(r => r.Hotel)                // Navigation property in RoomData
                        .WithMany(h => h.Rooms)             // Navigation property in HotelData
                        .HasForeignKey(r => r.HotelCode)    // Foreign key in RoomData
                        .HasPrincipalKey(h => h.HotelCode); // Alternate key in HotelData

                    // Seed data
                    ent.HasData(roomData);
                });

                // Configuring the Hotels entity
                var hotels = StaticData.GetValueOrDefault<IEnumerable<HotelData>>(SettingKeys.Hotels)?
                    .Select(x => { x.HotelCode = optimaMapper.MapToHotelCode(x.HotelID); return x; })
                    .Where(x => !string.IsNullOrEmpty(x.HotelCode))
                    .ToList() ?? [];
                if (hotels.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Hotels.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PlanData)} empty"));

                modelBuilder.Entity<HotelData>(
                    entity =>
                    {
                        entity.HasKey(e => new { e.HotelID, e.CustomerId });
                        entity.HasIndex(h => h.HotelCode);
                        entity.HasAlternateKey(e => e.HotelCode);
                        entity.Property(p => p.HotelCode);
                        entity.Property(p => p.RoomCategory).IsRequired(false);
                        entity.Property(p => p.PlanCode).IsRequired(false);
                        entity.Property(p => p.Wing).IsRequired(false);
                        entity.Property(p => p.PmsHotelCode).IsRequired(false);
                        entity.HasMany(p => p.Rooms);
                        entity.HasData(hotels);
                    });

                if (hotels.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Hotels.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(HotelData)} empty"));

                // Plans
                var planCodeValidator = scope.ServiceProvider.GetService<IOptimaPlanCodeValidator>()!;
                var optimaDataMapper = scope.ServiceProvider.GetService<IApiOptimaDataMapper>()!;
                var plans = StaticData.GetValueOrDefault<IEnumerable<PlanData>>(SettingKeys.Plans)?
                    .Select(x => { x.BoardBaseCode = optimaDataMapper.MapToBoardbase(x); return x; })
                    .Where(x => planCodeValidator.IsPlanCodeValid(x))
                    .ToList() ?? [];
                if (plans.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Plans.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PlanData)} empty"));
                modelBuilder.Entity<PlanData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PlanCode, p.HotelID, p.CustomerId });
                        ent.Property(p => p.HotelID);
                        ent.Property(p => p.Name);
                        ent.Property(p => p.BoardBaseCode);
                        ent.Property(p => p.Description);
                        ent.Property(p => p.GlobalPlanCode).IsRequired(false);
                        ent.Property(p => p.OtaMealPlanCode).IsRequired(false);
                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, new JsonSerializerOptions()));
                        ent.HasData(plans);
                    });

                // PriceCodes
                var priceCodeData = StaticData.GetValueOrDefault<IEnumerable<PriceCodeData>>(SettingKeys.PriceCodes)?.ToList() ?? [];
                if (priceCodeData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PriceCodes.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PriceCodeData)} empty"));
                modelBuilder.Entity<PriceCodeData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PriceCode, p.HotelID, p.InternetPriceCode, p.CustomerId });
                        ent.Property(p => p.PriceCode).IsRequired();
                        ent.Property(p => p.HotelID).IsRequired();
                        ent.Property(p => p.Description).IsRequired(false);
                        ent.Property(p => p.ReasonCode).IsRequired(false);
                        ent.Property(p => p.OtaRateType).IsRequired(false);
                        ent.Property(p => p.PolicyCode).IsRequired(false);
                        ent.Property(p => p.PriceGroupId).IsRequired(false);
                        ent.Property(p => p.InternetPriceCode);
                        ent.Property(p => p.IsPromoCodeOnly);
                        ent.Property(p => p.IsOverRideClubDiscount);
                        ent.Property(p => p.DiscountPercent);
                        ent.Property(p => p.Order);
                        ent.Property(p => p.IsLocal);
                        ent.Property(p => p.NotAllowedForCustomerGroupID);
                        ent.Property(p => p.StartDate);
                        ent.Property(p => p.EndDate);
                        ent.Property(p => p.DefaultLeadDays);
                        ent.Property(p => p.DefaultCancelDays);
                        ent.Property(p => p.CommissionPercent);
                        ent.Property(p => p.IsNetPrice);

                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                        ent.HasData(priceCodeData);
                    });

                // PriceCodeTranslations
                var priceCodeTranslationsData = StaticData.GetValueOrDefault<IEnumerable<PriceCodeTranslationsData>>(
                    SettingKeys.PriceCodeTranslations)?
                    .GroupBy(p => new { p.PriceCode, p.HotelID, p.LanguageID, p.CustomerId })
                    .Select(g => g.First())
                    .ToList() ?? [];

                if (priceCodeTranslationsData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PriceCodeTranslations.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PriceCodeTranslationsData)} empty"));
                modelBuilder.Entity<PriceCodeTranslationsData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PriceCode, p.HotelID, p.LanguageID, p.CustomerId });
                        ent.Property(p => p.HotelID).IsRequired();
                        ent.Property(p => p.PriceCode).IsRequired();
                        ent.Property(p => p.LanguageID).IsRequired();
                        ent.Property(p => p.Name);
                        ent.Property(p => p.Description);
                        ent.Property(p => p.Picture1Url).IsRequired(false);
                        ent.Property(p => p.Picture2Url).IsRequired(false);
                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                        ent.HasData(priceCodeTranslationsData);
                    });

                // PriceCodeCategories
                var priceCodeCategoriesData = StaticData.GetValueOrDefault<IEnumerable<PriceCodeCategoryData>>(
                    SettingKeys.PriceCodeCategories)?.ToList() ?? [];
                if (priceCodeCategoriesData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PriceCodeCategories.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PriceCodeCategoryData)} empty"));
                modelBuilder.Entity<PriceCodeCategoryData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PriceCodeCategoryID, p.HotelID, p.CustomerId });
                        ent.Property(p => p.HotelID).IsRequired();
                        ent.Property(p => p.PriceCodeCategoryID).IsRequired();
                        ent.Property(p => p.Description);
                        ent.Property(p => p.ShortDescription);
                        ent.Property(p => p.Delete);
                        ent.Property(p => p.DisplayOrder);
                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                        ent.HasData(priceCodeCategoriesData);
                    });

                // PriceCodeCategories
                var priceCodeCategoryLinkData = StaticData.GetValueOrDefault<IEnumerable<PriceCodeCategoryLinkData>>(
                    SettingKeys.PriceCodeCategoryLinks)?
                        .Where(x => !string.IsNullOrEmpty(x.PriceCode)).ToList() ?? [];

                if (priceCodeCategoryLinkData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PriceCodeCategoryLinks.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PriceCodeCategoryLinkData)} empty"));

                modelBuilder.Entity<PriceCodeCategoryLinkData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PriceCodeCategoryID, p.PriceCode, p.HotelID, p.CustomerId });
                        ent.Property(p => p.PriceCodeCategoryID).IsRequired();
                        ent.Property(p => p.HotelID).IsRequired();
                        ent.Property(p => p.PriceCode).IsRequired(); ;
                        ent.HasData(priceCodeCategoryLinkData);
                    });

                // PackageShow
                var packageShowData = StaticData.GetValueOrDefault<IEnumerable<PackageShowData>>(
                    SettingKeys.PackageShow)?.ToList() ?? [];
                if (packageShowData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.PackageShow.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(PackageShowData)} empty"));
                modelBuilder.Entity<PackageShowData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PackageID, p.CustomerId });
                        ent.Property(p => p.HotelID);
                        ent.Property(p => p.Name).IsRequired(false);
                        ent.Property(p => p.Description).IsRequired(false);
                        ent.Property(p => p.CurrencyCode).IsRequired(false);
                        ent.Property(p => p.RoomCategory).IsRequired(false);
                        ent.Property(p => p.ComparisonPriceCode).IsRequired(false);
                        ent.Property(p => p.Wing).IsRequired(false);
                        ent.Property(p => p.PlanCode).IsRequired(false);
                        ent.Property(p => p.Picture1Url).IsRequired(false);
                        ent.Property(p => p.Picture2Url).IsRequired(false);
                        ent.Property(p => p.StartTime).IsRequired(false);
                        ent.Property(p => p.EndTime).IsRequired(false);
                        ent.Property(p => p.PriceGroup).IsRequired(false);
                        ent.Property(p => p.ShortDescription).IsRequired(false);
                        ent.OwnsMany(p => p.CategriesList);
                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                        ent.HasData(packageShowData);
                    });

                // PackageTraslates
                var packageTranslationsData = StaticData.GetValueOrDefault<IEnumerable<PackageTranslationsData>>(
                    SettingKeys.PackageTranslations)?
                    .GroupBy(p => new { p.PackageID, p.HotelID, p.LanguageID })
                    .Select(g => g.First())
                    .ToList() ?? [];
                if (packageTranslationsData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation($"{ApiHelper.LogTitle()}. Entity {nameof(packageTranslationsData)} empty"));
                modelBuilder.Entity<PackageTranslationsData>(
                    ent =>
                    {
                        ent.HasKey(p => new { p.PackageID, p.HotelID, p.LanguageID });
                        ent.Property(p => p.PackageID).IsRequired();
                        ent.Property(p => p.HotelID).IsRequired();
                        ent.Property(p => p.LanguageID).IsRequired();
                        ent.Property(p => p.Name).IsRequired();
                        ent.Property(p => p.Description).IsRequired();
                        ent.Property(p => p.ShortDescription).IsRequired(false);
                        ent.Property(p => p.Pic1URL).IsRequired(false);
                        ent.Property(p => p.Pic2URL).IsRequired(false);
                        ent.Property(p => p.GenericParameters)
                            .IsRequired(false)
                            .HasConversion(
                                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null));
                        ent.HasData(packageTranslationsData);
                    });

                // Regions
                var regionData = StaticData.GetValueOrDefault<IEnumerable<RegionData>>(SettingKeys.Regions)?.ToList() ?? [];
                if (regionData.Count == 0)
                    EntityErrors.Add(ErrorInfo.Validation(SettingKeys.Regions.ToString(), $"{ApiHelper.LogTitle()}. Entity {nameof(RegionData)} empty"));
                modelBuilder.Entity<RegionData>(ent =>
                {
                    ent.HasKey(p => new { p.RegionID, p.ChainID });
                    ent.Property(p => p.RegionID).IsRequired();
                    ent.Property(p => p.ChainID).IsRequired();
                    ent.Property(p => p.RegionName).IsRequired();
                    ent.Property(p => p.PmsRegionName).IsRequired();
                    ent.Property(p => p.PmsRegionCode).IsRequired();
                    ent.HasData(regionData);
                });

                modelBuilder.Entity<CategoriesList>().HasNoKey();
                modelBuilder.Entity<AvailableProperties>().HasNoKey();
                modelBuilder.Entity<AlternativeHotelsOption>().HasNoKey();
                modelBuilder.Entity<CustomerIds>().HasNoKey();
                modelBuilder.Entity<CrmSettings>().HasNoKey();
                modelBuilder.Entity<PmsSettings>().HasNoKey();

                base.OnModelCreating(modelBuilder);

                //var entityTypes = modelBuilder.Model.GetEntityTypes();
                //foreach (var entityType in entityTypes)
                //{
                //    Console.WriteLine(entityType.Name);
                //}
            }
            catch (Exception ex)
            {
                Log.Logger.Error("{TITLE} exception: {EXCEPTION}. \nStackTrace: {StackTrace}\n",
                    ApiHelper.LogTitle(), ex.InnerException?.Message ?? ex.Message, ex.StackTrace);
            }
        }

        public IServiceProvider GetInternalServiceProvider()
        {
            return this.GetInfrastructure();
        }

        public void Initialize()
        {
            // Trigger `OnConfiguring`.
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging(true);
            base.OnConfiguring(optionsBuilder);
        }

        /// <summary> Initialize with SeedData </summary>
        public bool Initialize(List<EntityData> data)
        {
            // ensure create database
            //var isCreated = Dal.EnsureCreated();

            var settingTasks = new List<Task>()
            {
                //hotelDataTask,
                //optimaDataProvider.GetAsync2<List<RegionData>>(SettingKeys.Regions)
                //    .ContinueWith((x) => ServiceResult<List<object>>.Ok(x.Result.Title?.Select(xx => (object)xx).ToList())),
                //optimaDataProvider.GetAsync2<List<PackageShow>>(SettingKeys.PackageShow),
                //optimaDataProvider.GetAsync2<List<RoomData>>(SettingKeys.Rooms),
                //optimaDataProvider.GetAsync2<List<PlanData>>(SettingKeys.Plans),
                //optimaDataProvider.GetAsync2<List<PriceCodeData>>(SettingKeys.PriceCodes),
                //optimaDataProvider.GetAsync2<List<PriceCodeCategoryData>>(SettingKeys.PriceCodeCategories),
                //optimaDataProvider.GetAsync2<List<PriceCodeCategoryLinkData>>(SettingKeys.PriceCodeCategoryLinks),
                //optimaDataProvider.GetAsync2<List<PriceCodeCategoryData>>(SettingKeys.PriceCodeTranslations),
                //optimaDataProvider.GetAsync2<List<PackageTranslateData>>(SettingKeys.PackageTranslations),
                //blobDataProvider.GetAsync2<List<UmbracoSettings>>(SettingKeys.UmbracoSettings)
                //    .ContinueWith((completedTask, modelBuilder) =>
                //    {
                //        //var results = completedTask.Result;
                //        //if (!results.error)
                //        //{
                //        //    int nextId = 1;
                //        //    results.objectResult.ForEach(item => item.Id = nextId++);

                //        //    var builder = (ModelBuilder)modelBuilder!;
                //        //    builder.Entity<UmbracoSettings>(
                //        //        ent =>
                //        //        {
                //        //            ent.HasKey(x => x.Id);
                //        //            ent.Property(x => x.Id).ValueGeneratedOnAdd();//.UseIdentityColumn(seed: 1, increment: 1);
                //        //            ent.OwnsMany(p => p.RoomsFilter).Ignore(x => x.Rooms);
                //        //            ent.OwnsMany(p => p.AvailableHotelsAndRooms);
                //        //            //ent.OwnsMany(p => p.AlternativeHotelsOptions);
                //        //            ent.OwnsMany(p => p.ConnectingDoorSettings);
                //        //            ent.OwnsMany(p => p.RoomSeparateCombinations).Ignore(x => x.Combinations);
                //        //            //ent.Ignore(x => x.AlternativeHotelsOptions);
                //        //            ent.Ignore(x => x.AvailableHotelsAndRooms);
                //        //            ent.Ignore(p => p.ConnectingDoorSettings);
                //        //            ent.Ignore(p => p.RoomSeparateCombinations);
                //        //            ent.Ignore(p => p.RoomsFilter);
                //        //            ent.HasData(results.objectResult);
                //        //        });

                //        //    //builder.Entity<AlternativeHotelsOption>(
                //        //    //    ent =>
                //        //    //    {
                //        //    //        ent.HasNoKey();
                //        //    //        ent.HasData(results.objectResult.SelectMany(x => x.AlternativeHotelsOptions));
                //        //    //    });
                //        //}
                //    }, modelBuilder),
                //    blobDataProvider.GetAsync2<OptimaSettings>(SettingKeys.OptimaSettings)
                //.ContinueWith((completedTask, modelBuilder) =>
                //{
                //    //var results = completedTask.Result;
                //    //if (!results.error)
                //    //{
                //    //    var builder = (ModelBuilder)modelBuilder!;
                //    //    results.objectResult.OptimaSettingsId = 1;
                //    //    int nextId = 1;

                //    //    results.objectResult.CodesConversion.ForEach(hotel =>
                //    //        {
                //    //            hotel.HotelID = nextId++;
                //    //            hotel.OptimaSettingsId = 1;
                //    //            hotel.OptimaSettings = results.objectResult;
                //    //        });
                //    //    nextId = 1;
                //    //    results.objectResult.SitesSettings.ForEach(sitesSettings =>
                //    //    {
                //    //        sitesSettings.OptimaSiteSettingsId = nextId++;
                //    //        sitesSettings.OptimaSettingsId = 1;
                //    //        sitesSettings.OptimaSettings = results.objectResult;
                //    //    });

                //    //    builder.Entity<Hotel>()
                //    //       .HasIndex(p => p.OptimaSettingsId)
                //    //       .HasDatabaseName("OptimaSettingsIndex");

                //    //    builder.Entity<OptimaSettings>(
                //    //        ent =>
                //    //        {
                //    //            ent.HasKey(o => o.OptimaSettingsId);
                //    //            ent.Ignore(p => p.CodesConversion);
                //    //            ent.Ignore(p => p.SitesSettings);
                //    //            //ent.HasMany(o => o.CodesConversion)
                //    //            //   .WithOne(h => h.OptimaSettings)
                //    //            //   .HasForeignKey(h => h.HotelID);
                //    //            //ent.HasMany(o => o.SitesSettings)
                //    //            //   .WithOne(h => h.OptimaSettings)
                //    //            //   .HasForeignKey(h => h.OptimaSiteSettingsId);
                //    //            ent.HasData(results.objectResult);
                //    //        });
                //    //}
                //}, modelBuilder)
            };

            return true;
        }

        public void Migrate(OptimaContext dbContext)
        {
            //dbContext.Dal.Migrate();
            //var currentMigrations = dbContext.Dal.GetMigrations().ToList();
            //var pendingMigrations = dbContext.Dal.GetPendingMigrations().ToList();

            ////// apply pending migrations
            //if (pendingMigrations.Any())
            //{
            //    try
            //    {
            //        dbContext.Dal.Migrate();
            //    }
            //    catch (Exception ex)
            //    {
            //    }
            //}
        }

        public void SeedData()
        {
            // Ensure that migrations are applied
            //Dal.Migrate();

            // Save changes to persist seeded data
            //SaveChanges();

        }

        /// <summary>
        /// Fetches data from Local cache or reloads if TTL has expired.
        /// </summary>
        public async Task<List<TEntity>> GetWithCacheAsync<TEntity>(DbSet<TEntity> dbSet,
            Func<TEntity, bool> predicate,
            string cacheKey,
            TimeSpan? ttl = null) where TEntity : class
        {
            // Determine expiration time
            var expirationTime = ttl ?? _defaultTtl;

            // Check if cache is still valid
            if (_cacheExpiry.TryGetValue(cacheKey, out var expiryTime) && DateTime.UtcNow < expiryTime)
            {
                // Return data from Local cache if still valid
                return dbSet.Local.Where(predicate).ToList(); // ToListAsync
            }

            // Reload data from database
            var data = dbSet.Where(predicate).ToList();

            // Update Local cache and TTL
            _cacheExpiry[cacheKey] = DateTime.UtcNow.Add(expirationTime);

            return data;
        }
    }

    public class DataSeeder
    {
        private readonly OptimaContext _context;
        private readonly IApiHttpClientFactory _apiHttpClientFactory;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DataSeeder(OptimaContext context,
            IApiHttpClientFactory apiHttpClientFactory,
            IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _apiHttpClientFactory = apiHttpClientFactory;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task SeedDataAsync()
        {
            // Fetch data from external API
            var client = _apiHttpClientFactory.CreateClient(ApiHttpClientNames.OptimaAvailabilityApi);
            var scope = _serviceScopeFactory.CreateAsyncScope();
            var optimaRepo = scope.ServiceProvider.GetService<IOptimaBaseRepository>()
                    ?? throw new ApiException("Repo not defined");
            var response = await client.GetAsync("https://api.example.com/data");
            //if (!response.IsSuccessStatusCode)
            //    throw new Exception("Failed to fetch data from the external API.");

            //var responseContent = await response.Content.ReadAsStringAsync();
            //var responseData = System.Text.Json.JsonSerializer.GetObjectOrDefault<List<HotelData>>(responseContent);


            //// Seed database with fetched data
            //foreach (var data in responseData)
            //{
            //    _context.Hotels.Add(data);
            //}

            await _context.SaveChangesAsync();
        }
    }

    public class ApiDbContextOptions : DbContextOptions
    {
        public override Type ContextType => typeof(OptimaContext);

        public IServiceProvider ServiceProvider { get; set; } = default!;

        public IApiOptimaDataMapper? OptimaDataMapper => ServiceProvider?.GetService<IApiOptimaDataMapper>();

        public override DbContextOptions WithExtension<TExtension>(TExtension extension)
        {
            return this;
        }
    }
}
