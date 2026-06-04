"""Generate app icons (ico/icns/png) for SUSSYMODMANAGER from the source logo.

Crops the rounded-square logo out of its black backdrop, makes it a transparent
square, and emits Windows .ico, macOS .icns, and Linux PNGs into the app Assets.
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "sus-logo-source.png")
OUT = os.path.abspath(os.path.join(HERE, "..", "src", "SussyModManager", "Assets"))
os.makedirs(OUT, exist_ok=True)


def bbox_of_content(img, threshold=40):
    """Bounding box of pixels brighter than threshold (drops near-black backdrop)."""
    gray = img.convert("L")
    mask = gray.point(lambda p: 255 if p > threshold else 0)
    return mask.getbbox()


def main():
    img = Image.open(SRC).convert("RGBA")

    box = bbox_of_content(img)
    if box:
        img = img.crop(box)

    # Make black-ish backdrop transparent so corners aren't hard black.
    px = img.load()
    w, h = img.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if r + g + b < 36:
                px[x, y] = (r, g, b, 0)

    # Center on a transparent square canvas.
    side = max(img.size)
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.paste(img, ((side - img.width) // 2, (side - img.height) // 2), img)

    master = canvas.resize((1024, 1024), Image.LANCZOS)

    # Windows .ico (multi-resolution)
    ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    master.save(os.path.join(OUT, "sus.ico"), sizes=ico_sizes)

    # Linux / generic PNGs
    master.resize((512, 512), Image.LANCZOS).save(os.path.join(OUT, "sus.png"))
    master.resize((256, 256), Image.LANCZOS).save(os.path.join(OUT, "sus-256.png"))

    # macOS .icns (Pillow needs a square RGBA image)
    try:
        master.save(os.path.join(OUT, "sus.icns"))
    except Exception as e:  # noqa: BLE001
        print("icns save failed:", e)

    print("Wrote icons to", OUT)
    for f in sorted(os.listdir(OUT)):
        print("  ", f, os.path.getsize(os.path.join(OUT, f)), "bytes")


if __name__ == "__main__":
    main()
