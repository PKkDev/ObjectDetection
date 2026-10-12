using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace FrameAnalysis.Llm.Answerers.Claude
{
    /// <summary>
    /// Отвечает на вопросы по кадру через Claude API.
    /// Ключ берется из переменной окружения ANTHROPIC_API_KEY, если клиент не передан явно
    /// </summary>
    public class ClaudeFrameQuestionAnswerer : IFrameQuestionAnswerer
    {
        private readonly AnthropicClient _client;
        private readonly ClaudeAnswererOptions _options;
        private readonly FramePromptBuilder _promptBuilder;

        public ClaudeFrameQuestionAnswerer(ClaudeAnswererOptions? options = null, AnthropicClient? client = null)
        {
            _options = options ?? new ClaudeAnswererOptions();
            _client = client ?? new AnthropicClient();
            _promptBuilder = new FramePromptBuilder(_options.Prompt);
        }

        public async Task<FrameAnswer> AskAsync(SKBitmap frame, IReadOnlyList<YOLO26Result> detections, string question, CancellationToken cancellationToken = default)
        {
            var prompt = _promptBuilder.Build(frame, detections, question);

            // сначала изображения, потом текст с вопросом
            var content = new List<BetaContentBlockParam> { JpegBlock(prompt.FrameJpeg) };
            content.AddRange(prompt.Crops.Select(c => (BetaContentBlockParam)JpegBlock(c.Jpeg)));
            content.Add(new BetaTextBlockParam { Text = prompt.Text });

            var response = await _client.Beta.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = FramePrompts.System,
                // если запрос отклонит защитный классификатор, сервер сам повторит его на запасной модели
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
                OutputConfig = new BetaOutputConfig
                {
                    Format = new BetaJsonOutputFormat { Schema = FrameAnswer.JsonSchema },
                },
                Messages = [new BetaMessageParam { Role = Role.User, Content = content }],
            }, cancellationToken);

            if (response.StopReason == "refusal")
                throw new FrameQuestionFailedException("Модель отказалась отвечать на вопрос по этому кадру");

            if (response.StopReason == "max_tokens")
                throw new FrameQuestionFailedException($"Ответ не уместился в {_options.MaxTokens} токенов");

            string json = string.Concat(response.Content
                .Select(b => b.TryPickText(out BetaTextBlock? text) ? text.Text : null));

            return FrameAnswer.Parse(json) with
            {
                Model = response.Model,
                InputTokens = response.Usage.InputTokens,
                OutputTokens = response.Usage.OutputTokens,
            };
        }

        private static BetaImageBlockParam JpegBlock(byte[] jpeg) => new()
        {
            Source = new BetaBase64ImageSource
            {
                Data = Convert.ToBase64String(jpeg),
                MediaType = MediaType.ImageJpeg,
            },
        };
    }
}
