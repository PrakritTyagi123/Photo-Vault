"""Download all AI models for PhotoVault."""
import os
import sys

def main():
    print("=" * 50)
    print("PhotoVault AI Model Downloader")
    print("=" * 50)

    print("\n[1/7] Downloading BLIP (captioning)...")
    try:
        from transformers import BlipProcessor, BlipForConditionalGeneration
        BlipProcessor.from_pretrained("Salesforce/blip-image-captioning-base")
        BlipForConditionalGeneration.from_pretrained("Salesforce/blip-image-captioning-base")
        print("  ✓ BLIP ready")
    except Exception as e:
        print(f"  ✗ BLIP failed: {e}")

    # YOLOv8 (tagging/detection)
    print("\n[2/7] Downloading YOLOv8 (object detection)...")
    try:
        from ultralytics import YOLO
        YOLO("yolov8n.pt")
        print("  ✓ YOLOv8 ready")
    except Exception as e:
        print(f"  ✗ YOLOv8 failed: {e}")

    print("\n[3/7] Downloading MediaPipe (face detection)...")
    try:
        import mediapipe as mp
        detector = mp.solutions.face_detection.FaceDetection(model_selection=1)
        print("  ✓ MediaPipe ready")
    except Exception as e:
        print(f"  ✗ MediaPipe failed: {e}")

    # CLIP (embeddings)
    print("\n[4/7] Downloading CLIP (semantic search)...")
    try:
        import open_clip
        open_clip.create_model_and_transforms("ViT-B-32", pretrained="openai")
        print("  ✓ CLIP ready")
    except Exception as e:
        print(f"  ✗ CLIP failed: {e}")

    # PaddleOCR
    print("\n[5/7] Downloading PaddleOCR...")
    try:
        from paddleocr import PaddleOCR
        ocr = PaddleOCR(lang="en")
        print("  ✓ PaddleOCR ready")
    except Exception as e:
        print(f"  ✗ PaddleOCR failed: {e}")

    # DepthAnything
    print("\n[6/7] Downloading Depth Anything...")
    try:
        from transformers import pipeline
        pipeline("depth-estimation", model="LiheYoung/depth-anything-small-hf")
        print("  ✓ Depth Anything ready")
    except Exception as e:
        print(f"  ✗ Depth Anything failed: {e}")

    # NSFW classifier
    print("\n[7/7] Downloading NSFW classifier...")
    try:
        from transformers import pipeline
        pipeline("image-classification", model="Falconsai/nsfw_image_detection")
        print("  ✓ NSFW classifier ready")
    except Exception as e:
        print(f"  ✗ NSFW classifier failed: {e}")

    print("\n" + "=" * 50)
    print("Model download complete!")
    print("=" * 50)


if __name__ == "__main__":
    main()
