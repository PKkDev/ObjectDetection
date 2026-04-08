namespace YOLO26.Shared.CvatWorker.Model
{
    public class ParkingData(string id, List<ParkingSpace> parkingSpaces)
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        public string Id { get; set; } = id;

        public List<ParkingSpace> ParkingSpaces { get; set; } = parkingSpaces;

    }

}
