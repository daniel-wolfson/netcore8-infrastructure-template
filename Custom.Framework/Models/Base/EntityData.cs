using Custom.Framework.Configuration.Models;

namespace Custom.Framework.Models.Base
{
    /// <summary>
    /// EntityData - data model for use as result from opposit configuration section
    /// </summary>
    public class EntityData : DataResult
    {
        public static new EntityData MakeError(string errorMsg)
        {
            return new EntityData("ErrorInfo", true, errorMsg, null);
        }

        public EntityData()
        {
        }

        public EntityData(DataResult item) : this(item.Name, item.Error, item.Message, item.Data)
        {
        }

        public EntityData(string name, bool error, string? message, object? data)
            : base(name, error, message, data)
        {
            Data = data;
            SettingKey = ConvertToSettingsKey(name);
        }

        public EntityData(string name, bool error, string? message, object? data, object? value)
            : base(name, error, message, data, value)
        {
            SettingKey = ConvertToSettingsKey(name);
        }

        /// <summary> Named key, type of SettingKeys
        private SettingKeys _settingKey;
        public SettingKeys SettingKey
        {
            get { return _settingKey; }
            set { _settingKey = value; Name = value.ToString(); }
        }

        /// <summary>
        /// Provider - name of data provider
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// ResourceType - type of resource by SettingKey
        /// </summary>
        public string ResourceType { get; set; } = typeof(object).Name;

        private SettingKeys ConvertToSettingsKey(string name)
        {
            if (Enum.TryParse(name, out SettingKeys parsedValue))
            {
                return parsedValue;
            }
            return SettingKeys.Unknown;
        }
    }
}