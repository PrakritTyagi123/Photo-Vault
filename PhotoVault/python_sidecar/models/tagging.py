"""YOLOv8 object detection for auto-tagging."""
import io
from PIL import Image
from ultralytics import YOLO


class TaggingModel:
    def __init__(self):
        self.model = YOLO("yolov8n.pt")

    def tag(self, img_bytes: bytes) -> list:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        results = self.model(image, verbose=False)
        tags = set()
        for r in results:
            for box in r.boxes:
                cls_id = int(box.cls[0])
                conf = float(box.conf[0])
                if conf > 0.3:
                    name = self.model.names[cls_id]
                    tags.add(name)
        return sorted(list(tags))
