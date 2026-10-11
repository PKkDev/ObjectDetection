namespace YOLO26.Shared.YOLOWorker.DataStructures
{
    public class YOLO26Result
    {
        public float X1, Y1, X2, Y2, Score;

        /// <summary>
        /// Идентификатор категории (индекс класса COCO)
        /// </summary>
        public int LabelId;

        /// <summary>
        /// Текстовое название категории
        /// </summary>
        public string Label = string.Empty;
    }
}
