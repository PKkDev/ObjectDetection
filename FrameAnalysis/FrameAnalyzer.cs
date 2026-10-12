using FrameAnalysis.Filtering;
using FrameAnalysis.Llm;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis
{
    /// <summary>
    /// Результат разбора кадра, который прошел фильтр
    /// </summary>
    public record FrameAnalysisResult(DateTimeOffset Timestamp, IReadOnlyList<YOLO26Result> Detections, FrameAnswer Answer);

    /// <summary>
    /// Конвейер: детектор находит объекты на каждом кадре, фильтр отбирает кадры-события,
    /// LLM отвечает на вопрос только по отобранным кадрам
    /// </summary>
    public class FrameAnalyzer
    {
        private readonly Func<SKBitmap, IReadOnlyList<YOLO26Result>> _detect;
        private readonly DetectionChangeFilter _filter;
        private readonly IFrameQuestionAnswerer _answerer;

        /// <param name="detect">Детектор объектов: получает кадр, возвращает найденные объекты в координатах кадра</param>
        public FrameAnalyzer(Func<SKBitmap, IReadOnlyList<YOLO26Result>> detect, DetectionChangeFilter filter, IFrameQuestionAnswerer answerer)
        {
            _detect = detect;
            _filter = filter;
            _answerer = answerer;
        }

        /// <summary>
        /// Обрабатывает очередной кадр. Возвращает null, если кадр не прошел фильтр и LLM не вызывалась
        /// </summary>
        public async Task<FrameAnalysisResult?> ProcessAsync(SKBitmap frame, DateTimeOffset timestamp, string question, CancellationToken cancellationToken = default)
        {
            var detections = _detect(frame);

            if (!_filter.ShouldAnalyze(detections, timestamp))
                return null;

            var answer = await _answerer.AskAsync(frame, detections, question, cancellationToken);
            return new FrameAnalysisResult(timestamp, detections, answer);
        }
    }
}
