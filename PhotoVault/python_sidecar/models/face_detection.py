"""Face detection using MediaPipe."""
import io
import numpy as np
from PIL import Image
import mediapipe as mp


class FaceDetectionModel:
    def __init__(self):
        self.detector = mp.solutions.face_detection.FaceDetection(
            model_selection=1, min_detection_confidence=0.5)

    def detect(self, img_bytes: bytes) -> list:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        img_array = np.array(image)
        results = self.detector.process(img_array)
        faces = []
        if results.detections:
            for det in results.detections:
                bbox = det.location_data.relative_bounding_box
                faces.append({
                    "x": float(bbox.xmin),
                    "y": float(bbox.ymin),
                    "w": float(bbox.width),
                    "h": float(bbox.height),
                    "confidence": float(det.score[0]),
                    "embedding": None,
                })
        return faces