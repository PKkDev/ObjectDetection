using System.Net.Http.Json;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Llm.Answerers.Ollama
{
    /// <summary>
    /// Отвечает на вопросы по кадру через локальную модель, запущенную в Ollama.
    /// Бесплатно, кадры не покидают компьютер
    /// </summary>
    public class OllamaFrameQuestionAnswerer : IFrameQuestionAnswerer
    {
        private readonly HttpClient _http;
        private readonly OllamaAnswererOptions _options;
        private readonly FramePromptBuilder _promptBuilder;
        private readonly string _modelName;
        private bool _modelChecked;

        public OllamaFrameQuestionAnswerer(OllamaAnswererOptions? options = null, HttpClient? httpClient = null)
        {
            _options = options ?? new OllamaAnswererOptions();
            _modelName = _options.Model.ToModelName();
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

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken);
            return tags?.Models?.Select(m => m.Name).OfType<string>().ToList() ?? [];
        }

        /// <summary>
        /// Проверяет, что Ollama запущена и выбранная модель скачана.
        /// Вызывается автоматически перед первым вопросом; можно вызвать заранее, чтобы узнать о проблеме при старте
        /// </summary>
        public async Task EnsureModelAvailableAsync(CancellationToken cancellationToken = default)
        {
            var available = await GetAvailableModelsAsync(cancellationToken);

            if (!available.Contains(_modelName, StringComparer.OrdinalIgnoreCase))
            {
                string list = available.Count > 0 ? string.Join(", ", available) : "нет ни одной";
                throw new FrameQuestionFailedException(
                    $"Модель {_modelName} не скачана в Ollama. Скачанные модели: {list}. Скачать: ollama pull {_modelName}");
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

            var request = new OllamaChatRequest
            {
                Model = _modelName,
                Stream = false,
                // схема заставляет модель вернуть JSON нужного вида
                Format = FrameAnswer.JsonSchema,
                Options = new OllamaChatOptions { Temperature = 0, ContextSize = _options.ContextSize },
                Messages =
                [
                    new OllamaChatMessage { Role = "system", Content = FramePrompts.System },
                    new OllamaChatMessage { Role = "user", Content = prompt.Text, Images = images },
                ],
            };

            using var response = await SendAsync(() => _http.PostAsJsonAsync(Endpoint("api/chat"), request, cancellationToken));
            await EnsureSuccessAsync(response, cancellationToken);

            var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken);
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
    }
}
