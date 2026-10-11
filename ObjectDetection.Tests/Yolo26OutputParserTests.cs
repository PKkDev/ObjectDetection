using YOLO26.Shared.YOLOWorker.DataStructures;
using YOLO26.Shared.YOLOWorker.Parser;

namespace ObjectDetection.Tests
{
    public class Yolo26OutputParserTests
    {
        /// <summary>
        /// Собирает выход модели: по 6 значений на предсказание (x1, y1, x2, y2, score, classId)
        /// </summary>
        private static Yolo26OutputData Output(params float[][] predictions)
            => new() { Predictions = predictions.SelectMany(p => p).ToArray() };

        [Fact]
        public void ParseOutputs_EmptyPredictions_ReturnsEmptyList()
        {
            var parser = new Yolo26OutputParser();

            Assert.Empty(parser.ParseOutputs(new Yolo26OutputData { Predictions = [] }, 640, 640));
            Assert.Empty(parser.ParseOutputs(new Yolo26OutputData { Predictions = null! }, 640, 640));
        }

        [Fact]
        public void ParseOutputs_DropsPredictionsAtOrBelowThreshold()
        {
            var parser = new Yolo26OutputParser(scoreThres: 0.5f);
            var output = Output(
                [0, 0, 10, 10, 0.9f, 2],
                [0, 0, 10, 10, 0.5f, 2],
                [0, 0, 10, 10, 0.2f, 2]);

            var results = parser.ParseOutputs(output, 640, 640);

            var result = Assert.Single(results);
            Assert.Equal(0.9f, result.Score);
        }

        [Fact]
        public void ParseOutputs_DefaultThresholdIsOneTenth()
        {
            var parser = new Yolo26OutputParser();
            var output = Output(
                [0, 0, 10, 10, 0.11f, 0],
                [0, 0, 10, 10, 0.09f, 0]);

            var results = parser.ParseOutputs(output, 640, 640);

            Assert.Single(results);
        }

        [Fact]
        public void ParseOutputs_ScalesBoxToOriginalImageSize()
        {
            var parser = new Yolo26OutputParser();
            var output = Output([64, 32, 320, 640, 0.9f, 2]);

            // 1280x320: по X координаты растягиваются в 2 раза, по Y сжимаются в 2 раза
            var result = Assert.Single(parser.ParseOutputs(output, 1280, 320));

            Assert.Equal(128f, result.X1, 3);
            Assert.Equal(16f, result.Y1, 3);
            Assert.Equal(640f, result.X2, 3);
            Assert.Equal(320f, result.Y2, 3);
        }

        [Theory]
        [InlineData(0, "person")]
        [InlineData(2, "car")]
        [InlineData(7, "truck")]
        [InlineData(79, "toothbrush")]
        public void ParseOutputs_MapsClassIdToLabel(int classId, string expectedLabel)
        {
            var parser = new Yolo26OutputParser();
            var output = Output([0, 0, 10, 10, 0.9f, classId]);

            var result = Assert.Single(parser.ParseOutputs(output, 640, 640));

            Assert.Equal(classId, result.LabelId);
            Assert.Equal(expectedLabel, result.Label);
        }

        [Theory]
        [InlineData(80)]
        [InlineData(999)]
        [InlineData(-1)]
        public void ParseOutputs_UnknownClassId_UsesIdAsLabel(int classId)
        {
            var parser = new Yolo26OutputParser();
            var output = Output([0, 0, 10, 10, 0.9f, classId]);

            var result = Assert.Single(parser.ParseOutputs(output, 640, 640));

            Assert.Equal(classId, result.LabelId);
            Assert.Equal(classId.ToString(), result.Label);
        }

        [Fact]
        public void ParseOutputs_KeepsOrderOfPredictions()
        {
            var parser = new Yolo26OutputParser();
            var output = Output(
                [0, 0, 10, 10, 0.3f, 2],
                [0, 0, 10, 10, 0.9f, 7],
                [0, 0, 10, 10, 0.6f, 0]);

            var results = parser.ParseOutputs(output, 640, 640);

            Assert.Equal(["car", "truck", "person"], results.Select(r => r.Label));
        }
    }
}
