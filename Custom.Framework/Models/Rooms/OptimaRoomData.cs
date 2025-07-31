using Custom.Domain.Optima.Models.Base;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace Custom.Framework.Models.Rooms;

public class OptimaRoomData : OptimaData
{
    public string RoomCategory { get; set; }
    public int HotelID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Wing { get; set; }
    public bool Active { get; set; }
    public int SortOrder { get; set; }
    public bool IsConnectingRoom { get; set; }
    public bool MinPriceRoom { get; set; }
    public bool RequestedRoom { get; set; }
    public string PmsRoomCategory { get; set; }
    public string GlobalRoomCategory { get; set; }
    public string GlobalRoomType { get; set; }
    public string Street { get; set; }
    public string Quarter { get; set; }
    public string FullAddress { get; set; }
    public string Zip { get; set; }
    public string Longtitude { get; set; }
    public string Latitude { get; set; }

    public static OptimaRoomData Default(int hotelId, string roomCategory, string emptyTextReplacement)
    {
        return new OptimaRoomData()
        {
            HotelID = hotelId,
            RoomCategory = roomCategory,
            Name = emptyTextReplacement,
            Description = emptyTextReplacement
        };
    }
}