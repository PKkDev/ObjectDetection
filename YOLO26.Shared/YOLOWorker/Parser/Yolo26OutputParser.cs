using YOLO26.Shared.YOLOWorker.DataStructures;

namespace YOLO26.Shared.YOLOWorker.Parser
{
    public class Yolo26OutputParser
    {
        /// <summary>
        /// Категории COCO в том порядке и с теми названиями, которые использует Ultralytics
        /// </summary>
        private readonly string[] _categories = new string[]
        {
            "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
            "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
            "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
            "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
            "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
            "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
            "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair",
            "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote",
            "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book", "clock",
            "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
        };

        /// <summary>
        /// Размер входа модели
        /// </summary>
        private const float ModelSize = 640f;

        /// <summary>
        /// Каждое предсказание - 6 значений: x1, y1, x2, y2, score, classId
        /// </summary>
        private const int ValuesPerPrediction = 6;

        private readonly float _scoreThres;

        public Yolo26OutputParser(float scoreThres = 0.1f)
        {
            _scoreThres = scoreThres;
        }

        /// <summary>
        /// Разбор выхода головы One-to-One (NMS не нужен), координаты пересчитываются под размер оригинала
        /// </summary>
        public List<YOLO26Result> ParseOutputs(Yolo26OutputData prediction, int orgW, int orgH)
        {
            var results = new List<YOLO26Result>();

            if (prediction.Predictions == null || prediction.Predictions.Length == 0)
                return results;

            float scaleX = orgW / ModelSize;
            float scaleY = orgH / ModelSize;

            int resultsCount = prediction.Predictions.Length / ValuesPerPrediction;
            for (int i = 0; i < resultsCount; i++)
            {
                int offset = i * ValuesPerPrediction;
                float score = prediction.Predictions[offset + 4];

                if (score <= _scoreThres) continue;

                int classId = (int)prediction.Predictions[offset + 5];

                results.Add(new YOLO26Result
                {
                    X1 = prediction.Predictions[offset + 0] * scaleX,
                    Y1 = prediction.Predictions[offset + 1] * scaleY,
                    X2 = prediction.Predictions[offset + 2] * scaleX,
                    Y2 = prediction.Predictions[offset + 3] * scaleY,
                    Score = score,
                    LabelId = classId,
                    Label = classId >= 0 && classId < _categories.Length ? _categories[classId] : classId.ToString()
                });
            }

            return results;
        }
    }
}
