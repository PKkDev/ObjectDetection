using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Transforms.Onnx;
using System.Drawing;
using YOLO3.Shared.DataStructures;
using YOLO3.Shared.Settings;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;
using static System.Net.Mime.MediaTypeNames;

MLContext mlContext = new();
ITransformer? model = null;

try
{
    // var yoloModel = "tiny-yolov3-11.onnx";
    // var yoloModel = "yolov3-12.onnx";
    var yoloModel = "yolov3-12-int8.onnx";
    var modelLocation = Path.Combine(Environment.CurrentDirectory, "Model", yoloModel);

    FileInfo fi = new(modelLocation);
    if (!fi.Exists) throw new Exception("yolo model not found");

    #region pipeline

    var basePipeline = mlContext.Transforms
        .LoadImages(
        outputColumnName: "image",
        imageFolder: "",
        inputColumnName: "imagePath")
        .Append(mlContext.Transforms.ResizeImages(
            inputColumnName: "image",
            outputColumnName: "input_1",
            imageWidth: Yolo3NetSettings.imageWidth,
            imageHeight: Yolo3NetSettings.imageHeight,
            resizing: ResizingKind.Fill))
        .Append(mlContext.Transforms.ExtractPixels(
            outputColumnName: "input_1",
            scaleImage: 1f / 255f,
            interleavePixelColors: false))
        .Append(mlContext.Transforms.Concatenate(
            outputColumnName: "image_shape",
            inputColumnNames: new string[] { "height", "width" }));

    EstimatorChain<OnnxTransformer> pipeline = basePipeline
        .Append(mlContext.Transforms.ApplyOnnxModel(
            shapeDictionary: new Dictionary<string, int[]>()
            {
                {  "input_1", new[] { 1, 3, 416, 416 } }
            },
            inputColumnNames: new[]
            {
                "input_1",
                "image_shape"
            },
            //outputColumnNames: new[]
            //{
            //    "yolonms_layer_1",
            //    "yolonms_layer_1:1",
            //    "yolonms_layer_1:2"
            //},
            outputColumnNames: new[]
            {
                "yolonms_layer_1/ExpandDims_1:0",
                "yolonms_layer_1/ExpandDims_3:0",
                "yolonms_layer_1/concat_2:0"
            },
            modelFile: modelLocation,
            recursionLimit: 100));

    #endregion pipeline

    IDataView emptyData = mlContext.Data.LoadFromEnumerable(new List<Yolo3InputData>());
    model = pipeline.Fit(emptyData);

    PredictionEngine<Yolo3InputData, Yolo3OutputData> predictionEngine = mlContext.Model.CreatePredictionEngine<Yolo3InputData, Yolo3OutputData>(model);
    var imagePath = "D:\\work\\develops\\MLProjects\\ObjectDetection\\assets\\images\\image4.jpg";
    using Bitmap btm = new(imagePath);
    Yolo3InputData image = new() { ImagePath = imagePath, };
    Yolo3OutputData predict = predictionEngine.Predict(image);

    var probability = GetResults(predict, Yolo3NetSettings.Categories);

    DrawTest(imagePath, probability.ToList());

}
catch (Exception e)
{
    Console.WriteLine(e);
}
finally
{
    Console.ReadKey();
}

static IReadOnlyList<Yolo3Result> GetResults(Yolo3OutputData prediction, string[] categories)
{
    if (prediction.Concat == null || prediction.Concat.Length == 0)
        return new List<Yolo3Result>();

    //if (prediction.Boxes.Length != Yolo3OutputData.YoloV3BboxPredictionCount * 4)
    //    throw new ArgumentException();

    //if (prediction.Scores.Length != Yolo3OutputData.YoloV3BboxPredictionCount * categories.Length)
    //    throw new ArgumentException();

    List<Yolo3Result> results = new List<Yolo3Result>();

    // Concat size is 'nbox'x3 (batch_index, class_index, box_index)
    int resulstCount = prediction.Concat.Length / 3;
    for (int c = 0; c < resulstCount; c++)
    {
        var res = prediction.Concat.Skip(c * 3).Take(3).ToArray();

        var batch_index = res[0];
        var class_index = res[1];
        var box_index = res[2];

        var label = categories[class_index];
        var bbox = new float[]
        {
            prediction.Boxes[box_index * 4],
            prediction.Boxes[box_index * 4 + 1],
            prediction.Boxes[box_index * 4 + 2],
            prediction.Boxes[box_index * 4 + 3],
        };
        var score = prediction.Scores[box_index + class_index * Yolo3OutputData.YoloV3BboxPredictionCount];

        results.Add(new Yolo3Result(bbox, label, score));
    }

    return results;
}

static void DrawTest(string imagePath, List<Yolo3Result> probability)
{
    using var bitmap = new Bitmap(imagePath);
    using var g = Graphics.FromImage(bitmap);

    var originalImageHeight = bitmap.Height;
    var originalImageWidth = bitmap.Width;

    foreach (var res in probability)
    {
        var x = res.BBox[1];
        var y = res.BBox[0];
        var width = res.BBox[3] - res.BBox[1];
        var height = res.BBox[2] - res.BBox[0];

        x = (uint)originalImageWidth * x / Yolo3NetSettings.imageWidth;
        y = (uint)originalImageHeight * y / Yolo3NetSettings.imageHeight;
        width = (uint)originalImageWidth * width / Yolo3NetSettings.imageWidth;
        height = (uint)originalImageHeight * height / Yolo3NetSettings.imageHeight;

        g.DrawRectangle(Pens.Yellow, x, y, width, height);
        string text = $"{res.Label} ({(res.Confidence * 100).ToString("0")}%)";
        g.DrawString(text, new Font("Arial", 12), Brushes.Black, new PointF(x, y));
    }

    var fi = new FileInfo(imagePath);
    var outputPath = Path.Combine(fi.DirectoryName, $"processed{fi.Extension}");
    bitmap.Save(outputPath);
}

/*

name (Parameter 'Ouput tensor, yolonms_layer_1/ExpandDims_1:0, does not exist in the ONNX model.
Available output names are [yolonms_layer_1,yolonms_layer_1:1,yolonms_layer_1:2].')
Actual value was yolonms_layer_1/ExpandDims_1:0.


Could not find input column 'image_shape' (Parameter 'inputSchema')

 */