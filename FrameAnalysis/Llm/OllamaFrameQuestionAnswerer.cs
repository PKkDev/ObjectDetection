using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Llm
{
    /// <summary>
    /// Проверенные локальные модели с поддержкой изображений (имена из реестра Ollama)
    /// </summary>
    public static class OllamaModels
    {
        /// <summary>
        /// Около 3 ГБ, целиком помещается в 6 ГБ видеопамяти. Быстрая, но слабее на нескольких изображениях
        /// </summary>
        public const string Gemma3_4b = "gemma3:4b";

        /// <summary>
        /// Около 6 ГБ, в 6 ГБ видеопамяти целиком не помещается и работает медленнее
        /// </summary>
        public const string Qwen25Vl_7b = "qwen2.5vl:7b";
    }

    public class OllamaAnswererOptions
    {
        /// <summary>
        /// Адрес локального сервера Ollama. 127.0.0.1:11434 - адрес, на котором Ollama слушает по умолчанию;
        /// меняется на стороне Ollama переменной окружения OLLAMA_HOST
        /// </summary>
        public Uri BaseAddress { get; set; } = new("http://localhost:11434");

        /// <summary>
        /// Модель с поддержкой изображений; должна быть заранее скачана командой "ollama pull"
        /// </summary>
        public string Model { get; set; } = OllamaModels.Gemma3_4b;

        /// <summary>
        /// Локальная модель отвечает медленно, особенно при первом обращении, когда она загружается в память
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Размер окна контекста в токенах. По умолчанию у Ollama он небольшой,
        /// и запрос с несколькими изображениями может не поместиться - тогда он молча обрезается
        /// </summary>
        public int ContextSize { get; set; } = 8192;

        public FramePromptOptions Prompt { get; set; } = new();
    }

    /// <summary>
    /// Отвечает на вопросы по кадру через локальную модель, запущенную в Ollama.
    /// Бесплатно, кадры не покидают компьютер
    /// </summary>
    public class OllamaFrameQuestionAnswerer : IFrameQuestionAnswerer
    {
        private readonly HttpClient _http;
        private readonly OllamaAnswererOptions _options;
        private readonly FramePromptBuilder _promptBuilder;
        private bool _modelChecked;

        public OllamaFrameQuestionAnswerer(OllamaAnswererOptions? options = null, HttpClient? httpClient = null)
        {
            _options = options ?? new OllamaAnswererOptions();
            _http = httpClient ?? new HttpClient { Timeout = _options.Timeout };
            _promptBuilder = new FramePromptBuilder(_options.Prompt);
        }

        /// <summary>
        /// Имена моделей, скачанных в Ollama
        /// </summary>
        public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            using var response = await SendAsync(() => _http.GetAsync(Endpoint("api/tags"), cancellationToken));
            await EnsureSuccessAsync(response, cancellationToken);

            var tags = await response.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken);
            return tags?.Models?.Select(m => m.Name).OfType<string>().ToList() ?? [];
        }

        /// <summary>
        /// Проверяет, что Ollama запущена и выбранная модель скачана.
        /// Вызывается автоматически перед первым вопросом; можно вызвать заранее, чтобы узнать о проблеме при старте
        /// </summary>
        public async Task EnsureModelAvailableAsync(CancellationToken cancellationToken = default)
        {
            var available = await GetAvailableModelsAsync(cancellationToken);

            // "gemma3" без тега Ollama понимает как "gemma3:latest"
            string wanted = _options.Model.Contains(':') ? _options.Model : $"{_options.Model}:latest";

            if (!available.Contains(wanted, StringComparer.OrdinalIgnoreCase))
            {
                string list = available.Count > 0 ? string.Join(", ", available) : "нет ни одной";
                throw new FrameQuestionFailedException(
                    $"Модель {_options.Model} не скачана в Ollama. Скачанные модели: {list}. Скачать: ollama pull {_options.Model}");
            }

            _modelChecked = true;
        }

        public async Task<FrameAnswer> AskAsync(SKBitmap frame, IReadOnlyList<YOLO26Result> detections, string question, CancellationToken cancellationToken = default)
        {
            if (!_modelChecked)
                await EnsureModelAvailableAsync(cancellationToken);

            var prompt = _promptBuilder.Build(frame, detections, question);

            var images = new List<string> { Convert.ToBase64String(prompt.FrameJpeg) };
            images.AddRange(prompt.Crops.Select(c => Convert.ToBase64String(c.Jpeg)));

            var request = new ChatRequest
            {
                Model = _options.Model,
                Stream = false,
                // схема заставляет модель вернуть JSON нужного вида
                Format = FrameAnswer.JsonSchema,
                Options = new ChatOptions { Temperature = 0, ContextSize = _options.ContextSize },
                Messages =
                [
                    new ChatMessage { Role = "system", Content = FramePrompts.System },
                    new ChatMessage { Role = "user", Content = prompt.Text, Images = images },
                ],
            };

            using var response = await SendAsync(() => _http.PostAsJsonAsync(Endpoint("api/chat"), request, cancellationToken));
            await EnsureSuccessAsync(response, cancellationToken);

            var chat = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
            if (string.IsNullOrWhiteSpace(chat?.Message?.Content))
                throw new FrameQuestionFailedException("Ollama вернула пустой ответ");

            return FrameAnswer.Parse(chat.Message.Content) with
            {
                Model = chat.Model,
                InputTokens = chat.PromptEvalCount,
                OutputTokens = chat.EvalCount,
            };
        }

        private Uri Endpoint(string path) => new(_options.BaseAddress, path);

        private async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            try
            {
                return await send();
            }
            catch (HttpRequestException ex)
            {
                throw new FrameQuestionFailedException(
                    $"Не удалось подключиться к Ollama по адресу {_options.BaseAddress}. Проверьте, что Ollama установлена и запущена. {ex.Message}");
            }
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode) return;

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new FrameQuestionFailedException($"Ollama вернула ошибку {(int)response.StatusCode}: {body}");
        }

        private sealed class ChatRequest
        {
            [JsonPropertyName("model")]
            public required string Model { get; init; }

            [JsonPropertyName("stream")]
            public bool Stream { get; init; }

            [JsonPropertyName("format")]
            public required IReadOnlyDictionary<string, JsonElement> Format { get; init; }

            [JsonPropertyName("options")]
            public required ChatOptions Options { get; init; }

            [JsonPropertyName("messages")]
            public required List<ChatMessage> Messages { get; init; }
        }

        private sealed class ChatOptions
        {
            [JsonPropertyName("temperature")]
            public float Temperature { get; init; }

            [JsonPropertyName("num_ctx")]
            public int ContextSize { get; init; }
        }

        private sealed class ChatMessage
        {
            [JsonPropertyName("role")]
            public string? Role { get; init; }

            [JsonPropertyName("content")]
            public string? Content { get; init; }

            [JsonPropertyName("images")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<string>? Images { get; init; }
        }

        private sealed class ChatResponse
        {
            [JsonPropertyName("model")]
            public string? Model { get; init; }

            [JsonPropertyName("message")]
            public ChatMessage? Message { get; init; }

            [JsonPropertyName("prompt_eval_count")]
            public long PromptEvalCount { get; init; }

            [JsonPropertyName("eval_count")]
            public long EvalCount { get; init; }
        }

        private sealed class TagsResponse
        {
            [JsonPropertyName("models")]
            public List<TagModel>? Models { get; init; }
        }

        private sealed class TagModel
        {
            [JsonPropertyName("name")]
            public string? Name { get; init; }
        }
    }
}
