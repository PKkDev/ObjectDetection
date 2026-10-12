using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    /// <summary>
    /// Тело запроса POST /api/chat
    /// </summary>
    internal sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public required string Model { get; init; }

        [JsonPropertyName("stream")]
        public bool Stream { get; init; }

        /// <summary>
        /// JSON-схема, которой должен соответствовать ответ модели
        /// </summary>
        [JsonPropertyName("format")]
        public required IReadOnlyDictionary<string, JsonElement> Format { get; init; }

        [JsonPropertyName("options")]
        public required OllamaChatOptions Options { get; init; }

        [JsonPropertyName("messages")]
        public required List<OllamaChatMessage> Messages { get; init; }
    }
}
