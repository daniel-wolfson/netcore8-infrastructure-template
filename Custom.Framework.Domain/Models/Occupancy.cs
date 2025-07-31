namespace Custom.Domain.Optima.Models
{
    public class Occupancy
    {
        public static Occupancy CreateFrom(int occupancyCode)
        {
            return new Occupancy
            {
                Adults = occupancyCode / 100,
                Children = occupancyCode / 10 % 10,
                Infants = occupancyCode % 10
            };
        }
        public static List<Occupancy> CreateFrom(params int[] occupancyCodes)
        {
            return occupancyCodes.Select(occupancyCode => CreateFrom(occupancyCode)).ToList();
        }

        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }

        public int OccupancyCode => Adults * 100 + Children * 10 + Infants;

        public override string ToString()
        {
            return $"{Adults}{Children}{Infants}";
        }
    }
}
