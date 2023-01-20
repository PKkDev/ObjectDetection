using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YOLO3.Shared.DataStructures;

namespace YOLO3.Shared.Parser
{
    public class Yolo3OutputParser
    {
        private readonly string[] _categories = new string[]
        {
            "person", "bicycle", "car", "motorbike", "aeroplane", "bus",
            "train", "truck", "boat", "traffic light", "fire hydrant",
            "stop sign", "parking meter", "bench", "bird", "cat", "dog",
            "horse", "sheep", "cow", "elephant", "bear", "zebra",
            "giraffe", "backpack", "umbrella", "handbag", "tie", "suitcase",
            "frisbee", "skis", "snowboard", "sports ball", "kite", "baseball bat",
            "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl",
            "banana", "apple", "sandwich", "orange", "broccoli", "carrot",
            "hot dog", "pizza", "donut", "cake", "chair", "sofa", "pottedplant",
            "bed", "diningtable", "toilet", "tvmonitor", "laptop", "mouse",
            "remote", "keyboard", "cell phone", "microwave", "oven", "toaster",
            "sink", "refrigerator", "book", "clock", "vase", "scissors", "teddy bear",
            "hair drier", "toothbrush"
        };

        public Yolo3OutputParser() { }

        public IReadOnlyList<Yolo3Result> ParseOutputs(Yolo3OutputData prediction)
        {
            if (prediction.Concat == null || prediction.Concat.Length == 0)
                return new List<Yolo3Result>();

            //if (prediction.Boxes.Length != Yolo3OutputData.YoloV3BboxPredictionCount * 4)
            //    throw new ArgumentException();

            //if (prediction.Scores.Length != Yolo3OutputData.YoloV3BboxPredictionCount * categories.Length)
            //    throw new ArgumentException();

            List<Yolo3Result> results = new List<Yolo3Result>();

            // Concat size is 'nbox'x3 (batch_index, class_index, box_index)
            int resulstCount = prediction.Concat.Length / 3;
            for (int c = 0; c < resulstCount; c++)
            {
                var res = prediction.Concat.Skip(c * 3).Take(3).ToArray();

                var batch_index = res[0];
                var class_index = res[1];
                var box_index = res[2];

                var label = _categories[class_index];
                var bbox = new float[]
                {
                    prediction.Boxes[box_index * 4],
                    prediction.Boxes[box_index * 4 + 1],
                    prediction.Boxes[box_index * 4 + 2],
                    prediction.Boxes[box_index * 4 + 3],
                };
                var score = prediction.Scores[box_index + class_index * Yolo3OutputData.YoloV3BboxPredictionCount];

                results.Add(new Yolo3Result(bbox, label, score));
            }

            return results;
        }
    }
}
