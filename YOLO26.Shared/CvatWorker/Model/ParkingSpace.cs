using SixLabors.ImageSharp;

namespace YOLO26.Shared.CvatWorker.Model
{
    public class ParkingSpace(int id)
    {
        /// <summary>
        /// Идентификатор
        /// </summary>
        public int Id { get; set; } = id;

        /// <summary>
        /// 4 точки, описывающие углы трапеции Алгоритм «Point in Polygon»
        /// </summary>
        public PointF[] Points { get; set; } = new PointF[4];

        /// <summary>
        /// Занято ли место
        /// </summary>
        public bool IsOccupied { get; set; } = false;
    }

}
