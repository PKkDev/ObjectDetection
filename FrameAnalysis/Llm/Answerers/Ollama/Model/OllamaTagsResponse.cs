using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    /// <summary>
    /// Ответ GET /api/tags - список скачанных моделей
    /// </summary>
    internal sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaTagModel>? Models { get; init; }
    }

    internal sealed class OllamaTagModel
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
