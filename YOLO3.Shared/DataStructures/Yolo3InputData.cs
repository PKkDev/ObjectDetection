using Microsoft.ML.Data;

namespace YOLO3.Shared.DataStructures
{
    public class Yolo3InputData
    {
        [ColumnName("imagePath")]
        public string ImagePath;

        [ColumnName("width")]
        public float ImageWidth => 416;

        [ColumnName("height")]
        public float ImageHeight => 416;
    }
}
