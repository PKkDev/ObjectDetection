# ObjectDetection

Детекция объектов на .NET через ML.NET и ONNX Runtime. В репозитории две независимые части:

- **YOLO26** (.NET 10) — консольная утилита: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон.
- **YOLOv3 / YOLOv4** (.NET 6/7) — WinUI 3-приложение с детекцией на картинке, камере и видео, плюс консольные утилиты для подготовки моделей.

## Структура

| Папка | Что это |
| --- | --- |
| `onnxMap/` | Python-скрипты: экспорт YOLO26 из `.pt` в `.onnx` и проверка результата |
| `YOLO26.Shared/` | Структуры данных YOLO26 |
| `YOLO26.Transformer/` | Консоль: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон |
| `YOLO3.Shared/`, `YOLO4.Shared/` | Структуры данных и разбор выходов YOLOv3 / YOLOv4 |
| `YOLO3.Transformer/`, `YOLO4.Transformer/` | Консоль: `.onnx` → модель ML.NET (`.zip`) + тестовый прогон |
| `ObjectDetection.ConsoleApp/` | Консоль: прогон сохранённой модели YOLOv4 (`.zip`) |
| `ObjectDetection.WinApp/` | WinUI 3-приложение |
| `assets/images/` | Тестовые картинки |

## Требования

- Windows 10/11 x64
- .NET SDK 10 (собирает все консольные проекты); для запуска старых проектов нужны рантаймы .NET 7 и .NET 6
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
| `modelYolo4Path.zip` | результат `YOLO4.Transformer` (`Model/yolov4.zip`), переименовать | `ObjectDetection.WinApp/Assets/YoloModel/` и `ObjectDetection.ConsoleApp/YoloModel/` |

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

`YOLO3.Transformer` использует `Microsoft.ML.OnnxRuntime.Gpu` и настроен на видеокарту NVIDIA (`gpuDeviceId: 1`, `fallbackToCpu: false`). Без CUDA эти параметры в `Program.cs` нужно поменять.

### ObjectDetection.ConsoleApp

```bash
cd ObjectDetection.ConsoleApp
dotnet run
```

Прогоняет сохранённую модель `YoloModel/modelYolo4Path.zip` на `assets/images/image4.jpg` (или на картинке из аргумента).

### ObjectDetection.WinApp

1. Положить `modelYolo4Path.zip` и `modelYolo312int8Path.zip` в `ObjectDetection.WinApp/Assets/YoloModel/`.
2. Открыть `ObjectDetection.sln` в Visual Studio, выбрать платформу `x64` и стартовый проект `ObjectDetection.WinApp`.
3. Запустить (F5).

Страницы «картинка» и «видео» работают на YOLOv4, «камера» — на YOLOv3.

## Источники

Тестовые картинки в `assets/images/` взяты из Wikimedia Commons, ссылки и лицензии — в `assets/images/wikimedia.md`.
