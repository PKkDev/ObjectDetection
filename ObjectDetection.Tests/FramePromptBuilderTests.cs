using FrameAnalysis.Llm;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace ObjectDetection.Tests
{
    public class FramePromptBuilderTests
    {
        private static SKBitmap Frame(int width, int height, SKColor? color = null)
        {
            var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color ?? SKColors.Gray);
            return bitmap;
        }

        private static YOLO26Result Det(string label, float score, float x1, float y1, float x2, float y2)
            => new() { Label = label, Score = score, X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };

        private static SKBitmap Decode(byte[] jpeg) => SKBitmap.Decode(jpeg);

        [Fact]
        public void Build_NoDetections_SendsOnlyFrameAndSaysSo()
        {
            using var frame = Frame(800, 600);

            var prompt = new FramePromptBuilder().Build(frame, [], "Что на кадре?");

            Assert.Empty(prompt.Crops);
            Assert.Contains("ничего не нашел", prompt.Text);
            Assert.DoesNotContain("фрагменты", prompt.Text);
            Assert.EndsWith("Вопрос: Что на кадре?", prompt.Text);
        }

        [Fact]
        public void Build_SmallFrame_KeepsOriginalSize()
        {
            using var frame = Frame(800, 600);

            var prompt = new FramePromptBuilder().Build(frame, [], "?");

            using var sent = Decode(prompt.FrameJpeg);
            Assert.Equal(800, sent.Width);
            Assert.Equal(600, sent.Height);
        }

        [Fact]
        public void Build_LargeFrame_IsDownscaledKeepingAspectRatio()
        {
            using var frame = Frame(4000, 2000);

            var prompt = new FramePromptBuilder(new FramePromptOptions { MaxImageSide = 1000 }).Build(frame, [], "?");

            using var sent = Decode(prompt.FrameJpeg);
            Assert.Equal(1000, sent.Width);
            Assert.Equal(500, sent.Height);
        }

        [Fact]
        public void Build_NumbersDetectionsByScoreDescending()
        {
            using var frame = Frame(800, 600);
            var detections = new[]
            {
                Det("person", 0.5f, 10, 10, 60, 110),
                Det("car", 0.95f, 200, 200, 400, 300),
            };

            var prompt = new FramePromptBuilder().Build(frame, detections, "?");

            Assert.Contains("№1: car, уверенность 95%, рамка (200, 200)-(400, 300)", prompt.Text);
            Assert.Contains("№2: person, уверенность 50%, рамка (10, 10)-(60, 110)", prompt.Text);
        }

        [Fact]
        public void Build_TextReportsOriginalFrameSizeAndCoordinates()
        {
            using var frame = Frame(4000, 2000);
            var detections = new[] { Det("car", 0.9f, 3000, 1000, 3500, 1500) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { MaxImageSide = 1000 }).Build(frame, detections, "?");

            // координаты в тексте - в пикселях исходного кадра, а не уменьшенной копии
            Assert.Contains("4000x2000", prompt.Text);
            Assert.Contains("рамка (3000, 1000)-(3500, 1500)", prompt.Text);
        }

        [Fact]
        public void Build_CropIsCutFromOriginalFrameWithPadding()
        {
            using var frame = Frame(1000, 1000);
            var detections = new[] { Det("car", 0.9f, 400, 400, 600, 500) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { CropPadding = 0.1f }).Build(frame, detections, "?");

            var crop = Assert.Single(prompt.Crops);
            Assert.Equal(1, crop.Number);
            using var image = Decode(crop.Jpeg);
            // 200x100 плюс поля по 10% с каждой стороны и плашка с номером сверху
            Assert.Equal(240, image.Width);
            Assert.Equal(120 + FramePromptBuilder.CropLabelHeight, image.Height);
        }

        [Fact]
        public void Build_CropIsCleanWhileFrameHasBoxDrawn()
        {
            using var frame = Frame(400, 400, SKColors.Gray);
            var detections = new[] { Det("car", 0.9f, 100, 100, 300, 300) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { CropPadding = 0f, JpegQuality = 100 }).Build(frame, detections, "?");

            // на кадре по верхней границе рамки проходит зеленая линия
            using var sentFrame = Decode(prompt.FrameJpeg);
            var onBox = sentFrame.GetPixel(200, 100);
            Assert.True(onBox.Green > 200 && onBox.Red < 100, $"ожидалась зеленая рамка, получен цвет {onBox}");

            // фрагмент вырезан из исходного кадра - рамки на нем нет
            using var crop = Decode(prompt.Crops[0].Jpeg);
            var onCropEdge = crop.GetPixel(100, FramePromptBuilder.CropLabelHeight + 2);
            Assert.InRange(onCropEdge.Green, 118, 138);
            Assert.InRange(onCropEdge.Red, 118, 138);
        }

        [Fact]
        public void Build_CropHasLabelStripWithNumberAbove()
        {
            using var frame = Frame(400, 400, SKColors.Gray);
            var detections = new[] { Det("car", 0.9f, 100, 100, 300, 300) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { CropPadding = 0f, JpegQuality = 100 }).Build(frame, detections, "?");

            using var crop = Decode(prompt.Crops[0].Jpeg);

            // справа на плашке текста нет - там чистый цвет фона плашки
            var onStrip = crop.GetPixel(180, FramePromptBuilder.CropLabelHeight / 2);
            Assert.True(onStrip.Green > 200 && onStrip.Red < 100, $"ожидалась зеленая плашка, получен цвет {onStrip}");

            // слева на плашке написан номер: среди пикселей есть темные
            bool hasText = false;
            for (int x = 0; x < 40 && !hasText; x++)
                for (int y = 0; y < FramePromptBuilder.CropLabelHeight && !hasText; y++)
                    hasText = crop.GetPixel(x, y).Green < 100;
            Assert.True(hasText, "на плашке не найден текст номера");
        }

        [Fact]
        public void Build_NarrowCrop_IsWidenedToFitLabel()
        {
            using var frame = Frame(400, 400);
            var detections = new[] { Det("person", 0.9f, 100, 100, 106, 200) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { CropPadding = 0f }).Build(frame, detections, "?");

            using var image = Decode(Assert.Single(prompt.Crops).Jpeg);
            // сам объект шириной 6 пикселей, но номер должен поместиться целиком
            Assert.True(image.Width > 6, $"ширина фрагмента {image.Width}");
            Assert.Equal(100 + FramePromptBuilder.CropLabelHeight, image.Height);
        }

        [Fact]
        public void Build_CropAtFrameEdge_IsClippedToFrame()
        {
            using var frame = Frame(500, 500);
            var detections = new[] { Det("car", 0.9f, -20, -20, 100, 100) };

            var prompt = new FramePromptBuilder(new FramePromptOptions { CropPadding = 0.5f }).Build(frame, detections, "?");

            using var image = Decode(Assert.Single(prompt.Crops).Jpeg);
            // рамка с полями: (-80, -80)-(160, 160), в пределах кадра остается 160x160
            Assert.Equal(160, image.Width);
            Assert.Equal(160 + FramePromptBuilder.CropLabelHeight, image.Height);
        }

        [Fact]
        public void Build_LimitsNumberOfCropsToMostConfident()
        {
            using var frame = Frame(1000, 1000);
            var detections = Enumerable.Range(0, 5)
                .Select(i => Det("car", 0.5f + i * 0.1f, i * 100, 0, i * 100 + 80, 80))
                .ToArray();

            var prompt = new FramePromptBuilder(new FramePromptOptions { MaxCrops = 2 }).Build(frame, detections, "?");

            // в тексте перечислены все объекты, а фрагменты - только для двух самых уверенных
            Assert.Contains("№5:", prompt.Text);
            Assert.Equal([1, 2], prompt.Crops.Select(c => c.Number));
            Assert.Contains("фрагменты объектов №1, №2, номер объекта написан на плашке над фрагментом.", prompt.Text);
        }

        [Fact]
        public void Build_DegenerateDetection_IsListedButHasNoCrop()
        {
            using var frame = Frame(500, 500);
            var detections = new[] { Det("car", 0.9f, 100, 100, 100, 100) };

            var prompt = new FramePromptBuilder().Build(frame, detections, "?");

            Assert.Empty(prompt.Crops);
            Assert.Contains("№1: car", prompt.Text);
        }

        [Fact]
        public void Build_DoesNotChangeSourceFrame()
        {
            using var frame = Frame(400, 400, SKColors.Gray);
            var detections = new[] { Det("car", 0.9f, 100, 100, 300, 300) };

            new FramePromptBuilder().Build(frame, detections, "?");

            Assert.Equal(SKColors.Gray, frame.GetPixel(200, 100));
        }
    }
}
