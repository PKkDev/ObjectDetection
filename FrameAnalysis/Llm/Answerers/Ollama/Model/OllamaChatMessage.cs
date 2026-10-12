using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    internal sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        /// <summary>
        /// Изображения в base64
        /// </summary>
        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; init; }
    }
}
