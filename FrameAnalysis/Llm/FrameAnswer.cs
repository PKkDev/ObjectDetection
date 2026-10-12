using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameAnalysis.Llm
{
    public enum AnswerConfidence
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Ответ LLM на вопрос по кадру
    /// </summary>
    public record FrameAnswer
    {
        /// <summary>
        /// Текст ответа; если ответить нельзя - объяснение, чего не хватает
        /// </summary>
        public required string Answer { get; init; }

        /// <summary>
        /// false - по этому кадру ответить нельзя (не видно, слишком мелко, вопрос не про кадр)
        /// </summary>
        public required bool CanAnswer { get; init; }

        public required AnswerConfidence Confidence { get; init; }

        /// <summary>
        /// Номера объектов детектора, на которые опирается ответ
        /// </summary>
        public required IReadOnlyList<int> ReferencedObjects { get; init; }

        /// <summary>
        /// Модель, которая фактически ответила
        /// </summary>
        public string? Model { get; init; }

        public long InputTokens { get; init; }

        public long OutputTokens { get; init; }

        /// <summary>
        /// Схема ответа, которую LLM обязана соблюдать (structured outputs)
        /// </summary>
        public static IReadOnlyDictionary<string, JsonElement> JsonSchema { get; } = new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(new
            {
                answer = new { type = "string", description = "Ответ на вопрос на языке вопроса. Если ответить нельзя - что именно мешает" },
                can_answer = new { type = "boolean", description = "Можно ли ответить на вопрос по этому кадру" },
                confidence = new { type = "string", @enum = new[] { "low", "medium", "high" } },
                referenced_objects = new { type = "array", items = new { type = "integer" }, description = "Номера объектов детектора, на которые опирается ответ" },
            }),
            ["required"] = JsonSerializer.SerializeToElement(new[] { "answer", "can_answer", "confidence", "referenced_objects" }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        };

        /// <summary>
        /// Разбирает JSON, который вернула LLM
        /// </summary>
        public static FrameAnswer Parse(string json)
        {
            AnswerDto? dto;
            try
            {
                dto = JsonSerializer.Deserialize<AnswerDto>(json);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"Ответ LLM не является корректным JSON: {ex.Message}", ex);
            }

            if (dto?.Answer == null || dto.CanAnswer == null || dto.Confidence == null)
                throw new FormatException("В ответе LLM нет обязательных полей answer, can_answer, confidence");

            var confidence = dto.Confidence.ToLowerInvariant() switch
            {
                "low" => AnswerConfidence.Low,
                "medium" => AnswerConfidence.Medium,
                "high" => AnswerConfidence.High,
                _ => throw new FormatException($"Неизвестное значение confidence: {dto.Confidence}")
            };

            return new FrameAnswer
            {
                Answer = dto.Answer,
                CanAnswer = dto.CanAnswer.Value,
                Confidence = confidence,
                ReferencedObjects = dto.ReferencedObjects ?? [],
            };
        }

        private sealed class AnswerDto
        {
            [JsonPropertyName("answer")]
            public string? Answer { get; set; }

            [JsonPropertyName("can_answer")]
            public bool? CanAnswer { get; set; }

            [JsonPropertyName("confidence")]
            public string? Confidence { get; set; }

            [JsonPropertyName("referenced_objects")]
            public List<int>? ReferencedObjects { get; set; }
        }
    }
}
