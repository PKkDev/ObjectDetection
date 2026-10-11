using Microsoft.ML;
using SkiaSharp;

using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Parser;
using YOLO4.Shared.Settings;
using YOLO4.Transformer;


MLContext mlContext = new MLContext();
OnnxModelScorer onnxModel = new(mlContext);
Yolo4OutputParser parser = new Yolo4OutputParser();

try
{
    // путь к картинке можно передать первым аргументом, по умолчанию - assets/images в корне репозитория
    var imagePath = args.Length > 0
        ? Path.GetFullPath(args[0])
        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../assets/images/image4.jpg"));

    YoloV4InputData image = new() { ImagePath = imagePath };

    //byte[] vector = GetVectorByImgPath(imagePath);
    //YoloV4InputData image = new() { ImageVector = vector };
    //SaveVectorByImg(imagePath, 416, 416, vector);

    var modelLocation = Path.Combine(Environment.CurrentDirectory, "Model", "yolov4.onnx");

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
    using var source = SKBitmap.Decode(imagePath);

    // сразу в формат RGBA, чтобы порядок байт не зависел от платформы
    var info = new SKImageInfo(ImageNetSettings.imageWidth, ImageNetSettings.imageHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
    using var btmToPrse = source.Resize(info, SKFilterQuality.High);
    ReadOnlySpan<byte> pixels = btmToPrse.GetPixelSpan();

    List<byte> listT = new List<byte>();
    for (int y = 0; y < btmToPrse.Height; y++)
    {
        for (int x = 0; x < btmToPrse.Width; x++)
        {
            int offset = y * btmToPrse.RowBytes + x * 4;
            listT.Add(pixels[offset + 0]);
            listT.Add(pixels[offset + 1]);
            listT.Add(pixels[offset + 2]);
        }
    }
    return listT.ToArray();
}

void SaveVectorByImg(string imagePath, int w, int h, byte[] data)
{
    using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque));

    int pos = 0;
    for (int y = 0; y < h; y++)
    {
        for (int x = 0; x < w; x++)
        {
            bitmap.SetPixel(x, y, new SKColor(data[pos], data[pos + 1], data[pos + 2]));
            pos += 3;
        }
    }

    var fi = new FileInfo(imagePath);
    var outputPath = Path.Combine(fi.DirectoryName, $"processed_test{fi.Extension}");
    SaveBitmap(bitmap, outputPath);
}

void DrawTest(string imagePath, List<YoloV4Result> probability)
{
    using var bitmap = SKBitmap.Decode(imagePath);
    using var canvas = new SKCanvas(bitmap);

    // Если шрифта Arial нет (например, в Linux), Skia подставит шрифт по умолчанию
    using var typeface = SKTypeface.FromFamilyName("Arial");
    using var font = new SKFont(typeface, 16);
    using var pen = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Yellow, StrokeWidth = 1f };
    using var textPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Black, IsAntialias = true };

    var originalImageHeight = bitmap.Height;
    var originalImageWidth = bitmap.Width;

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

        canvas.DrawRect(SKRect.Create(x, y, width, height), pen);

        // Y - это базовая линия текста, поэтому сдвигаем подпись вниз на размер шрифта
        string text = $"{res.Label} ({(res.Confidence * 100).ToString("0")}%)";
        canvas.DrawText(text, x, y + font.Size, font, textPaint);
    }

    canvas.Flush();

    var fi = new FileInfo(imagePath);
    var outputPath = Path.Combine(fi.DirectoryName, $"processed{fi.Extension}");
    SaveBitmap(bitmap, outputPath);
}

void SaveBitmap(SKBitmap bitmap, string outputPath)
{
    // формат выбираем по расширению исходного файла
    var format = Path.GetExtension(outputPath).ToLowerInvariant() == ".png"
        ? SKEncodedImageFormat.Png
        : SKEncodedImageFormat.Jpeg;

    using var stream = File.Create(outputPath);
    bitmap.Encode(stream, format, 90);
}


string GetAbsolutePath(string relativePath)
{
    FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
    string assemblyFolderPath = _dataRoot.Directory.FullName;

    string fullPath = Path.Combine(assemblyFolderPath, relativePath);

    return fullPath;
}