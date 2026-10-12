using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    /// <summary>
    /// Ответ POST /api/chat
    /// </summary>
    internal sealed class OllamaChatResponse
    {
        [JsonPropertyName("model")]
        public string? Model { get; init; }

        [JsonPropertyName("message")]
        public OllamaChatMessage? Message { get; init; }

        /// <summary>
        /// Токенов в запросе
        /// </summary>
        [JsonPropertyName("prompt_eval_count")]
        public long PromptEvalCount { get; init; }

        /// <summary>
        /// Токенов в ответе
        /// </summary>
        [JsonPropertyName("eval_count")]
        public long EvalCount { get; init; }
    }
}
