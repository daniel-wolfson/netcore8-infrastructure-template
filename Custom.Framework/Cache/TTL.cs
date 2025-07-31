namespace Custom.Framework.Cache
{
    /// <summary> Define TTL (time-to-live) values in seconds </summary>
    public static class TTL
    {
        public const int None = 0;
        public const int Default = 1;
        public const int Timeout = 5;
        public const int OneHalfMinute = 30;
        public const int OneMinute = 60;
        public const int FiveMinutes = 300;
        public const int TenMinutes = 600;
        public const int ThirtyMinutes = 1800;
        public const int OneHour = 3600;
        public const int OneDay = 86400;
        public const int OneWeek = 604800;
        public const int OneMonth = 2592000;
        public const int OneYear = 31536000;
    }
}