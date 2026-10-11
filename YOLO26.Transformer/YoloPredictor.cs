using Microsoft.ML;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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
            using var image = Image.Load<Rgb24>(sourcePath);

            // 2. Начинаем рисование
            image.Mutate(ctx =>
            {
                // Настройки для тонкой рамки и мелкого текста
                var font = SystemFonts.CreateFont("Arial", 16, FontStyle.Regular); // Шрифт по умолчанию

                var pen = Pens.Solid(Color.Lime, 1f); // Зеленая рамка толщиной 1 пиксель

                foreach (var det in detections)
                {
                    if (!onlyVehicles || det.LabelId == 2 || det.LabelId == 7)
                    {
                        // Определяем прямоугольник рамки
                        var rect = new RectangleF(det.X1, det.Y1, det.X2 - det.X1, det.Y2 - det.Y1);

                        // Рисуем рамку
                        ctx.Draw(pen, rect);

                        // Готовим текст подписи
                        string label = $"ID:{det.LabelId} {det.Score:P0}";

                        // Измеряем размер текста, чтобы нарисовать фон
                        FontRectangle textSize = TextMeasurer.MeasureBounds(label, new TextOptions(font));

                        // Рисуем полупрозрачный фон для текста для лучшей читаемости
                        //var textBgRect = new RectangleF(det.X1, det.Y1 - textSize.Height - 2, textSize.Width + 4, textSize.Height + 2);
                        //ctx.Fill(Color.Lime.WithAlpha(0.6f), textBgRect);

                        // Рисуем сам текст (белым цветом поверх зеленого фона)
                        ctx.DrawText(label, font, Color.White, new PointF(det.X1 + 2, det.Y1 - textSize.Height - 1));
                    }
                }
            });

            // 3. Сохраняем результат
            image.Save(outputPath);
        }

        /// <summary>
        /// Модель ожидает массив float[], 
        /// где пиксели нормализованы (0..1) и идут в порядке RGB (а не BGR) и в формате CHW (сначала все красные, потом зеленые, потом синие).
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public float[] PrepareImage(Image<Rgb24> image)
        {
            image.Mutate(x => x.Resize(640, 640));

            float[] fc = new float[1 * 3 * 640 * 640];

            // Заполняем массив в формате CHW (Planar)
            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    var pixel = image[x, y];
                    fc[0 * 640 * 640 + y * 640 + x] = pixel.R / 255f;
                    fc[1 * 640 * 640 + y * 640 + x] = pixel.G / 255f;
                    fc[2 * 640 * 640 + y * 640 + x] = pixel.B / 255f;
                }
            }
            return fc;
        }
    }

}
