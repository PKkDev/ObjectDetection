using System.Globalization;
using System.Text;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Llm
{
    public class FramePromptOptions
    {
        /// <summary>
        /// Кадр и фрагменты уменьшаются так, чтобы длинная сторона не превышала это значение
        /// </summary>
        public int MaxImageSide { get; set; } = 1568;

        /// <summary>
        /// Сколько увеличенных фрагментов объектов прикладывать (берутся объекты с наибольшей уверенностью)
        /// </summary>
        public int MaxCrops { get; set; } = 8;

        /// <summary>
        /// Поля вокруг объекта на фрагменте, в долях от размера рамки
        /// </summary>
        public float CropPadding { get; set; } = 0.15f;

        public int JpegQuality { get; set; } = 85;
    }

    /// <summary>
    /// Увеличенный фрагмент одного объекта
    /// </summary>
    public record FrameCrop(int Number, byte[] Jpeg);

    /// <summary>
    /// Все, что уходит в LLM по одному кадру
    /// </summary>
    public record FramePrompt(byte[] FrameJpeg, IReadOnlyList<FrameCrop> Crops, string Text);

    /// <summary>
    /// Собирает из кадра, результатов детекции и вопроса запрос для LLM:
    /// кадр целиком с пронумерованными рамками, увеличенные фрагменты объектов и текст
    /// </summary>
    public class FramePromptBuilder
    {
        /// <summary>
        /// Высота плашки с номером над каждым фрагментом, в пикселях
        /// </summary>
        public const int CropLabelHeight = 22;

        private const float CropLabelFontSize = 16f;

        private readonly FramePromptOptions _options;

        public FramePromptBuilder(FramePromptOptions? options = null)
        {
            _options = options ?? new FramePromptOptions();
        }

        public FramePrompt Build(SKBitmap frame, IReadOnlyList<YOLO26Result> detections, string question)
        {
            // номера объектов: №1 - самый уверенный
            var numbered = detections.OrderByDescending(d => d.Score).ToList();

            var frameJpeg = RenderAnnotatedFrame(frame, numbered);

            var crops = new List<FrameCrop>();
            for (int i = 0; i < numbered.Count && crops.Count < _options.MaxCrops; i++)
            {
                var jpeg = RenderCrop(frame, numbered[i], i + 1);
                if (jpeg != null) crops.Add(new FrameCrop(i + 1, jpeg));
            }

            return new FramePrompt(frameJpeg, crops, BuildText(frame, numbered, crops, question));
        }

        private static string BuildText(SKBitmap frame, List<YOLO26Result> numbered, List<FrameCrop> crops, string question)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Кадр с камеры, {frame.Width}x{frame.Height} пикселей.");

            if (numbered.Count == 0)
                sb.AppendLine("Детектор объектов ничего не нашел на кадре.");
            else
            {
                sb.AppendLine("Детектор объектов нашел на кадре:");
                for (int i = 0; i < numbered.Count; i++)
                {
                    var d = numbered[i];
                    sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                        $"№{i + 1}: {d.Label}, уверенность {d.Score * 100:0}%, рамка ({d.X1:0}, {d.Y1:0})-({d.X2:0}, {d.Y2:0})"));
                }
            }

            sb.AppendLine();
            sb.Append("Изображения: первое - кадр целиком");
            if (numbered.Count > 0) sb.Append(" с пронумерованными рамками");
            if (crops.Count > 0)
                sb.Append($"; далее по порядку - увеличенные фрагменты объектов {string.Join(", ", crops.Select(c => $"№{c.Number}"))}, номер объекта написан на плашке над фрагментом");
            sb.AppendLine(".");

            sb.AppendLine();
            sb.Append($"Вопрос: {question}");
            return sb.ToString();
        }

        private byte[] RenderAnnotatedFrame(SKBitmap frame, List<YOLO26Result> numbered)
        {
            float scale = Math.Min(1f, _options.MaxImageSide / (float)Math.Max(frame.Width, frame.Height));
            int width = Math.Max(1, (int)Math.Round(frame.Width * scale));
            int height = Math.Max(1, (int)Math.Round(frame.Height * scale));

            using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(target))
            {
                canvas.DrawBitmap(frame, new SKRect(0, 0, width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));

                float fontSize = Math.Max(14f, Math.Min(width, height) / 40f);
                using var typeface = SKTypeface.FromFamilyName("Arial");
                using var font = new SKFont(typeface, fontSize);
                using var pen = new SKPaint { Style = SKPaintStyle.Stroke, Color = SKColors.Lime, StrokeWidth = 2f };
                using var labelBackground = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Lime };
                using var labelText = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Black, IsAntialias = true };

                for (int i = 0; i < numbered.Count; i++)
                {
                    var d = numbered[i];
                    var rect = new SKRect(d.X1 * scale, d.Y1 * scale, d.X2 * scale, d.Y2 * scale);
                    canvas.DrawRect(rect, pen);

                    // плашка с номером в левом верхнем углу рамки
                    string label = (i + 1).ToString(CultureInfo.InvariantCulture);
                    float labelWidth = font.MeasureText(label) + 6f;
                    float labelHeight = fontSize + 4f;
                    float left = Math.Clamp(rect.Left, 0, Math.Max(0, width - labelWidth));
                    float top = Math.Clamp(rect.Top, 0, Math.Max(0, height - labelHeight));
                    canvas.DrawRect(SKRect.Create(left, top, labelWidth, labelHeight), labelBackground);
                    canvas.DrawText(label, left + 3f, top + fontSize, SKTextAlign.Left, font, labelText);
                }

                canvas.Flush();
            }

            // Сохраняем результат в файл для отладки
            //using (var image = SKImage.FromBitmap(target))
            //using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            //using (var stream = File.OpenWrite("output.png"))
            //{
            //    data.SaveTo(stream);
            //}

            return Encode(target);
        }

        /// <summary>
        /// Вырезает объект из исходного кадра в полном разрешении - без рамок.
        /// Над фрагментом рисуется плашка с номером объекта, чтобы модель не путала, какой фрагмент к чему относится
        /// </summary>
        private byte[]? RenderCrop(SKBitmap frame, YOLO26Result detection, int number)
        {
            float padX = (detection.X2 - detection.X1) * _options.CropPadding;
            float padY = (detection.Y2 - detection.Y1) * _options.CropPadding;

            int left = Math.Clamp((int)Math.Floor(detection.X1 - padX), 0, frame.Width);
            int top = Math.Clamp((int)Math.Floor(detection.Y1 - padY), 0, frame.Height);
            int right = Math.Clamp((int)Math.Ceiling(detection.X2 + padX), 0, frame.Width);
            int bottom = Math.Clamp((int)Math.Ceiling(detection.Y2 + padY), 0, frame.Height);

            int cropWidth = right - left;
            int cropHeight = bottom - top;
            if (cropWidth < 2 || cropHeight < 2) return null;

            float scale = Math.Min(1f, _options.MaxImageSide / (float)Math.Max(cropWidth, cropHeight));
            int width = Math.Max(1, (int)Math.Round(cropWidth * scale));
            int height = Math.Max(1, (int)Math.Round(cropHeight * scale));

            string label = $"№{number}";
            using var typeface = SKTypeface.FromFamilyName("Arial");
            using var font = new SKFont(typeface, CropLabelFontSize);

            // узкий фрагмент расширяем, чтобы номер поместился целиком
            int canvasWidth = Math.Max(width, (int)Math.Ceiling(font.MeasureText(label)) + 8);

            using var target = new SKBitmap(new SKImageInfo(canvasWidth, height + CropLabelHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(target))
            {
                canvas.Clear(SKColors.Black);

                using var labelBackground = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Lime };
                using var labelText = new SKPaint { Style = SKPaintStyle.Fill, Color = SKColors.Black, IsAntialias = true };
                canvas.DrawRect(SKRect.Create(0, 0, canvasWidth, CropLabelHeight), labelBackground);
                canvas.DrawText(label, 4f, CropLabelFontSize, SKTextAlign.Left, font, labelText);

                canvas.DrawBitmap(frame, new SKRect(left, top, right, bottom), new SKRect(0, CropLabelHeight, width, CropLabelHeight + height),
                    new SKSamplingOptions(SKCubicResampler.Mitchell));
                canvas.Flush();
            }

            return Encode(target);
        }

        private byte[] Encode(SKBitmap bitmap)
        {
            using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, _options.JpegQuality);
            return data.ToArray();
        }
    }
}
