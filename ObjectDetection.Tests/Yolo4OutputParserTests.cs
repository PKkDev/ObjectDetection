using YOLO4.Shared.DataStructures;
using YOLO4.Shared.Parser;

namespace ObjectDetection.Tests
{
    public class Yolo4OutputParserTests
    {
        private const int AnchorsCount = 3;
        private const int ValuesPerBox = 85; // x, y, w, h, conf + 80 классов

        // Третий выход модели: сетка 13x13, шаг 32, якорь 0 = 142x110
        private const int Grid13 = 13;

        private static YoloV4OutputData EmptyOutput() => new()
        {
            Identity = new float[52 * 52 * AnchorsCount * ValuesPerBox],
            Identity1 = new float[26 * 26 * AnchorsCount * ValuesPerBox],
            Identity2 = new float[13 * 13 * AnchorsCount * ValuesPerBox]
        };

        /// <summary>
        /// Записывает в сетку 13x13 рамку с нулевыми смещениями: центр попадает в середину ячейки, размер равен якорю
        /// </summary>
        private static void SetBox13(YoloV4OutputData output, int cellX, int cellY, int classIndex, float classProb, float conf = 1f)
        {
            int offset = (cellY * Grid13 + cellX) * AnchorsCount * ValuesPerBox;
            output.Identity2[offset + 4] = conf;
            output.Identity2[offset + 5 + classIndex] = classProb;
        }

        [Fact]
        public void ParseOutputs_NullOutput_ReturnsEmptyList()
        {
            var parser = new Yolo4OutputParser();

            Assert.Empty(parser.ParseOutputs(new YoloV4OutputData()));
        }

        [Fact]
        public void ParseOutputs_ZeroOutput_ReturnsEmptyList()
        {
            var parser = new Yolo4OutputParser();

            Assert.Empty(parser.ParseOutputs(EmptyOutput()));
        }

        [Fact]
        public void ParseOutputs_SingleBox_DecodesBoxLabelAndConfidence()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            SetBox13(output, cellX: 6, cellY: 6, classIndex: 2, classProb: 0.9f);

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal("car", result.Label);
            Assert.Equal(0.9f, result.Confidence, 4);

            // центр: (sigmoid(0) * 1.05 - 0.025 + 6) * 32 = 208; размер: exp(0) * якорь = 142x110
            Assert.Equal(208f - 71f, result.BBox[0], 2);
            Assert.Equal(208f - 55f, result.BBox[1], 2);
            Assert.Equal(208f + 71f, result.BBox[2], 2);
            Assert.Equal(208f + 55f, result.BBox[3], 2);
        }

        [Fact]
        public void ParseOutputs_ConfidenceIsObjectnessTimesClassProbability()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            SetBox13(output, cellX: 6, cellY: 6, classIndex: 0, classProb: 0.8f, conf: 0.9f);

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal("person", result.Label);
            Assert.Equal(0.72f, result.Confidence, 4);
        }

        [Fact]
        public void ParseOutputs_DropsBoxesAtOrBelowScoreThreshold()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            // порог 0.5: 1 * 0.5 не проходит, 1 * 0.4 тоже
            SetBox13(output, cellX: 2, cellY: 2, classIndex: 2, classProb: 0.5f);
            SetBox13(output, cellX: 10, cellY: 10, classIndex: 2, classProb: 0.4f);

            Assert.Empty(parser.ParseOutputs(output));
        }

        [Fact]
        public void ParseOutputs_PicksClassWithHighestScore()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            SetBox13(output, cellX: 6, cellY: 6, classIndex: 2, classProb: 0.6f);
            SetBox13(output, cellX: 6, cellY: 6, classIndex: 7, classProb: 0.95f);

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal("truck", result.Label);
            Assert.Equal(0.95f, result.Confidence, 4);
        }

        [Fact]
        public void ParseOutputs_OverlappingBoxes_NmsKeepsMostConfident()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            // соседние ячейки: рамки 142x110 сдвинуты на 32 пикселя, IoU = 0.63 > 0.5
            SetBox13(output, cellX: 6, cellY: 6, classIndex: 2, classProb: 0.7f);
            SetBox13(output, cellX: 7, cellY: 6, classIndex: 2, classProb: 0.9f);

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal(0.9f, result.Confidence, 4);
            Assert.Equal(240f - 71f, result.BBox[0], 2);
        }

        [Fact]
        public void ParseOutputs_DistantBoxes_BothKeptAndSortedByConfidence()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            SetBox13(output, cellX: 2, cellY: 2, classIndex: 2, classProb: 0.7f);
            SetBox13(output, cellX: 10, cellY: 10, classIndex: 0, classProb: 0.9f);

            var results = parser.ParseOutputs(output);

            Assert.Equal(["person", "car"], results.Select(r => r.Label));
        }

        [Fact]
        public void ParseOutputs_BoxAtImageEdge_IsClippedToImage()
        {
            var parser = new Yolo4OutputParser();
            var output = EmptyOutput();
            // центр ячейки (0, 0) - точка (16, 16), рамка 142x110 выходит за левый верхний угол
            SetBox13(output, cellX: 0, cellY: 0, classIndex: 2, classProb: 0.9f);

            var result = Assert.Single(parser.ParseOutputs(output));

            Assert.Equal(0f, result.BBox[0]);
            Assert.Equal(0f, result.BBox[1]);
            Assert.Equal(16f + 71f, result.BBox[2], 2);
            Assert.Equal(16f + 55f, result.BBox[3], 2);
        }
    }
}
