using SkiaSharp;
using YOLO26.Shared.YOLOWorker;

namespace YOLO26.Transformer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var workFolder = Path.Combine(Environment.CurrentDirectory, "Images");
            var imgname = "1";

            //var yoloModel = "yolo26n.onnx";
            var yoloModel = "yolo26m.onnx";
            var modelLocation = Path.Combine(Environment.CurrentDirectory, "Model", yoloModel);

            YoloPredictor yolo = new(modelLocation);
            yolo.SaveModel(yoloModel);

            var imagePath = Path.Combine(workFolder, $"{imgname}.png");
            using SKBitmap image = SKBitmap.Decode(imagePath);
            int orgW = image.Width;
            int orgH = image.Height;
            var data = Yolo26ImagePreprocessor.PrepareImage(image);

            var detections = yolo.Predict(data, orgW, orgH);
            var imagePathProcessed = Path.Combine(workFolder, $"{imgname}-processed.png");
            yolo.SaveAnnotatedImage(imagePath, imagePathProcessed, detections);
        }
    }
}