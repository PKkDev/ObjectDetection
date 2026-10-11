using SkiaSharp;
using YOLO26.Transformer;

namespace ObjectDetection.Tests
{
    public class YoloPredictorPrepareImageTests
    {
        private const int Size = 640;
        private const int Plane = Size * Size;

        private static SKBitmap SolidBitmap(int width, int height, SKColor color)
        {
            var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color);
            return bitmap;
        }

        /// <summary>
        /// Индекс в массиве формата CHW: сначала вся плоскость R, потом G, потом B
        /// </summary>
        private static int Index(int channel, int x, int y) => channel * Plane + y * Size + x;

        [Fact]
        public void PrepareImage_ReturnsTensorOf3x640x640()
        {
            using var bitmap = SolidBitmap(100, 50, SKColors.Black);

            var data = YoloPredictor.PrepareImage(bitmap);

            Assert.Equal(3 * Size * Size, data.Length);
        }

        [Fact]
        public void PrepareImage_WritesChannelsInRgbOrderNormalizedToUnit()
        {
            using var bitmap = SolidBitmap(64, 64, new SKColor(255, 102, 0));

            var data = YoloPredictor.PrepareImage(bitmap);

            foreach (var (x, y) in new[] { (0, 0), (320, 320), (639, 639) })
            {
                Assert.Equal(1f, data[Index(0, x, y)], 2);
                Assert.Equal(102 / 255f, data[Index(1, x, y)], 2);
                Assert.Equal(0f, data[Index(2, x, y)], 2);
            }
        }

        [Fact]
        public void PrepareImage_AllValuesAreBetweenZeroAndOne()
        {
            using var bitmap = SolidBitmap(32, 32, SKColors.White);

            var data = YoloPredictor.PrepareImage(bitmap);

            Assert.All(data, v => Assert.InRange(v, 0f, 1f));
            Assert.Equal(1f, data[Index(1, 100, 100)], 2);
        }

        [Fact]
        public void PrepareImage_KeepsHorizontalLayout()
        {
            // левая половина красная, правая синяя
            using var bitmap = SolidBitmap(200, 100, SKColors.Red);
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.Blue })
                canvas.DrawRect(new SKRect(100, 0, 200, 100), paint);

            var data = YoloPredictor.PrepareImage(bitmap);

            Assert.Equal(1f, data[Index(0, 100, 320)], 2);
            Assert.Equal(0f, data[Index(2, 100, 320)], 2);
            Assert.Equal(0f, data[Index(0, 540, 320)], 2);
            Assert.Equal(1f, data[Index(2, 540, 320)], 2);
        }

        [Fact]
        public void PrepareImage_KeepsVerticalLayout()
        {
            // верхняя половина зеленая, нижняя синяя
            using var bitmap = SolidBitmap(100, 200, new SKColor(0, 255, 0));
            using (var canvas = new SKCanvas(bitmap))
            using (var paint = new SKPaint { Color = SKColors.Blue })
                canvas.DrawRect(new SKRect(0, 100, 100, 200), paint);

            var data = YoloPredictor.PrepareImage(bitmap);

            Assert.Equal(1f, data[Index(1, 320, 100)], 2);
            Assert.Equal(0f, data[Index(2, 320, 100)], 2);
            Assert.Equal(0f, data[Index(1, 320, 540)], 2);
            Assert.Equal(1f, data[Index(2, 320, 540)], 2);
        }

        [Fact]
        public void PrepareImage_DoesNotChangeSourceBitmap()
        {
            using var bitmap = SolidBitmap(100, 50, SKColors.Red);

            YoloPredictor.PrepareImage(bitmap);

            Assert.Equal(100, bitmap.Width);
            Assert.Equal(50, bitmap.Height);
            Assert.Equal(SKColors.Red, bitmap.GetPixel(10, 10));
        }
    }
}
