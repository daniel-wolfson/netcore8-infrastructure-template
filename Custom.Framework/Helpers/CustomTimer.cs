using Custom.Framework.Configuration.Models;

namespace Custom.Framework.Helpers
{
    public class CustomTimer(SettingKeys key, string sourceType, int reloadInterval) 
        : System.Timers.Timer(reloadInterval)
    {
        public SettingKeys SettingKey { get; set; } = key;
        public string SourceType { get; set; } = sourceType;
    }
}
