from ultralytics import YOLO
import cv2

# 1. Загружаем модель напрямую из .pt файла
# Библиотека сама определит, что это YOLO26
model = YOLO("yolo26n.pt")


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