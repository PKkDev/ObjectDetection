using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.Transforms.Onnx;
using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Settings;
using static Microsoft.ML.Transforms.Image.ImageResizingEstimator;

namespace YOLO4.Transformer
{
    public class OnnxModelScorer
    {
        private readonly MLContext _mlContext;

        private ITransformer _model;

        public OnnxModelScorer(MLContext mlContext)
        {
            _mlContext = mlContext;
        }

        public ITransformer LoadModel(string modelLocation)
        {
            var basePipeline = GetTransformerForImagePath();
            // var basePipeline = GetTransformerForImageVector();

            EstimatorChain<OnnxTransformer> pipeline = basePipeline
                .Append(_mlContext.Transforms.ApplyOnnxModel(
                    shapeDictionary: new Dictionary<string, int[]>()
                    {
                        { "input_1:0", new[] { 1, 416, 416, 3 } },
                        { "Identity:0", new[] { 1, 52, 52, 3, 85 } },
                        { "Identity_1:0", new[] { 1, 26, 26, 3, 85 } },
                        { "Identity_2:0", new[] { 1, 13, 13, 3, 85 } },
                    },
                    inputColumnNames: new[]
                    {
                        "input_1:0"
                    },
                    outputColumnNames: new[]
                    {
                        "Identity:0",
                        "Identity_1:0",
                        "Identity_2:0"
                    },
                    modelFile: modelLocation,
                    recursionLimit: 100));

            IDataView emptyData = _mlContext.Data.LoadFromEnumerable(new List<YoloV4InputData>());
            _model = pipeline.Fit(emptyData);
            return _model;
        }

        public void TestTransform(YoloV4InputData inputTiView)
        {
            var inputData = _mlContext.Data.LoadFromEnumerable(new List<YoloV4InputData>() { inputTiView });
            var transformedData = _model.Transform(inputData);
            PrintColumnsForPathTransformer(transformedData);
            // PrintColumnsForVectorTransformer(transformedData);
        }

        public YoloV4OutputData ScoreTest(YoloV4InputData image)
        {
            PredictionEngine<YoloV4InputData, YoloV4OutputData> predictionEngine = _mlContext.Model.CreatePredictionEngine<YoloV4InputData, YoloV4OutputData>(_model);
            YoloV4OutputData predict = predictionEngine.Predict(image);

            return predict;
        }

        public void Save(string pathToSave)
        {
            var modelNmae = "modelYolo4Path.zip";
            //var modelNmae = "modelYolo4Vector.zip";
            var fullPath = Path.Combine(pathToSave, modelNmae);
            IDataView data = _mlContext.Data.LoadFromEnumerable(new List<YoloV4InputData>());
            _mlContext.Model.Save(_model, data.Schema, fullPath);
        }

        private EstimatorChain<ImagePixelExtractingTransformer> GetTransformerForImagePath()
        {
            EstimatorChain<ImagePixelExtractingTransformer> pipeline = _mlContext.Transforms
                .LoadImages(
                outputColumnName: "image",
                imageFolder: "",
                inputColumnName: "ImagePath")
                .Append(_mlContext.Transforms.ResizeImages(
                    inputColumnName: "image",
                    outputColumnName: "input_1:0",
                    imageWidth: ImageNetSettings.imageWidth,
                    imageHeight: ImageNetSettings.imageHeight,
                    resizing: ResizingKind.Fill))
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "input_1:0",
                    scaleImage: 1f / 255f,
                    interleavePixelColors: true));

            return pipeline;
        }

        private EstimatorChain<ImagePixelExtractingTransformer> GetTransformerForImageVector()
        {
            EstimatorChain<ImagePixelExtractingTransformer> pipeline = _mlContext.Transforms
                .ConvertToImage(
                outputColumnName: "image",
                inputColumnName: "ImageVector",
                imageHeight: ImageNetSettings.imageHeight,
                imageWidth: ImageNetSettings.imageWidth,
                colorsPresent: ImagePixelExtractingEstimator.ColorBits.Rgb,
                orderOfColors: ImagePixelExtractingEstimator.ColorsOrder.ARGB,
                interleavedColors: true)
                .Append(_mlContext.Transforms.ExtractPixels(
                    outputColumnName: "input_1:0",
                    inputColumnName: "image",
                    scaleImage: 1f / 255f,
                    orderOfExtraction: ImagePixelExtractingEstimator.ColorsOrder.ARGB,
                    interleavePixelColors: true));

            return pipeline;
        }

        private void PrintColumnsForVectorTransformer(IDataView transformedData)
        {
            using (var cursor = transformedData.GetRowCursor(transformedData.Schema))
            {
                VBuffer<byte> features = default;
                VBuffer<float> pixels = default;
                MLImage imageObject = null;

                var featuresGetter = cursor.GetGetter<VBuffer<byte>>(cursor.Schema["ImageVector"]);
                var pixelsGetter = cursor.GetGetter<VBuffer<float>>(cursor.Schema["input_1:0"]);
                var imageGetter = cursor.GetGetter<MLImage>(cursor.Schema["image"]);

                while (cursor.MoveNext())
                {
                    featuresGetter(ref features);
                    pixelsGetter(ref pixels);
                    imageGetter(ref imageObject);

                    Console.WriteLine($"ImageVector");
                    Console.WriteLine($"{string.Join(",", features.DenseValues().Take(15)) + "..."}");
                    Console.WriteLine($"");

                    Console.WriteLine($"image");
                    Console.WriteLine($"Tag: {imageObject.Tag}");
                    Console.WriteLine($"Width: {imageObject.Width}");
                    Console.WriteLine($"Height: {imageObject.Height}");
                    Console.WriteLine($"BitsPerPixel: {imageObject.BitsPerPixel}");
                    Console.WriteLine($"PixelFormat: {imageObject.PixelFormat}");
                    Console.WriteLine($"Pixels: {string.Join(",", imageObject.Pixels.ToArray().Take(15)) + "..."}");
                    Console.WriteLine($"Pixels count: {imageObject.Pixels.Length}");
                    Console.WriteLine($"");

                    Console.WriteLine($"input_1:0");
                    Console.WriteLine($"count: {pixels.Length}");
                    Console.WriteLine(string.Join(",", pixels.DenseValues().Take(15)) + "...");

                    Console.WriteLine($"");
                }
                imageObject.Dispose();
            }
        }

        private void PrintColumnsForPathTransformer(IDataView transformedData)
        {
            using (var cursor = transformedData.GetRowCursor(transformedData.Schema))
            {
                ReadOnlyMemory<char> imgPath = default;
                VBuffer<float> pixels = default;
                MLImage imageObject = null;

                var imgPathGetter = cursor.GetGetter<ReadOnlyMemory<char>>(cursor.Schema["ImagePath"]);
                var pixelsGetter = cursor.GetGetter<VBuffer<float>>(cursor.Schema["input_1:0"]);
                var imageGetter = cursor.GetGetter<MLImage>(cursor.Schema["image"]);

                while (cursor.MoveNext())
                {
                    imgPathGetter(ref imgPath);
                    pixelsGetter(ref pixels);
                    imageGetter(ref imageObject);

                    Console.WriteLine($"ImagePath");
                    Console.WriteLine($"{imgPath}");
                    Console.WriteLine($"");

                    Console.WriteLine($"image");
                    Console.WriteLine($"Tag: {imageObject.Tag}");
                    Console.WriteLine($"Width: {imageObject.Width}");
                    Console.WriteLine($"Height: {imageObject.Height}");
                    Console.WriteLine($"BitsPerPixel: {imageObject.BitsPerPixel}");
                    Console.WriteLine($"PixelFormat: {imageObject.PixelFormat}");
                    Console.WriteLine($"Pixels: {string.Join(",", imageObject.Pixels.ToArray().Take(15)) + "..."}");
                    Console.WriteLine($"Pixels count: {imageObject.Pixels.Length}");
                    Console.WriteLine($"");

                    Console.WriteLine($"input_1:0");
                    Console.WriteLine($"count: {pixels.Length}");
                    Console.WriteLine(string.Join(",", pixels.DenseValues().Take(15)) + "...");

                    Console.WriteLine($"");
                }
                imageObject.Dispose();
            }
        }
    }
}
