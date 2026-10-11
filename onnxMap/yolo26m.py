from pathlib import Path

from ultralytics import YOLO
import cv2

BASE_DIR = Path(__file__).resolve().parent

model = YOLO(str(BASE_DIR / "yolo26m.pt"))

# Экспортируем
model.export(
   format="onnx",
   imgsz=640,           # размер, который мы обсуждали (YOLO стандарт)
   #opset=12,           # оптимально для совместимости с ML.NET / ONNX Runtime
   dynamic=False,       # статический размер входа (быстрее)
   simplify=True        # упрощает граф модели, убирая лишние узлы
)

results = model.predict(source=str(BASE_DIR / "images" / "3.png"), conf=0.25)

for r in results:
    annotated_frame = r.plot(line_width=1, font_size=1)
    cv2.imshow("YOLO26 Native Detection", annotated_frame)
    cv2.waitKey(0)

cv2.destroyAllWindows()
