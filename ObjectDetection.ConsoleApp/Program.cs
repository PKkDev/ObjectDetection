using FrameAnalysis.Llm;
using FrameAnalysis.Llm.Answerers;
using FrameAnalysis.Llm.Answerers.Claude;
using FrameAnalysis.Llm.Answerers.Ollama;
using Microsoft.ML;
using SkiaSharp;
using YOLO26.Shared.YOLOWorker;
using YOLO26.Shared.YOLOWorker.DataStructures;
using YOLO26.Shared.YOLOWorker.Parser;

// Проверка всего конвейера: кадр -> YOLO26 -> запрос -> LLM -> структурированный ответ.
// Аргументы (оба необязательные): вопрос и путь к кадру

// Переключатель LLM
var llm = Llm.OllamaGemma3;
//var llm = Llm.OllamaQwen25Vl;
//var llm = Llm.Claude;

var question = args.Length > 0 ? args[0] : "Сколько машин на кадре?";

// модель - результат работы проекта YOLO26.Transformer (.zip), кадр по умолчанию берется оттуда же
var transformerFolder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../YOLO26.Transformer"));
var modelPath = Path.Combine(transformerFolder, "Model", "yolo26m.zip");
var imagePath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(transformerFolder, "Images", "1.png");

try
{
    if (!File.Exists(modelPath)) throw new FileNotFoundException($"Модель YOLO26 не найдена: {modelPath}. Сначала запустите проект YOLO26.Transformer - он ее сохраняет", modelPath);
    if (!File.Exists(imagePath)) throw new FileNotFoundException($"Кадр не найден: {imagePath}", imagePath);

    IFrameQuestionAnswerer answerer = llm switch
    {
        // локальные модели в Ollama: бесплатно, кадр не покидает компьютер
        Llm.OllamaGemma3 => new OllamaFrameQuestionAnswerer(new OllamaAnswererOptions { Model = OllamaModel.Gemma3_4b }),
        Llm.OllamaQwen25Vl => new OllamaFrameQuestionAnswerer(new OllamaAnswererOptions { Model = OllamaModel.Qwen25Vl_7b }),
        // Claude API: вызов платный и отправляет кадр наружу; ключ - в переменной окружения ANTHROPIC_API_KEY
        Llm.Claude => new ClaudeFrameQuestionAnswerer(),
        _ => throw new ArgumentOutOfRangeException(nameof(llm)),
    };

    // о недоступной модели узнаем сразу, а не после детекции
    if (answerer is OllamaFrameQuestionAnswerer ollama)
        await ollama.EnsureModelAvailableAsync();

    // модель ML.NET (.zip) загружается так же, как это делает потребитель; в приложении вместо этого - PredictionEnginePool
    var mlContext = new MLContext();
    ITransformer model = mlContext.Model.Load(modelPath, out _);
    var predictionEngine = mlContext.Model.CreatePredictionEngine<Yolo26InputData, Yolo26OutputData>(model);
    var outputParser = new Yolo26OutputParser();

    using var frame = SKBitmap.Decode(imagePath);
    var output = predictionEngine.Predict(Yolo26ImagePreprocessor.ToInput(frame));
    var detections = outputParser.ParseOutputs(output, frame.Width, frame.Height);

    Console.WriteLine($"Кадр: {imagePath}");
    Console.WriteLine($"Найдено объектов: {detections.Count}");
    foreach (var det in detections.OrderByDescending(d => d.Score))
        Console.WriteLine($"  {det.Label} [{det.Score:P0}] ({det.X1:0}, {det.Y1:0})-({det.X2:0}, {det.Y2:0})");

    var answer = await answerer.AskAsync(frame, detections, question);

    Console.WriteLine();
    Console.WriteLine($"Вопрос: {question}");
    Console.WriteLine($"Ответ: {answer.Answer}");
    Console.WriteLine($"Можно ответить по кадру: {(answer.CanAnswer ? "да" : "нет")}, уверенность: {answer.Confidence}, объекты: {string.Join(", ", answer.ReferencedObjects)}");
    Console.WriteLine($"Модель: {answer.Model}, токены: {answer.InputTokens} на входе, {answer.OutputTokens} на выходе");
}
catch (Exception e) when (e is FrameQuestionFailedException or FileNotFoundException or FormatException)
{
    Console.WriteLine($"Ошибка: {e.Message}");
}

enum Llm
{
    OllamaGemma3,
    OllamaQwen25Vl,
    Claude,
}
