using Microsoft.ML;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using YOLO26.Shared.CvatWorker.Model;
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

        public void SaveAnnotatedImage(string sourcePath, string outputPath, List<YOLO26Result> detections)
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
                    if (det.LabelId == 2 || det.LabelId == 7)
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

        public void DrawParkingMap(string sourcePath, string outputPath, List<ParkingSpace> spaces, List<YOLO26Result> detections)
        {
            using var image = Image.Load<Rgb24>(sourcePath);

            image.Mutate(ctx =>
            {
                foreach (var space in spaces)
                {
                    //bool occupied = detections.Any(d =>
                    //{
                    //    // Находим точку "контакта" машины с землей (середина нижней грани рамки)
                    //    var carBottomCenter = new PointF((d.X1 + d.X2) / 2, d.Y2);

                    //    return (d.LabelId == 2 || d.LabelId == 7) && // 2 - car, 7 - truck в COCO
                    //           IsPointInPolygon(space.Points, carBottomCenter);
                    //});

                    bool occupied = detections.Any(d =>
                        (d.LabelId == 2 || d.LabelId == 7) && // Машины и грузовики
                        IsSpaceOccupied(space.Points, d)
                    );

                    foreach (var det in detections)
                    {
                        // Генерируем те же 5 точек, что и в логике IsSpaceOccupied
                        float yBottom = det.Y2;
                        float xLeft = det.X1;
                        float xRight = det.X2;
                        float width = xRight - xLeft;
                        float offset = 7f; // Такое же смещение

                        var testPoints = new PointF[]
                        {
                            new PointF(xLeft + width * 0.30f, yBottom - offset),
                            new PointF(xLeft + width * 0.40f, yBottom - offset),
                            new PointF(xLeft + width * 0.50f, yBottom - offset),
                            new PointF(xLeft + width * 0.60f, yBottom - offset),
                            new PointF(xLeft + width * 0.70f, yBottom - offset)
                        };

                        // Рисуем каждую точку как маленький красный кружок
                        foreach (var p in testPoints)
                        {
                            // EllipsePolygon создает круг с центром в 'p' и радиусом 3 пикселя
                            ctx.Fill(Color.Red, new EllipsePolygon(p, 4f));
                        }
                    }

                    //// Место занято, если любая машина перекрывает его более чем на 40%
                    //bool occupied = detections.Any(d =>
                    //    (d.LabelId == 2 || d.LabelId == 7) && // 2 - car, 7 - truck в COCO
                    //    GetOccupationRatio(space.Area, new RectangleF(d.X1, d.Y1, d.X2 - d.X1, d.Y2 - d.Y1)) > 0.4f
                    //);

                    space.IsOccupied = occupied;

                    Console.WriteLine($"Место #{space.Id}: {(space.IsOccupied ? "ЗАНЯТО" : "СВОБОДНО")}");

                    // 2. Выбираем цвет: Красный - занято, Зеленый - свободно
                    Color color = occupied ? Color.Red : Color.Lime;

                    // 3. Рисуем зону парковки
                    //ctx.Draw(color, 2f, space.Area);
                    ctx.DrawPolygon(color, 2f, space.Points);

                    // 4. Подписываем номер места
                    var font = SystemFonts.CreateFont("Arial", 14, FontStyle.Bold);
                    //ctx.DrawText(space.Id.ToString(), font, color, new PointF(space.Area.X + 5, space.Area.Y + 5));
                    ctx.DrawText(space.Id.ToString(), font, color, new PointF(space.Points[0].X + 5, space.Points[0].Y + 5));
                }
            });

            image.Save(outputPath);
        }

        /// <summary>
        /// функция, которая скажет, какая часть парковочного места перекрыта машиной
        /// </summary>
        /// <param name="space"></param>
        /// <param name="car"></param>
        /// <returns></returns>
        public float GetOccupationRatio(RectangleF space, RectangleF car)
        {
            // Находим область пересечения
            float x1 = Math.Max(space.Left, car.Left);
            float y1 = Math.Max(space.Top, car.Top);
            float x2 = Math.Min(space.Right, car.Right);
            float y2 = Math.Min(space.Bottom, car.Bottom);

            if (x2 <= x1 || y2 <= y1) return 0; // Пересечения нет

            float intersectionArea = (x2 - x1) * (y2 - y1);
            float spaceArea = space.Width * space.Height;

            return intersectionArea / spaceArea; // Какой % места занят машиной
        }

        /// <summary>
        /// Алгоритм «Point in Polygon»
        /// Чтобы понять, занято ли место, нам нужно проверить, попадает ли центральная точка (или углы) найденной машины внутрь вашего четырехугольника. 
        /// Самый популярный алгоритм для этого — Ray Casting (выпуск луча)
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsPointInPolygon(PointF[] polygon, PointF point)
        {
            bool isInside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }

        public bool IsSpaceOccupied(PointF[] spacePoints, YOLO26Result det)
        {
            // 1. Берем нижнюю линию рамки YOLO
            float yBottom = det.Y2;
            float xLeft = det.X1;
            float xRight = det.X2;
            float width = xRight - xLeft;

            // 2. Генерируем 5 точек вдоль этой линии (0%, 25%, 50%, 75%, 100% ширины)
            // Добавим небольшое смещение ВНУТРЬ рамки (например, на 5-10 пикселей вверх),
            // чтобы точка гарантированно была "под машиной", а не на границе бампера.
            float offset = 5f;
            var pointsToTest = new PointF[]
            {
                new PointF(xLeft + width * 0.30f, yBottom - offset),
                new PointF(xLeft + width * 0.40f, yBottom - offset),
                new PointF(xLeft + width * 0.50f, yBottom - offset),
                new PointF(xLeft + width * 0.60f, yBottom - offset),
                new PointF(xLeft + width * 0.70f, yBottom - offset)
            };

            // 3. Если хоть одна точка в полигоне — место занято
            var res = pointsToTest.Where(p => IsPointInPolygon(spacePoints, p));

            return res.Count() >= 1;
        }

    }

}
