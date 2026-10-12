using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Filtering
{
    /// <summary>
    /// Решает, какие кадры стоит отправлять на разбор в LLM.
    /// Событие - это устойчивое изменение состава объектов на кадре (сколько объектов какой категории),
    /// а не сам факт наличия объектов: иначе стоящая машина отправлялась бы на каждом кадре
    /// </summary>
    public class DetectionChangeFilter
    {
        private readonly FrameFilterOptions _options;

        // состав объектов на момент последнего события; изначально - пустая сцена
        private string _analyzedSignature = string.Empty;
        private DateTimeOffset? _lastEventTime;

        // состав, который наблюдаем сейчас, и сколько кадров подряд он держится
        private string _candidateSignature = string.Empty;
        private int _candidateFrames;

        public DetectionChangeFilter(FrameFilterOptions? options = null)
        {
            _options = options ?? new FrameFilterOptions();
        }

        /// <summary>
        /// Объекты кадра, которые фильтр учитывает: нужной категории и с достаточной уверенностью
        /// </summary>
        public List<YOLO26Result> RelevantDetections(IReadOnlyList<YOLO26Result> detections)
            => detections
                .Where(d => d.Score >= _options.MinScore)
                .Where(d => _options.WatchedLabels == null || _options.WatchedLabels.Contains(d.Label))
                .ToList();

        /// <summary>
        /// Вызывается для каждого кадра по порядку. Возвращает true, если кадр нужно отправить на разбор
        /// </summary>
        public bool ShouldAnalyze(IReadOnlyList<YOLO26Result> detections, DateTimeOffset timestamp)
        {
            var relevant = RelevantDetections(detections);
            var signature = Signature(relevant);

            if (signature == _candidateSignature)
                _candidateFrames++;
            else
            {
                _candidateSignature = signature;
                _candidateFrames = 1;
            }

            // состав не изменился с последнего события
            if (signature == _analyzedSignature) return false;

            // изменение еще не устоялось
            if (_candidateFrames < _options.StableFrames) return false;

            // пауза после предыдущего события не истекла: изменение не теряется, сработает позже
            if (_lastEventTime != null && timestamp - _lastEventTime.Value < _options.Cooldown) return false;

            if (_options.Condition != null && !_options.Condition(relevant)) return false;

            _analyzedSignature = signature;
            _lastEventTime = timestamp;
            return true;
        }

        /// <summary>
        /// Состав объектов в виде строки: "car:2;person:1"
        /// </summary>
        private static string Signature(List<YOLO26Result> detections)
            => string.Join(";", detections
                .GroupBy(d => d.Label)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}:{g.Count()}"));
    }
}
