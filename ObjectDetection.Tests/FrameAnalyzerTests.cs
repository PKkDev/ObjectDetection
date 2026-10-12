using FrameAnalysis;
using FrameAnalysis.Filtering;
using FrameAnalysis.Llm;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace ObjectDetection.Tests
{
    public class FrameAnalyzerTests
    {
        private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Подмена LLM: запоминает, с чем ее вызвали, и возвращает заготовленный ответ
        /// </summary>
        private sealed class FakeAnswerer : IFrameQuestionAnswerer
        {
            public List<(IReadOnlyList<YOLO26Result> Detections, string Question)> Calls { get; } = [];

            public Task<FrameAnswer> AskAsync(SKBitmap frame, IReadOnlyList<YOLO26Result> detections, string question, CancellationToken cancellationToken = default)
            {
                Calls.Add((detections, question));
                return Task.FromResult(new FrameAnswer
                {
                    Answer = $"ответ {Calls.Count}",
                    CanAnswer = true,
                    Confidence = AnswerConfidence.High,
                    ReferencedObjects = [],
                });
            }
        }

        private static YOLO26Result Car() => new() { Label = "car", Score = 0.9f, X1 = 0, Y1 = 0, X2 = 10, Y2 = 10 };

        private static DetectionChangeFilter ImmediateFilter()
            => new(new FrameFilterOptions { StableFrames = 1, Cooldown = TimeSpan.Zero });

        [Fact]
        public async Task ProcessAsync_FrameRejectedByFilter_ReturnsNullWithoutCallingLlm()
        {
            var answerer = new FakeAnswerer();
            var analyzer = new FrameAnalyzer(_ => [], ImmediateFilter(), answerer);
            using var frame = new SKBitmap(10, 10);

            var result = await analyzer.ProcessAsync(frame, Start, "Что на кадре?");

            Assert.Null(result);
            Assert.Empty(answerer.Calls);
        }

        [Fact]
        public async Task ProcessAsync_FrameAccepted_ReturnsAnswerWithDetections()
        {
            var answerer = new FakeAnswerer();
            var detections = new List<YOLO26Result> { Car() };
            var analyzer = new FrameAnalyzer(_ => detections, ImmediateFilter(), answerer);
            using var frame = new SKBitmap(10, 10);

            var result = await analyzer.ProcessAsync(frame, Start, "Что на кадре?");

            Assert.NotNull(result);
            Assert.Equal(Start, result.Timestamp);
            Assert.Same(detections, result.Detections);
            Assert.Equal("ответ 1", result.Answer.Answer);

            var call = Assert.Single(answerer.Calls);
            Assert.Same(detections, call.Detections);
            Assert.Equal("Что на кадре?", call.Question);
        }

        [Fact]
        public async Task ProcessAsync_UnchangedScene_CallsLlmOnlyOnce()
        {
            var answerer = new FakeAnswerer();
            var analyzer = new FrameAnalyzer(_ => [Car()], ImmediateFilter(), answerer);
            using var frame = new SKBitmap(10, 10);

            for (int i = 0; i < 20; i++)
                await analyzer.ProcessAsync(frame, Start.AddSeconds(i), "Что на кадре?");

            Assert.Single(answerer.Calls);
        }
    }
}
