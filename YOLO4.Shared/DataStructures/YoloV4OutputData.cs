using Microsoft.ML.Data;

namespace YOLO4.Shared.DataStructures
{
    public class YoloV4OutputData
    {
        /// <summary>
        /// Identity
        /// </summary>
        [VectorType(1, 52, 52, 3, 85)]
        [ColumnName("Identity:0")]
        public float[] Identity { get; set; }

        /// <summary>
        /// Identity1
        /// </summary>
        [VectorType(1, 26, 26, 3, 85)]
        [ColumnName("Identity_1:0")]
        public float[] Identity1 { get; set; }

        /// <summary>
        /// Identity2
        /// </summary>
        [VectorType(1, 13, 13, 3, 85)]
        [ColumnName("Identity_2:0")]
        public float[] Identity2 { get; set; }

        [ColumnName("width")]
        public float ImageWidth { get; set; }

        [ColumnName("height")]
        public float ImageHeight { get; set; }
    }
}
