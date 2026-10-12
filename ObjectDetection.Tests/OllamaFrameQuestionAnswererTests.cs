using System.Net;
using System.Text;
using System.Text.Json;
using FrameAnalysis.Llm;
using FrameAnalysis.Llm.Answerers;
using FrameAnalysis.Llm.Answerers.Ollama;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker.DataStructures;

namespace ObjectDetection.Tests
{
    public class OllamaFrameQuestionAnswererTests
    {
        /// <summary>
        /// Подмена сервера Ollama: на запрос списка моделей отдает заданный список,
        /// запрос к модели запоминает и возвращает заготовленный ответ
        /// </summary>
        private sealed class FakeOllama : HttpMessageHandler
        {
            private readonly Func<HttpResponseMessage> _respond;
            private readonly string[] _models;

            public FakeOllama(Func<HttpResponseMessage> respond, params string[] models)
            {
                _respond = respond;
                _models = models.Length > 0 ? models : ["gemma3:4b", "qwen2.5vl:7b"];
            }

            public Uri? RequestUri { get; private set; }

            public JsonElement RequestBody { get; private set; }

            public int TagsRequests { get; private set; }

            public int ChatRequests { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath == "/api/tags")
                {
                    TagsRequests++;
                    var tags = JsonSerializer.Serialize(new { models = _models.Select(name => new { name }) });
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tags, Encoding.UTF8, "application/json") };
                }

                ChatRequests++;
                RequestUri = request.RequestUri;
                RequestBody = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement;
                return _respond();
            }
        }

        private const string AnswerJson =
            """{"answer": "Две машины", "can_answer": true, "confidence": "medium", "referenced_objects": [1, 2]}""";

        private static HttpResponseMessage Ok(string content, string model = "gemma3:4b")
        {
            var body = JsonSerializer.Serialize(new
            {
                model,
                message = new { role = "assistant", content },
                prompt_eval_count = 1200,
                eval_count = 40,
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        private static SKBitmap Frame()
        {
            var bitmap = new SKBitmap(640, 480);
            bitmap.Erase(SKColors.Gray);
            return bitmap;
        }

        private static YOLO26Result[] TwoCars() =>
        [
            new() { Label = "car", Score = 0.9f, X1 = 10, Y1 = 10, X2 = 110, Y2 = 90 },
            new() { Label = "car", Score = 0.8f, X1 = 300, Y1 = 200, X2 = 420, Y2 = 290 },
        ];

        [Fact]
        public async Task AskAsync_ParsesAnswerAndUsage()
        {
            var server = new FakeOllama(() => Ok(AnswerJson));
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));
            using var frame = Frame();

            var answer = await answerer.AskAsync(frame, TwoCars(), "Сколько машин?");

            Assert.Equal("Две машины", answer.Answer);
            Assert.True(answer.CanAnswer);
            Assert.Equal(AnswerConfidence.Medium, answer.Confidence);
            Assert.Equal([1, 2], answer.ReferencedObjects);
            Assert.Equal("gemma3:4b", answer.Model);
            Assert.Equal(1200, answer.InputTokens);
            Assert.Equal(40, answer.OutputTokens);
        }

        [Fact]
        public async Task AskAsync_SendsChatRequestWithSchemaAndImages()
        {
            var server = new FakeOllama(() => Ok(AnswerJson));
            var options = new OllamaAnswererOptions { Model = OllamaModel.Qwen25Vl_7b, BaseAddress = new Uri("http://localhost:11434") };
            var answerer = new OllamaFrameQuestionAnswerer(options, new HttpClient(server));
            using var frame = Frame();

            await answerer.AskAsync(frame, TwoCars(), "Сколько машин?");

            Assert.Equal("http://localhost:11434/api/chat", server.RequestUri!.ToString());

            var body = server.RequestBody;
            Assert.Equal("qwen2.5vl:7b", body.GetProperty("model").GetString());
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.Equal(0, body.GetProperty("options").GetProperty("temperature").GetDouble());
            Assert.Equal(8192, body.GetProperty("options").GetProperty("num_ctx").GetInt32());

            // схема ответа уходит целиком
            var format = body.GetProperty("format");
            Assert.Equal("object", format.GetProperty("type").GetString());
            Assert.True(format.GetProperty("properties").TryGetProperty("can_answer", out _));

            var messages = body.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());

            var system = messages[0];
            Assert.Equal("system", system.GetProperty("role").GetString());
            Assert.Equal(FramePrompts.System, system.GetProperty("content").GetString());
            Assert.False(system.TryGetProperty("images", out _));

            var user = messages[1];
            Assert.Equal("user", user.GetProperty("role").GetString());
            Assert.EndsWith("Вопрос: Сколько машин?", user.GetProperty("content").GetString());

            // кадр целиком и по фрагменту на каждый из двух объектов, все - корректные JPEG
            var images = user.GetProperty("images");
            Assert.Equal(3, images.GetArrayLength());
            foreach (var image in images.EnumerateArray())
            {
                using var decoded = SKBitmap.Decode(Convert.FromBase64String(image.GetString()!));
                Assert.NotNull(decoded);
            }
        }

        [Fact]
        public async Task AskAsync_ServerError_ThrowsWithServerMessage()
        {
            var server = new FakeOllama(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error": "out of memory"}"""),
            });
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));
            using var frame = Frame();

            var ex = await Assert.ThrowsAsync<FrameQuestionFailedException>(() => answerer.AskAsync(frame, [], "?"));

            Assert.Contains("500", ex.Message);
            Assert.Contains("out of memory", ex.Message);
        }

        [Fact]
        public async Task AskAsync_ModelNotDownloaded_ThrowsBeforeAskingAndListsAvailableModels()
        {
            var server = new FakeOllama(() => Ok(AnswerJson), "gemma3:4b", "llava:latest");
            var options = new OllamaAnswererOptions { Model = OllamaModel.Qwen25Vl_7b };
            var answerer = new OllamaFrameQuestionAnswerer(options, new HttpClient(server));
            using var frame = Frame();

            var ex = await Assert.ThrowsAsync<FrameQuestionFailedException>(() => answerer.AskAsync(frame, [], "?"));

            Assert.Contains("qwen2.5vl:7b не скачана", ex.Message);
            Assert.Contains("gemma3:4b, llava:latest", ex.Message);
            Assert.Contains("ollama pull qwen2.5vl:7b", ex.Message);
            Assert.Equal(0, server.ChatRequests);
        }

        [Fact]
        public async Task AskAsync_ChecksModelOnlyOnce()
        {
            var server = new FakeOllama(() => Ok(AnswerJson));
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));
            using var frame = Frame();

            await answerer.AskAsync(frame, [], "?");
            await answerer.AskAsync(frame, [], "?");

            Assert.Equal(1, server.TagsRequests);
            Assert.Equal(2, server.ChatRequests);
        }

        [Theory]
        [InlineData(OllamaModel.Gemma3_4b, "gemma3:4b")]
        [InlineData(OllamaModel.Qwen25Vl_7b, "qwen2.5vl:7b")]
        public void ToModelName_ReturnsOllamaRegistryName(OllamaModel model, string expected)
        {
            Assert.Equal(expected, model.ToModelName());
        }

        [Fact]
        public void ToModelName_CoversEveryModel()
        {
            foreach (var model in Enum.GetValues<OllamaModel>())
                Assert.False(string.IsNullOrWhiteSpace(model.ToModelName()));
        }

        [Fact]
        public async Task GetAvailableModelsAsync_ReturnsDownloadedModelNames()
        {
            var server = new FakeOllama(() => Ok(AnswerJson), "gemma3:4b", "qwen2.5vl:7b");
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));

            var models = await answerer.GetAvailableModelsAsync();

            Assert.Equal(["gemma3:4b", "qwen2.5vl:7b"], models);
        }

        [Fact]
        public async Task AskAsync_EmptyAnswer_Throws()
        {
            var server = new FakeOllama(() => Ok(""));
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));
            using var frame = Frame();

            await Assert.ThrowsAsync<FrameQuestionFailedException>(() => answerer.AskAsync(frame, [], "?"));
        }

        [Fact]
        public async Task AskAsync_AnswerIsNotValidJson_ThrowsFormatException()
        {
            var server = new FakeOllama(() => Ok("Две машины"));
            var answerer = new OllamaFrameQuestionAnswerer(httpClient: new HttpClient(server));
            using var frame = Frame();

            await Assert.ThrowsAsync<FormatException>(() => answerer.AskAsync(frame, [], "?"));
        }

        [Fact]
        public async Task AskAsync_ServerNotRunning_ThrowsReadableError()
        {
            // порт 1 закрыт: соединение отклоняется сразу
            var options = new OllamaAnswererOptions { BaseAddress = new Uri("http://127.0.0.1:1") };
            var answerer = new OllamaFrameQuestionAnswerer(options);
            using var frame = Frame();

            var ex = await Assert.ThrowsAsync<FrameQuestionFailedException>(() => answerer.AskAsync(frame, [], "?"));

            Assert.Contains("Ollama", ex.Message);
            Assert.Contains("запущена", ex.Message);
        }
    }
}
