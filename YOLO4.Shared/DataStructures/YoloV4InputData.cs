using Microsoft.ML.Data;

namespace YOLO4.Shared.DataStructures
{
    public class YoloV4InputData
    {
        [LoadColumn(0)]
        public string ImagePath;

        //[VectorType(1, 416, 416, 3)]
        //public byte[] ImageVector { get; set; }
    }
}
