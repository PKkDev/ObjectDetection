namespace YOLO3.Shared.Settings
{
    public class Yolo3NetSettings
    {
        public const int imageHeight = 416;
        public const int imageWidth = 416;
        public const int numberOfChannels = 3;
        public const int inputSize = imageHeight * imageWidth * numberOfChannels;

        public static readonly string[] Categories = new string[]
        {
            "person", "bicycle", "car", "motorbike", "aeroplane", "bus",
            "train", "truck", "boat", "traffic light", "fire hydrant",
            "stop sign", "parking meter", "bench", "bird", "cat", "dog",
            "horse", "sheep", "cow", "elephant", "bear", "zebra",
            "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase",
            "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat",
            "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl",
            "banana", "apple", "sandwich", "orange", "broccoli", "carrot",
            "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant",
            "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse",
            "remote", "keyboard", "cell phone", "microwave", "oven", "toaster",
            "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear",
            "hair drier", "toothbrush"
        };
    }
}
