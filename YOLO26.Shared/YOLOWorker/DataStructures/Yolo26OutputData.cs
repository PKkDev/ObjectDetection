using Microsoft.ML.Data;

namespace YOLO26.Shared.YOLOWorker.DataStructures
{
    public class Yolo26OutputData
    {
        /// <summary>
        /// Имя "output0" должно совпадать с именем выходного узла в ONNX
        /// Для YOLO26 One-to-One это массив 1 * 300 * 6 = 1800 элементов
        /// </summary>
        [ColumnName("output0")]
        public float[] Predictions { get; set; }
    }
}
