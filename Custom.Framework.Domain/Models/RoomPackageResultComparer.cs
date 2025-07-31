using Custom.Domain.Optima.Models.Availability;

namespace Custom.Domain.Optima.Models;

public class RoomPackageResultComparer : IEqualityComparer<PackagesList>
{
    public bool Equals(PackagesList? x, PackagesList? y)
    {
        if (x == null || y == null)
            return false;

        return x.RoomCategory == y.RoomCategory
            && x.PackageID == y.PackageID;
    }

    public int GetHashCode(PackagesList obj)
    {
        unchecked
        {
            int hash = 17;
            foreach (var roomCode in obj.RoomCategory)
            {
                hash = hash * 23 + roomCode.GetHashCode();
            }
            hash = hash * 23 + obj.PackageID.GetHashCode();
            return hash;
        }
    }
}
