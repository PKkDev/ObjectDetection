using Microsoft.ML;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;
using YOLO26.Shared.YOLOWorker.Parser;

namespace YOLO26.Transformer
{
    public class YoloPredictor
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<Yolo26InputData, Yolo26OutputData> _predictionEngine;
        private readonly ITransformer _model;
        private readonly Yolo26OutputParser _outputParser = new();

        public YoloPredictor(string modelPath)
        {
            _mlContext = new MLContext();

            // 1. Создаем пустой набор данных для инициализации схемы
            var dataView = _mlContext.Data.LoadFromEnumerable(new List<Yolo26InputData>());

            // 2. Описываем конвейер (Pipeline)
            var pipeline = _mlContext.Transforms.ApplyOnnxModel(
                modelFile: modelPath,
                outputColumnNames: new[] { "output0" },
                inputColumnNames: new[] { "images" },
                gpuDeviceId: null // null для CPU, 0 для первой видеокарты NVIDIA
            );

            // 3. Создаем модель и движок предсказаний
            _model = pipeline.Fit(dataView);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<Yolo26InputData, Yolo26OutputData>(_model);
        }

        public void SaveModel(string modelName)
        {
            var relativePath = @"../../../Model";
            FileInfo _dataRoot = new FileInfo(typeof(Program).Assembly.Location);
            string assemblyFolderPath = _dataRoot.Directory.FullName;
            string pathToSave = System.IO.Path.Combine(assemblyFolderPath, relativePath);


            var modelNmae = $"{modelName.Replace(".onnx", "")}.zip";
            var fullPathToSave = System.IO.Path.Combine(pathToSave, modelNmae);

            IDataView data = _mlContext.Data.LoadFromEnumerable(new List<Yolo26InputData>());
            _mlContext.Model.Save(_model, data.Schema, fullPathToSave);
        }

        public List<YOLO26Result> Predict(float[] pixelData, int orgW, int orgH)
        {
            var input = new Yolo26InputData { Image = pixelData };
            var output = _predictionEngine.Predict(input);

            var detections = _outputParser.ParseOutputs(output, orgW, orgH);

            foreach (var det in detections)
                Console.WriteLine($"Найден объект {det.Label} (ID:{det.LabelId}) [Conf: {det.Score:P0}] на {det.X1:0},{det.Y1:0}");

            return detections;
        }

        /// <summary>
        /// Рисует рамки найденных объектов на исходном изображении
        /// </summary>
        /// <param name="onlyVehicles">true - рисовать только машины и грузовики (2 и 7 в COCO), false - все классы</param>
        public void SaveAnnotatedImage(string sourcePath, string outputPath, List<YOLO26Result> detections, bool onlyVehicles = true)
        {
            // 1. Загружаем ОРИГИНАЛЬНОЕ изображение
            using var image = SKBitmap.Decode(sourcePath);

            // 2. Начинаем рисование
            using var canvas = new SKCanvas(image);

            // Настройки для тонкой рамки и мелкого текста
            // Если шрифта Arial нет (например, в Linux), Skia подставит шрифт по умолчанию
            using var typeface = SKTypeface.FromFamilyName("Arial");
            using var font = new SKFont(typeface, 16);

            // Зеленая рамка толщиной 1 пиксель
            using var pen = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Lime, StrokeWidth = 1f };
            using var textPaint = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.White, IsAntialias = true };

            foreach (var det in detections)
            {
                if (!onlyVehicles || det.LabelId == 2 || det.LabelId == 7)
                {
                    // Определяем прямоугольник рамки
                    var rect = new SKRect(det.X1, det.Y1, det.X2, det.Y2);

                    // Рисуем рамку
                    canvas.DrawRect(rect, pen);

                    // Готовим текст подписи
                    string label = $"{det.Label} {det.Score:P0}";

                    // Рисуем сам текст (белым цветом) над рамкой; Y - это базовая линия текста
                    canvas.DrawText(label, det.X1 + 2, det.Y1 - 4, SKTextAlign.Left, font, textPaint);
                }
            }

            canvas.Flush();

            // 3. Сохраняем результат
            using var output = File.Create(outputPath);
            image.Encode(output, SKEncodedImageFormat.Png, 100);
        }
    }
}
