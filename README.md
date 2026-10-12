# ObjectDetection

Детекция объектов на .NET через ML.NET и ONNX Runtime. В репозитории две независимые части:

- **YOLO26** (.NET 10) — консольная утилита: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон.
- **YOLOv3 / YOLOv4** — консольные утилиты для подготовки моделей (.NET 10) и WinUI 3-приложение с детекцией на картинке, камере и видео (.NET 6).

## Структура

| Папка | Что это |
| --- | --- |
| `onnxMap/` | Python-скрипты: экспорт YOLO26 из `.pt` в `.onnx` и проверка результата |
| `YOLO26.Shared/` | Всё, что нужно потребителю модели YOLO26: входные и выходные данные, подготовка кадра и разбор выхода |
| `FrameAnalysis/` | Конвейер «детектор отбирает кадры → LLM отвечает на вопросы по ним» |
| `YOLO26.Transformer/` | Консоль: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон |
| `YOLO3.Shared/`, `YOLO4.Shared/` | Структуры данных и разбор выходов YOLOv3 / YOLOv4 |
| `YOLO3.Transformer/`, `YOLO4.Transformer/` | Консоль: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон |
| `ObjectDetection.ConsoleApp/` | Консоль: проверка всего конвейера «кадр → YOLO26 → LLM → ответ» |
| `ObjectDetection.WinApp/` | WinUI 3-приложение |
| `assets/images/` | Тестовые картинки |

## Требования

- Windows 10/11 x64
- .NET SDK 10 — для всех консольных проектов и тестов; для запуска WinUI-приложения нужен рантайм .NET 6
- Visual Studio 2022+ с нагрузкой «Разработка приложений WinUI» — только для `ObjectDetection.WinApp`
- Python 3 — только для экспорта YOLO26

## Модели

Файлы моделей (`*.pt`, `*.onnx`, `*.zip`) в git не хранятся, их нужно получить самостоятельно и положить в указанные папки.

| Модель | Откуда | Куда положить |
| --- | --- | --- |
| `yolo26n.onnx`, `yolo26m.onnx` | экспорт скриптами из `onnxMap/` (см. ниже) | `YOLO26.Transformer/Model/` |
| `yolov3-12-int8.onnx` | [ONNX Model Zoo, yolov3](https://github.com/onnx/models/tree/main/validated/vision/object_detection_segmentation/yolov3/model) | `YOLO3.Transformer/Model/` |
| `yolov4.onnx` | [ONNX Model Zoo, yolov4](https://github.com/onnx/models/tree/main/validated/vision/object_detection_segmentation/yolov4/model) | `YOLO4.Transformer/Model/` |
| `modelYolo312int8Path.zip` | результат `YOLO3.Transformer` (`Model/yolov3-12-int8.zip`), переименовать | `ObjectDetection.WinApp/Assets/YoloModel/` |
| `modelYolo4Path.zip` | результат `YOLO4.Transformer` (`Model/yolov4.zip`), переименовать | `ObjectDetection.WinApp/Assets/YoloModel/` |

### Экспорт YOLO26 в ONNX

```bash
pip install ultralytics onnx onnxruntime opencv-python
```

Веса `yolo26n.pt` / `yolo26m.pt` берутся из [релизов Ultralytics](https://github.com/ultralytics/assets/releases) и кладутся в `onnxMap/`. Тестовая картинка — `onnxMap/images/3.png`.

```bash
python onnxMap/yolo26m.py
```

Скрипт создаёт `onnxMap/yolo26m.onnx` и показывает результат детекции. Остальные скрипты:

- `yolo26n.py` — прогон готового `yolo26n.onnx` через ONNX Runtime, тот же препроцессинг, что и в C# (удобно для сверки);
- `yolo26n_pt.py` — прогон исходного `yolo26n.pt` средствами Ultralytics.

Полученный `.onnx` нужно скопировать в `YOLO26.Transformer/Model/`.

## Запуск

Консольные проекты ищут модель относительно текущей папки, поэтому запускать их нужно из папки проекта.

### YOLO26

```bash
cd YOLO26.Transformer
dotnet run
```

Проект работает и на Linux: картинки обрабатываются через SkiaSharp, которому там нужен пакет `libfontconfig1`.

Читает `Model/yolo26m.onnx` и `Images/1.png`, пишет `Images/1-processed.png` с рамками машин и грузовиков, сохраняет модель ML.NET в `Model/yolo26m.zip`. Модель и имя картинки задаются в `Program.cs`.

### YOLOv3 / YOLOv4: подготовка моделей

```bash
cd YOLO4.Transformer
dotnet run
```

```bash
cd YOLO3.Transformer
dotnet run
```

Каждая утилита прогоняет `assets/images/image4.jpg`, кладёт рядом `processed.jpg` и сохраняет модель ML.NET в `Model/*.zip`. Путь к другой картинке можно передать аргументом: `dotnet run -- путь\к\картинке.jpg`.

Обе утилиты считают на CPU. Картинки рисуются через SkiaSharp 2.88 — эта версия нужна для совместимости с `Microsoft.ML.ImageAnalytics`.

### ObjectDetection.ConsoleApp

Проверка всего конвейера на одном кадре: детекция YOLO26, запрос к LLM и структурированный ответ. Подробности — в разделе «Разбор кадров с помощью LLM».

```bash
cd ObjectDetection.ConsoleApp
dotnet run -- "Сколько машин на кадре?"
```

### ObjectDetection.WinApp

1. Положить `modelYolo4Path.zip` и `modelYolo312int8Path.zip` в `ObjectDetection.WinApp/Assets/YoloModel/`.
2. Открыть `ObjectDetection.sln` в Visual Studio, выбрать платформу `x64` и стартовый проект `ObjectDetection.WinApp`.
3. Запустить (F5).

Страницы «картинка» и «видео» работают на YOLOv4, «камера» — на YOLOv3.

## Разбор кадров с помощью LLM

Библиотека `FrameAnalysis` соединяет детектор и LLM: детектор смотрит каждый кадр, а в LLM уходят только кадры-события.

- `DetectionChangeFilter` — отбирает кадры. Событие — устойчивое изменение состава объектов (сколько объектов какой категории), а не сам факт, что на кадре что-то есть. Категории, порог уверенности, число кадров для устойчивости, пауза между событиями и дополнительное условие задаются в `FrameFilterOptions`.
- `FramePromptBuilder` — собирает запрос: кадр с пронумерованными рамками, увеличенные фрагменты объектов с номером на плашке и текст с результатами детекции.
- `OllamaFrameQuestionAnswerer` и `ClaudeFrameQuestionAnswerer` — задают вопрос локальной модели в Ollama или Claude API и возвращают `FrameAnswer`: текст ответа, признак «по кадру ответить нельзя», уверенность и номера объектов, на которые опирается ответ.
- `FrameAnalyzer` — связывает всё вместе: `ProcessAsync(кадр, время, вопрос)` возвращает ответ или `null`, если кадр не прошёл фильтр.

```csharp
// pool - PredictionEnginePool<Yolo26InputData, Yolo26OutputData>, в который загружена модель yolo26m.zip
var parser = new Yolo26OutputParser();
IReadOnlyList<YOLO26Result> Detect(SKBitmap frame)
{
    var output = pool.Predict("Yolo26m", Yolo26ImagePreprocessor.ToInput(frame));
    return parser.ParseOutputs(output, frame.Width, frame.Height);
}

var filter = new DetectionChangeFilter(new FrameFilterOptions { WatchedLabels = new HashSet<string> { "car", "truck" } });
var analyzer = new FrameAnalyzer(Detect, filter, new OllamaFrameQuestionAnswerer());

var result = await analyzer.ProcessAsync(frame, DateTimeOffset.Now, "Есть ли машины с открытым багажником?");
```

### Локальная модель (Ollama)

Бесплатно, кадры не покидают компьютер.

1. Установить [Ollama](https://ollama.com) — после установки она работает как локальный сервер на `http://localhost:11434`.
2. Скачать модель с поддержкой изображений:

```bash
ollama pull gemma3:4b
```

Модель задаётся в `OllamaAnswererOptions.Model`, проверенные имена лежат в `OllamaModels`. `gemma3:4b` выбрана по умолчанию как помещающаяся в 6 ГБ видеопамяти. Перед первым вопросом проверяется, что Ollama запущена и модель скачана; если нет — ошибка перечисляет скачанные модели и команду для загрузки.

### Claude API

Ключ берётся из переменной окружения `ANTHROPIC_API_KEY`. Каждый вызов платный и отправляет кадр во внешний сервис.

### Проверка на одном кадре

Проверяется из `ObjectDetection.ConsoleApp`. Модель YOLO26 — результат работы `YOLO26.Transformer` (`Model/yolo26m.zip`), поэтому сначала нужно запустить его; кадр по умолчанию берётся оттуда же (`Images/1.png`); вопрос и путь к другому кадру можно передать аргументами. LLM выбирается переключателем в начале `Program.cs`: `gemma3:4b` или `qwen2.5vl:7b` в Ollama либо Claude API.

```bash
cd ObjectDetection.ConsoleApp
dotnet run -- "Сколько машин на кадре?" путь\к\кадру.png
```

## Тесты

```bash
dotnet test ObjectDetection.Tests
```

Юнит-тесты на xUnit покрывают разбор выходов YOLOv3, YOLOv4 и YOLO26, подготовку картинки для YOLO26 и конвейер `FrameAnalysis` (фильтр кадров, сборку запроса, разбор ответа). Модели и ключ API для них не нужны: вызов LLM в тестах подменён.

## Источники

Тестовые картинки в `assets/images/` взяты из Wikimedia Commons, ссылки и лицензии — в `assets/images/wikimedia.md`.
