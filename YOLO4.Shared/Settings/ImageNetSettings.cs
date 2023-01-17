namespace YOLO4.Shared.Settings
{
    public struct ImageNetSettings
    {
        public const int imageHeight = 416;
        public const int imageWidth = 416;
        public const int numberOfChannels = 3;
        public const int inputSize = imageHeight * imageWidth * numberOfChannels;
    }
}
