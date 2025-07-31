namespace Custom.Framework.Configuration.Models
{
    public class EndpointOptions
    {
        public string? ClientName { get; set; }
        public string? Password { get; set; }
        public string? UserName { get; set; }
        public string? Host { get; set; }
        public string? RootPath { get; set; }
        public int Timeout { get; set; }
        public string? Token { get; set; }
        public string? BaseOn { get; set; }
        public string? ContentType { get; set; }
    }
}