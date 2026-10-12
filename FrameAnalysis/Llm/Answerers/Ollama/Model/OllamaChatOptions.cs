using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    internal sealed class OllamaChatOptions
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; init; }

        /// <summary>
        /// Размер окна контекста в токенах
        /// </summary>
        [JsonPropertyName("num_ctx")]
        public int ContextSize { get; init; }
    }
}
