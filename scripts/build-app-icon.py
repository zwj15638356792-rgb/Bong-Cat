"""Build a Windows multi-resolution icon from the supplied character artwork."""

import argparse
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "artwork/source-poses/char_S_idle_01.png"
SIZES = (16, 24, 32, 48, 64, 128, 256)


def build_icon(destination: Path):
    source = Image.open(SOURCE).convert("RGBA")
    # The source is a full-body 1254px sprite. Focus on the head, bonnet and
    # front hair so the face remains recognizable at taskbar/tray sizes.
    width, height = source.size
    head = source.crop((round(width * .19), round(height * .025),
                        round(width * .81), round(height * .615)))
    canvas = Image.new("RGBA", (1024, 1024))
    draw = ImageDraw.Draw(canvas)
    draw.ellipse((18, 18, 1006, 1006), fill=(186, 220, 239, 255),
                 outline=(81, 127, 171, 255), width=16)
    # A subtle inset light keeps the edge visible on both light and dark bars.
    draw.ellipse((44, 44, 980, 980), outline=(231, 246, 252, 255), width=10)
    head.thumbnail((1000, 1000), Image.Resampling.LANCZOS)
    x = (canvas.width - head.width) // 2
    y = (canvas.height - head.height) // 2
    shadow = Image.new("RGBA", canvas.size)
    shadow.paste((34, 57, 105, 90), (x + 6, y + 12), head.getchannel("A"))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8)))
    canvas.alpha_composite(head, (x, y))
    # Keep the icon silhouette round even where the side hair reaches the edge.
    clip = Image.new("L", canvas.size)
    ImageDraw.Draw(clip).ellipse((12, 12, 1012, 1012), fill=255)
    canvas.putalpha(ImageChops.multiply(canvas.getchannel("A"), clip))
    destination.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(destination, format="ICO", sizes=[(size, size) for size in SIZES])
    print(f"Built {destination} ({', '.join(map(str, SIZES))} px)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=ROOT / "output/Dafeiyu-Jointed/app.ico")
    args = parser.parse_args()
    build_icon(args.output.resolve())


if __name__ == "__main__":
    main()
