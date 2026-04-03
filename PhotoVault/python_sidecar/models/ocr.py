"""PaddleOCR text extraction."""
import io
import numpy as np
from PIL import Image


class OcrModel:
    def __init__(self):
        from paddleocr import PaddleOCR
        self.ocr = PaddleOCR(lang="en")

    def extract(self, img_bytes: bytes) -> str:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        img_array = np.array(image)
        try:
            results = self.ocr.ocr(img_array)
            texts = []
            if results and results[0]:
                for line in results[0]:
                    if line and len(line) >= 2:
                        text = line[1][0] if isinstance(line[1], (tuple, list)) else str(line[1])
                        texts.append(text)
            return " ".join(texts)
        except Exception as e:
            return ""