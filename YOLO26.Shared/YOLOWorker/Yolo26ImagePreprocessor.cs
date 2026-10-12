using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace YOLO26.Shared.YOLOWorker
{
    /// <summary>
    /// Готовит кадр для подачи в модель YOLO26
    /// </summary>
    public static class Yolo26ImagePreprocessor
    {
        private const int ModelSize = 640;

        /// <summary>
        /// Входные данные модели из кадра
        /// </summary>
        public static Yolo26InputData ToInput(SKBitmap image) => new() { Image = PrepareImage(image) };

        /// <summary>
        /// Модель ожидает массив float[],
        /// где пиксели нормализованы (0..1) и идут в порядке RGB (а не BGR) и в формате CHW (сначала все красные, потом зеленые, потом синие).
        /// </summary>
        public static float[] PrepareImage(SKBitmap image)
        {
            // Ресайз до 640x640 сразу в формат RGBA, чтобы порядок байт не зависел от платформы
            var info = new SKImageInfo(ModelSize, ModelSize, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using var resized = image.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
            ReadOnlySpan<byte> pixels = resized.GetPixelSpan();
            int rowBytes = resized.RowBytes;

            float[] fc = new float[1 * 3 * ModelSize * ModelSize];

            // Заполняем массив в формате CHW (Planar)
            for (int y = 0; y < ModelSize; y++)
            {
                for (int x = 0; x < ModelSize; x++)
                {
                    int offset = y * rowBytes + x * 4;
                    fc[0 * ModelSize * ModelSize + y * ModelSize + x] = pixels[offset + 0] / 255f;
                    fc[1 * ModelSize * ModelSize + y * ModelSize + x] = pixels[offset + 1] / 255f;
                    fc[2 * ModelSize * ModelSize + y * ModelSize + x] = pixels[offset + 2] / 255f;
                }
            }
            return fc;
        }
    }
}
