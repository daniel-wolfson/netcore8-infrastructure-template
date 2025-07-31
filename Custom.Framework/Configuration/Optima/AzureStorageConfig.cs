namespace Custom.Framework.Configuration.Optima
{
    public class AzureStorageConfig : ConfigData
    {
        public string PrimaryKey { get; set; }
        public string ContainerName { get; set; }
        public string ContainerPath { get; set; }
    }

}