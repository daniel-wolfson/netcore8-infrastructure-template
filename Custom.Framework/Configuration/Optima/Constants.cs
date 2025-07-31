namespace Custom.Framework.Configuration.Optima
{
    public class Constants : ISetting
    {
        // public const string ExtensionsConfigurationKey = "ExtensionsConfiguration";
        public const string ExtensionsConfigurationKey = "extensions";

        public int ReloadInterval => throw new NotImplementedException();
    }

    interface ISetting
    {
        int ReloadInterval { get; }
    }
}
