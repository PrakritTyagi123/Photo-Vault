"""CLIP image embedding for semantic search."""
import io
import torch
import numpy as np
from PIL import Image
import open_clip


class ClipModel:
    def __init__(self):
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.model, _, self.preprocess = open_clip.create_model_and_transforms("ViT-B-32", pretrained="openai")
        self.model = self.model.to(self.device).eval()

    def encode(self, img_bytes: bytes) -> np.ndarray:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        tensor = self.preprocess(image).unsqueeze(0).to(self.device)
        with torch.no_grad():
            features = self.model.encode_image(tensor)
            features /= features.norm(dim=-1, keepdim=True)
        return features.cpu().numpy().flatten()

    def encode_text(self, text: str) -> np.ndarray:
        tokenizer = open_clip.get_tokenizer("ViT-B-32")
        tokens = tokenizer([text]).to(self.device)
        with torch.no_grad():
            features = self.model.encode_text(tokens)
            features /= features.norm(dim=-1, keepdim=True)
        return features.cpu().numpy().flatten()
