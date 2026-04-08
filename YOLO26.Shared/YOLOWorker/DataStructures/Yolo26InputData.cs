using Microsoft.ML.Data;

namespace YOLO26.Shared.YOLOWorker.DataStructures
{
    public class Yolo26InputData
    {
        /// <summary>
        /// Имя "images" должно совпадать с именем входного узла в ONNX (проверьте в Netron)
        /// </summary>
        [ColumnName("images")]
        [VectorType(1, 3, 640, 640)]
        public float[] Image { get; set; }
    }

}
