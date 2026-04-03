"""NSFW image detection using Hugging Face classifier."""
import io
import torch
from PIL import Image
from transformers import pipeline


class NsfwModel:
    def __init__(self):
        self.pipe = pipeline("image-classification", model="Falconsai/nsfw_image_detection",
                            device=0 if torch.cuda.is_available() else -1)

    def check(self, img_bytes: bytes) -> bool:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        results = self.pipe(image)
        for r in results:
            if r["label"].lower() == "nsfw" and r["score"] > 0.7:
                return True
        return False
