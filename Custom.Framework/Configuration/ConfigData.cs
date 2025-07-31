using Custom.Framework.Configuration.Models;
using Custom.Framework.Contracts;
using Custom.Framework.Models.Base;
using System.ComponentModel;

namespace Custom.Framework.Configuration
{
    public class ConfigData : EntityData, IDisposable, IApiConfigurationSource
    {
        public int Id { get; set; }

        public string DependsOn { get; set; } = null!;

        public string ExternalKey { get; set; } = null!;

        public int[]? Languages { get; set; }

        /// <summary> 
        /// Named root key, type of SettingKeys
        /// </summary> 
        public SettingKeys RootSettingKey { get; set; }

        /// <summary> host</summary>
        public string? Host { get; set; }

        /// <summary> rootpath</summary>
        public string? RootPath { get; set; }

        /// <summary> Path such as url Path or as such as </summary>
        public string? Path { get; set; }

        /// <summary> password</summary>
        public required string Password { get; set; }

        /// <summary> config user name</summary>
        public required string UserName { get; set; }

        /// <summary> config token for access to</summary>
        public string? Token { get; set; }

        /// <summary> config connection string </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// timeout in a sec, it will invoke a timeout exception in the cause of long not complete async operations
        /// </summary>
        public int ConnectionTimeout { get; set; } = 5; //sec

        /// <summary>
        /// count of retry attempts on redis connection exception
        /// </summary>
        public int RetryAttempts { get; set; } = 3; //sec

        /// <summary>
        /// time interval in sec, between retry attempts
        /// </summary>
        public int RetryInterval { get; set; } = 2; //sec

        /// <summary>
        /// timeout in a sec, it will invoke a timeout exception in the cause of long not complete async operations
        /// </summary>
        public int RetryTimeout { get; set; } = 3; //sec

        /// <summary>
        /// Order of reload
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// ReloadInterval
        /// </summary>
        public int? ReloadInterval { get; set; }

        /// <summary>
        /// ReloadOnChange - is reload enable on change source
        /// </summary>
        public bool? ReloadOnChange { get; set; }

        /// <summary>
        /// ReloadTimeout - it thwrow excetion after it
        /// </summary>
        public int? ReloadTimeout { get; set; }

        /// <summary>
        /// SourceType - type of data provider by class ProviderTypes
        /// </summary>
        [DefaultValue(ProviderTypes.AzureStorage)]
        public string SourceType { get; set; } = ProviderTypes.AzureStorage;

        public string EmptyTextReplacement { get; set; } = "...";

        public void Dispose()
        {
            Data = null;
            Value = null;
        }
    }
}