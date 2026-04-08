from ultralytics import YOLO
import cv2

# Загружаем твою модель (убедись, что файл .pt на месте)
model = YOLO("yolo26m.pt") 
 
# Экспортируем
model.export(
    format="onnx",
    imgsz=640,           # размер, который мы обсуждали (YOLO стандарт)
    #opset=12,            # оптимально для совместимости с ML.NET / ONNX Runtime
    dynamic=False,       # для парковки лучше статический размер (быстрее)
    simplify=True        # упрощает граф модели, убирая лишние узлы
)


# 2. Выполняем предсказание 
results = model.predict(source="D:\\Projects\\ObjectDetection\\onnxMap\\20260326211505.png", conf=0.25)

# 3. Визуализация (у YOLO есть встроенный метод plot)
for r in results:
    # Метод .plot() возвращает массив BGR (OpenCV формат) с уже нарисованными рамками
    annotated_frame = r.plot(line_width=1, font_size=1)
    
    # Отображаем результат через OpenCV
    cv2.imshow("YOLO26 Native Detection", annotated_frame)
    cv2.waitKey(0)

cv2.destroyAllWindows()