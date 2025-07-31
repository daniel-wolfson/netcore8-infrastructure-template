using Custom.Framework.Cache;
using Custom.Framework.Configuration.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Runtime.CompilerServices;

namespace Custom.Framework.Contracts
{
    public interface IApiCacheOptions
    {
        int GetMemoryCacheTtl(CacheKeys settingsKey,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "");

        int GetRedisCacheTtl(CacheKeys settingsKey,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "");

        /// <summary>
        /// GetMemoryCacheOptions for CacheKeys, OnPostEviction - callback which gets called when a cache entry expires.
        /// </summary>
        MemoryCacheEntryOptions GetMemoryCacheOptions(
            CacheKeys settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "");

        MemoryCacheEntryOptions GetMemoryCacheOptions(
            string settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "");

        /// <summary>
        /// GetMemoryCacheOptions for SettingKeys, OnPostEviction - callback which gets called when a cache entry expires.
        /// </summary>
        MemoryCacheEntryOptions GetMemoryCacheOptions(
            SettingKeys settingsKey,
            int reloadInterval,
            PostEvictionDelegate? OnPostEviction = null,
            [CallerFilePath] string callerFilePath = "",
            [CallerMemberName] string callerMemberName = "");
    }
}