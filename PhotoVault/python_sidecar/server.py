"""PhotoVault AI Sidecar — FastAPI server for local ML inference."""
import os
import io
import logging
from fastapi import FastAPI, UploadFile, File, Query
from fastapi.responses import JSONResponse, PlainTextResponse
import uvicorn

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("photovault-ai")

app = FastAPI(title="PhotoVault AI Sidecar", version="1.0.0")

# Lazy-loaded models
_captioner = None
_tagger = None
_face_detector = None
_clip_encoder = None
_ocr_engine = None
_depth_estimator = None
_super_res = None
_nsfw_detector = None


def get_image_bytes(file: UploadFile) -> bytes:
    return file.file.read()


@app.get("/health")
async def health():
    return {"status": "ok", "gpu": _check_gpu()}


@app.get("/models")
async def list_models():
    return {
        "captioning": _captioner is not None,
        "tagging": _tagger is not None,
        "face_detection": _face_detector is not None,
        "clip": _clip_encoder is not None,
        "ocr": _ocr_engine is not None,
        "depth": _depth_estimator is not None,
        "super_resolution": _super_res is not None,
        "nsfw": _nsfw_detector is not None,
    }


@app.post("/caption")
async def caption(file: UploadFile = File(...)):
    global _captioner
    if _captioner is None:
        from models.captioning import CaptioningModel
        _captioner = CaptioningModel()
    img_bytes = get_image_bytes(file)
    result = _captioner.caption(img_bytes)
    return PlainTextResponse(result)


@app.post("/tag")
async def tag(file: UploadFile = File(...)):
    global _tagger
    if _tagger is None:
        from models.tagging import TaggingModel
        _tagger = TaggingModel()
    img_bytes = get_image_bytes(file)
    tags = _tagger.tag(img_bytes)
    return JSONResponse(tags)


@app.post("/faces")
async def detect_faces(file: UploadFile = File(...)):
    global _face_detector
    if _face_detector is None:
        from models.face_detection import FaceDetectionModel
        _face_detector = FaceDetectionModel()
    img_bytes = get_image_bytes(file)
    faces = _face_detector.detect(img_bytes)
    return JSONResponse(faces)


@app.post("/clip")
async def clip_embed(file: UploadFile = File(...)):
    global _clip_encoder
    if _clip_encoder is None:
        from models.clip_embeddings import ClipModel
        _clip_encoder = ClipModel()
    img_bytes = get_image_bytes(file)
    embedding = _clip_encoder.encode(img_bytes)
    return JSONResponse(embedding.tolist())


@app.post("/ocr")
async def ocr(file: UploadFile = File(...)):
    global _ocr_engine
    if _ocr_engine is None:
        from models.ocr import OcrModel
        _ocr_engine = OcrModel()
    img_bytes = get_image_bytes(file)
    text = _ocr_engine.extract(img_bytes)
    return PlainTextResponse(text)


@app.post("/depth")
async def depth(file: UploadFile = File(...), output: str = Query("")):
    global _depth_estimator
    if _depth_estimator is None:
        from models.depth import DepthModel
        _depth_estimator = DepthModel()
    img_bytes = get_image_bytes(file)
    result = _depth_estimator.estimate(img_bytes, output)
    return PlainTextResponse(result)


@app.post("/superres")
async def super_resolution(file: UploadFile = File(...), output: str = Query("")):
    global _super_res
    if _super_res is None:
        from models.super_resolution import SuperResModel
        _super_res = SuperResModel()
    img_bytes = get_image_bytes(file)
    result = _super_res.upscale(img_bytes, output)
    return PlainTextResponse(result)


@app.post("/nsfw")
async def nsfw_check(file: UploadFile = File(...)):
    global _nsfw_detector
    if _nsfw_detector is None:
        from models.nsfw_detection import NsfwModel
        _nsfw_detector = NsfwModel()
    img_bytes = get_image_bytes(file)
    is_nsfw = _nsfw_detector.check(img_bytes)
    return PlainTextResponse(str(is_nsfw).lower())


def _check_gpu():
    try:
        import torch
        if torch.cuda.is_available():
            return f"CUDA ({torch.cuda.get_device_name(0)})"
        return "CPU"
    except:
        return "CPU"


if __name__ == "__main__":
    logger.info("Starting PhotoVault AI Sidecar on port 8100...")
    uvicorn.run(app, host="0.0.0.0", port=8100, log_level="info")
