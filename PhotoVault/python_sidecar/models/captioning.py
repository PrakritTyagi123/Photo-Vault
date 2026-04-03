"""Image captioning using BLIP."""
import io
import torch
from PIL import Image
from transformers import BlipProcessor, BlipForConditionalGeneration


class CaptioningModel:
    def __init__(self):
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.processor = BlipProcessor.from_pretrained("Salesforce/blip-image-captioning-base")
        self.model = BlipForConditionalGeneration.from_pretrained("Salesforce/blip-image-captioning-base").to(self.device)
        self.model.eval()

    def caption(self, img_bytes: bytes) -> str:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        inputs = self.processor(image, return_tensors="pt").to(self.device)
        with torch.no_grad():
            ids = self.model.generate(**inputs, max_new_tokens=100)
        return self.processor.decode(ids[0], skip_special_tokens=True)