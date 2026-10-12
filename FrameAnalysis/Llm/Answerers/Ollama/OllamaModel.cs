namespace FrameAnalysis.Llm.Answerers.Ollama
{
    /// <summary>
    /// Проверенные локальные модели с поддержкой изображений
    /// </summary>
    public enum OllamaModel
    {
        /// <summary>
        /// gemma3:4b - около 3 ГБ, целиком помещается в 6 ГБ видеопамяти. Быстрая, но слабее на нескольких изображениях
        /// </summary>
        Gemma3_4b,

        /// <summary>
        /// qwen2.5vl:7b - около 6 ГБ, в 6 ГБ видеопамяти целиком не помещается и работает медленнее
        /// </summary>
        Qwen25Vl_7b,
    }

    public static class OllamaModelExtensions
    {
        /// <summary>
        /// Имя модели в реестре Ollama - то, что передается в "ollama pull"
        /// </summary>
        public static string ToModelName(this OllamaModel model) => model switch
        {
            OllamaModel.Gemma3_4b => "gemma3:4b",
            OllamaModel.Qwen25Vl_7b => "qwen2.5vl:7b",
            _ => throw new ArgumentOutOfRangeException(nameof(model), model, "Неизвестная модель Ollama"),
        };
    }
}
