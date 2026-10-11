using YOLO3.Shared.DataStructures;
using YOLO3.Shared.Parser;

namespace ObjectDetection.Tests
{
    public class Yolo3OutputParserTests
    {
        private const int BoxCount = Yolo3OutputData.YoloV3BboxPredictionCount;
        private const int ClassCount = 80;

        private static Yolo3OutputData EmptyOutput() => new()
        {
            Boxes = new float[BoxCount * 4],
            Scores = new float[ClassCount * BoxCount],
            Concat = []
        };

        private static void SetBox(Yolo3OutputData output, int boxIndex, float[] box)
            => Array.Copy(box, 0, output.Boxes, boxIndex * 4, 4);

        /// <summary>
        /// Scores имеет размер [80 x 'n_candidates']: сначала все рамки первого класса, потом второго и т.д.
        /// </summary>
        private static void SetScore(Yolo3OutputData output, int classIndex, int boxIndex, float score)
            => output.Scores[boxIndex + classIndex * BoxCount] = score;

        [Fact]
        public void ParseOutputs_NoSelectedBoxes_ReturnsEmptyList()
        {
            var parser = new Yolo3OutputParser();

            var output = EmptyOutput();
            Assert.Empty(parser.ParseOutputs(output));

            output.Concat = null!;
            Assert.Empty(parser.ParseOutputs(output));
        }

        [Fact]
        public void ParseOutputs_ReadsBoxLabelAndScoreByIndexes()
        {
            var parser = new Yolo3OutputParser();
            var output = EmptyOutput();
            SetBox(output, 5, [10, 20, 30, 40]);
            SetScore(output, classIndex: 2, boxIndex: 5, 0.75f);
            // batch_index, class_index, box_index
            output.Concat = [0, 2, 5];

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal("car", result.Label);
            Assert.Equal(0.75f, result.Confidence);
            Assert.Equal([10f, 20f, 30f, 40f], result.BBox);
        }

        [Fact]
        public void ParseOutputs_ReturnsOneResultPerConcatTriple()
        {
            var parser = new Yolo3OutputParser();
            var output = EmptyOutput();
            SetBox(output, 0, [1, 2, 3, 4]);
            SetBox(output, BoxCount - 1, [5, 6, 7, 8]);
            SetScore(output, classIndex: 0, boxIndex: 0, 0.9f);
            SetScore(output, classIndex: 79, boxIndex: BoxCount - 1, 0.6f);
            output.Concat = [0, 0, 0, 0, 79, BoxCount - 1];

            var results = parser.ParseOutputs(output);

            Assert.Equal(2, results.Count);
            Assert.Equal("person", results[0].Label);
            Assert.Equal(0.9f, results[0].Confidence);
            Assert.Equal("toothbrush", results[1].Label);
            Assert.Equal(0.6f, results[1].Confidence);
            Assert.Equal([5f, 6f, 7f, 8f], results[1].BBox);
        }

        [Fact]
        public void ParseOutputs_SameBoxWithTwoClasses_UsesScoreOfEachClass()
        {
            var parser = new Yolo3OutputParser();
            var output = EmptyOutput();
            SetBox(output, 7, [1, 2, 3, 4]);
            SetScore(output, classIndex: 2, boxIndex: 7, 0.8f);
            SetScore(output, classIndex: 7, boxIndex: 7, 0.4f);
            output.Concat = [0, 2, 7, 0, 7, 7];

            var results = parser.ParseOutputs(output);

            Assert.Equal(["car", "truck"], results.Select(r => r.Label));
            Assert.Equal([0.8f, 0.4f], results.Select(r => r.Confidence));
        }
    }
}
