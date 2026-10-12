using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Filtering
{
    public class FrameFilterOptions
    {
        /// <summary>
        /// Названия категорий, за которыми следим (например, "car", "truck"). null - все категории
        /// </summary>
        public IReadOnlySet<string>? WatchedLabels { get; set; }

        /// <summary>
        /// Объекты с уверенностью ниже этого порога фильтр не учитывает
        /// </summary>
        public float MinScore { get; set; } = 0.4f;

        /// <summary>
        /// Сколько кадров подряд должен продержаться новый состав объектов, чтобы считаться событием.
        /// Защищает от мерцания детектора на границе порога
        /// </summary>
        public int StableFrames { get; set; } = 3;

        /// <summary>
        /// Минимальная пауза между двумя событиями
        /// </summary>
        public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Дополнительное условие от вызывающего кода: получает учитываемые объекты кадра,
        /// событие срабатывает только если условие вернуло true. null - без условия
        /// </summary>
        public Func<IReadOnlyList<YOLO26Result>, bool>? Condition { get; set; }
    }
}
