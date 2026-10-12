using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Llm.Answerers
{
    /// <summary>
    /// Отвечает на вопрос по кадру, опираясь на изображение и результаты детектора
    /// </summary>
    public interface IFrameQuestionAnswerer
    {
        Task<FrameAnswer> AskAsync(SKBitmap frame, IReadOnlyList<YOLO26Result> detections, string question, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// LLM отказалась отвечать или оборвала ответ
    /// </summary>
    public class FrameQuestionFailedException : Exception
    {
        public FrameQuestionFailedException(string message) : base(message) { }
    }
}
