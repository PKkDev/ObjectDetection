using Microsoft.ML;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace YOLO26.Transformer
{
    public class YoloPredictor
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<Yolo26InputData, Yolo26OutputData> _predictionEngine;
        private ITransformer? _model = null;
        private const float ModelSize = 640f;

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

            var detections = new List<YOLO26Result>();

            // Разбор 300 предсказаний (каждое по 6 значений)
            for (int i = 0; i < 300; i++)
            {
                int offset = i * 6;
                float score = output.Predictions[offset + 4];

                if (score > 0.1f) // Порог уверенности 0.4f
                {
                    float x1 = output.Predictions[offset + 0];
                    float y1 = output.Predictions[offset + 1];
                    float x2 = output.Predictions[offset + 2];
                    float y2 = output.Predictions[offset + 3];
                    int classId = (int)output.Predictions[offset + 5];

                    // Масштабируем обратно под оригинал (если модель 640x640)
                    float x1_real = x1 * (orgW / ModelSize);
                    float y1_real = y1 * (orgH / ModelSize);
                    float x2_real = x2 * (orgW / ModelSize);
                    float y2_real = y2 * (orgH / ModelSize);

                    Console.WriteLine($"Найден объект {classId} [Conf: {score:P0}] на {x1_real:0},{y1_real:0}");

                    detections.Add(new YOLO26Result
                    {
                        X1 = x1 * (orgW / ModelSize),
                        Y1 = y1 * (orgH / ModelSize),
                        X2 = x2 * (orgW / ModelSize),
                        Y2 = y2 * (orgH / ModelSize),
                        Score = score,
                        LabelId = (int)output.Predictions[offset + 5]
                    });
                }
            }

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
                    string label = $"ID:{det.LabelId} {det.Score:P0}";

                    // Рисуем сам текст (белым цветом) над рамкой; Y - это базовая линия текста
                    canvas.DrawText(label, det.X1 + 2, det.Y1 - 4, SKTextAlign.Left, font, textPaint);
                }
            }

            canvas.Flush();

            // 3. Сохраняем результат
            using var output = File.Create(outputPath);
            image.Encode(output, SKEncodedImageFormat.Png, 100);
        }

        /// <summary>
        /// Модель ожидает массив float[], 
        /// где пиксели нормализованы (0..1) и идут в порядке RGB (а не BGR) и в формате CHW (сначала все красные, потом зеленые, потом синие).
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public float[] PrepareImage(SKBitmap image)
        {
            // Ресайз до 640x640 сразу в формат RGBA, чтобы порядок байт не зависел от платформы
            var info = new SKImageInfo(640, 640, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using var resized = image.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
            ReadOnlySpan<byte> pixels = resized.GetPixelSpan();
            int rowBytes = resized.RowBytes;

            float[] fc = new float[1 * 3 * 640 * 640];

            // Заполняем массив в формате CHW (Planar)
            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    int offset = y * rowBytes + x * 4;
                    fc[0 * 640 * 640 + y * 640 + x] = pixels[offset + 0] / 255f;
                    fc[1 * 640 * 640 + y * 640 + x] = pixels[offset + 1] / 255f;
                    fc[2 * 640 * 640 + y * 640 + x] = pixels[offset + 2] / 255f;
                }
            }
            return fc;
        }
    }

}
