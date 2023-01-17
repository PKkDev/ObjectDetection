using Microsoft.ML;

using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Parser;
using YOLO4.Shared.Settings;
using YOLO4.Transformer;

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

MLContext mlContext = new MLContext();
OnnxModelScorer onnxModel = new(mlContext);
YoloOutputParser parser = new YoloOutputParser();

try
{
    //var imagePath = "D:\\work\\develops\\MLProjects\\ObjectDetection\\assets\\images\\image4_copy.jpg";
    var imagePath = "D:\\work\\develops\\MLProjects\\ObjectDetection\\assets\\images\\image4.jpg";

    YoloV4InputData image = new() { ImagePath = imagePath };

    //byte[] vector = GetVectorByImgPath(imagePath);
    //YoloV4InputData image = new() { ImageVector = vector };
    //SaveVectorByImg(imagePath, 416, 416, vector);

    var modelLocation = Path.Combine(Environment.CurrentDirectory, "Model\\yolov4.onnx");

    FileInfo fi = new(modelLocation);
    if (!fi.Exists) throw new Exception("yolo model not found");

    onnxModel.LoadModel(modelLocation);

    var predict = onnxModel.ScoreTest(image);
    var probability = parser.ParseOutputs(predict);

    DrawTest(imagePath, probability);

    var assetsRelativePath = @"../../../Model";
    string pathToSave = GetAbsolutePath(assetsRelativePath);
    onnxModel.Save(pathToSave);
}
catch (Exception e)
{
    Console.WriteLine(e);
}

byte[] GetVectorByImgPath(string imagePath)
{
    List<byte> listT = new List<byte>();
    Bitmap btmToPrse = new Bitmap(imagePath);
    if (btmToPrse.Height != ImageNetSettings.imageHeight || btmToPrse.Width != ImageNetSettings.imageWidth)
        btmToPrse = new Bitmap(btmToPrse, ImageNetSettings.imageWidth, ImageNetSettings.imageHeight);
    for (int y = 0; y < btmToPrse.Height; y++)
    {
        for (int x = 0; x < btmToPrse.Width; x++)
        {
            Color clr = btmToPrse.GetPixel(x, y);
            listT.Add(clr.R);
            listT.Add(clr.G);
            listT.Add(clr.B);
        }
    }
    btmToPrse.Dispose();
    return listT.ToArray();
}

void SaveVectorByImg(string imagePath, int w, int h, byte[] data)
{
    Bitmap bitmap = new Bitmap(w, h, PixelFormat.Format24bppRgb);

    int pos = 0;
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            Color c = Color.FromArgb(data[pos], data[pos + 1], data[pos + 2]);
            bitmap.SetPixel(x, y, c);
            pos += 3;
        }
    }

    var fi = new FileInfo(imagePath);
    var outputPath = Path.Combine(fi.DirectoryName, $"processed_test{fi.Extension}");
    bitmap.Save(outputPath);
    bitmap.Dispose();
}

void DrawTest(string imagePath, List<YoloV4Result> probability)
{
    System.Drawing.Image image = System.Drawing.Image.FromFile(imagePath);

    using var bitmap = new Bitmap(System.Drawing.Image.FromFile(imagePath));
    using var g = Graphics.FromImage(bitmap);

    var originalImageHeight = image.Height;
    var originalImageWidth = image.Width;

    foreach (var res in probability)
    {
        var x = res.BBox[0];
        var y = res.BBox[1];
        var width = res.BBox[2] - res.BBox[0];
        var height = res.BBox[3] - res.BBox[1];

        Console.WriteLine($"originaly - x:{x}, y:{y}, w:{width}, h:{height}, l:{res.Label}, c:{res.Confidence}");

        x = (uint)originalImageWidth * x / ImageNetSettings.imageWidth;
        y = (uint)originalImageHeight * y / ImageNetSettings.imageHeight;
        width = (uint)originalImageWidth * width / ImageNetSettings.imageWidth;
        height = (uint)originalImageHeight * height / ImageNetSettings.imageHeight;

        Console.WriteLine($"after resize - x:{x}, y:{y}, w:{width}, h:{height}, l:{res.Label}, c:{res.Confidence}");

        g.DrawRectangle(Pens.Yellow, x, y, width, height);

        // using var brushes = new SolidBrush(Color.FromArgb(50, Color.Yellow));
        // g.FillRectangle(brushes, x, y, width, height);

        string text = $"{res.Label} ({(res.Confidence * 100).ToString("0")}%)";
        g.DrawString(text, new Font("Arial", 12), Brushes.Black, new PointF(x, y));
    }

    var fi = new FileInfo(imagePath);
    var outputPath = Path.Combine(fi.DirectoryName, $"processed{fi.Extension}");
    bitmap.Save(outputPath);
}


string GetAbsolutePath(string relativePath)
{
    FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
    string assemblyFolderPath = _dataRoot.Directory.FullName;

    string fullPath = Path.Combine(assemblyFolderPath, relativePath);

    return fullPath;
}