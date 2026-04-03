"""Depth Anything monocular depth estimation."""
import io
import torch
from PIL import Image
from transformers import pipeline


class DepthModel:
    def __init__(self):
        self.pipe = pipeline("depth-estimation", model="LiheYoung/depth-anything-small-hf",
                            device=0 if torch.cuda.is_available() else -1)

    def estimate(self, img_bytes: bytes, output_path: str = "") -> str:
        image = Image.open(io.BytesIO(img_bytes)).convert("RGB")
        result = self.pipe(image)
        depth_map = result["depth"]
        if output_path:
            depth_map.save(output_path)
            return output_path
        return "depth_estimated"
