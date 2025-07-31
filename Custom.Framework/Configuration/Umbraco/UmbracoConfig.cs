namespace Custom.Framework.Configuration.Umbraco
{
    public class UmbracoConfig : ConfigData
    {
        /// <summary> Get room data (name, description) </summary>
        public string RoomsPath { get; set; }
        public string UmbracoSearchSettings { get; set; }
        public string PmsSettings { get; set; }
        public string RoomSpecialRequestsCodes { get; set; }
        public string SunClubSettings { get; set; }
        public string CancelReservationBccAddresses { get; set; }
        public string ConnectingDoorReservationEmailAddresses { get; set; }
    }
}