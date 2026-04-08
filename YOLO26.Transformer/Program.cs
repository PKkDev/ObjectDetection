using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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

            var imagePath = $"{workFolder}\\{imgname}.png";
            using Image<Rgb24> image = Image.Load<Rgb24>(imagePath);
            int orgW = image.Width;
            int orgH = image.Height;
            var data = yolo.PrepareImage(image);

            var detections = yolo.Predict(data, orgW, orgH);
            var imagePathProcessed = $"{workFolder}\\{imgname}-processed.png";
            yolo.SaveAnnotatedImage(imagePath, imagePathProcessed, detections);

        }
    }
}







