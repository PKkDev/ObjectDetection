from ultralytics import YOLO
import cv2
import onnxruntime as ort
import numpy as np
 
# Загружаем вашу обученную модель (.pt)
#model = YOLO("yolo26n.pt") 

# Экспорт с головой One-to-One (идеально для C#)
#model.export(format="onnx", imgsz=640, dynamic=False)

# 1. Создаем сессию (используем CPU, так как в ML.NET чаще начинают с него)
session = ort.InferenceSession("yolo26n.onnx", providers=['CPUExecutionProvider'])
 
# 2. Загрузка исходного изображения
original_img = cv2.imread("D:\\Projects\\ObjectDetection\\onnxMap\\20260326211505.png")
h_orig, w_orig = original_img.shape[:2]

# Предобработка: ресайз до 640x640 для модели
input_size = 640
img_resized = cv2.resize(original_img, (input_size, input_size))
img_rgb = cv2.cvtColor(img_resized, cv2.COLOR_BGR2RGB)

# Нормализация и изменение осей (HWC -> CHW)
input_tensor = img_rgb.transpose(2, 0, 1).astype(np.float32) / 255.0
input_tensor = np.expand_dims(input_tensor, axis=0) # [1, 3, 640, 640]

# 3. Инференс
outputs = session.run(None, {"images": input_tensor})
predictions = outputs[0] # Ожидаем [1, 300, 6] для головы One-to-One

# Коэффициенты масштабирования для возврата к оригинальному размеру
x_scale = w_orig / input_size
y_scale = h_orig / input_size
 
# 5. Отрисовка на оригинальном (не сжатом) изображении
for i in range(300):
    pred = predictions[0, i]
    score = pred[4]

    if score > 0.00:
        print(f"Объект класса {int(pred[5])} найден с вероятностью {score:.2f}")
    
    if score > 0.1: # Порог уверенности
        # Координаты в системе 640x640
        x1, y1, x2, y2 = pred[:4]
        class_id = int(pred[5])
        
        # Пересчитываем в пиксели оригинала
        x1_real = int(x1 * x_scale)
        y1_real = int(y1 * y_scale)
        x2_real = int(x2 * x_scale)
        y2_real = int(y2 * y_scale)
        
        # --- НАСТРОЙКИ ВИЗУАЛИЗАЦИИ ---
        color = (0, 255, 0) # Зеленый (BGR)
        thickness = 2       # МИНИМАЛЬНАЯ ТОЛЩИНА ЛИНИИ
        font_scale = 1    # МАЛЕНЬКИЙ ШРИФТ
        
        # Рисуем рамку
        cv2.rectangle(original_img, (x1_real, y1_real), (x2_real, y2_real), color, thickness)
        
        # Пишем ID и Score
        label = f"ID:{class_id} {score:.2f}"
        cv2.putText(original_img, label, (x1_real, y1_real - 5), cv2.FONT_HERSHEY_SIMPLEX, font_scale, color, thickness)

# 6. Показ результата в полном разрешении
cv2.namedWindow("Full Resolution", cv2.WINDOW_NORMAL) # Позволяет менять размер окна
cv2.imshow("Full Resolution", original_img)
cv2.waitKey(0)
cv2.destroyAllWindows()