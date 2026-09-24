"""Build the jointed desktop pet's assets from the checked-in source artwork."""

import argparse
import json
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

from arm_artwork import extract_arm, render_mouse
from expression_artwork import build_expression_layers

ROOT = Path(__file__).resolve().parents[1]
STOCK = ROOT / "src-tauri/assets/models/standard"
CHARACTER = ROOT / "artwork/source-poses/char_S_idle_01.png"
ART_SCALE = 4


def build_assets(output: Path, geometry: dict):
    output.mkdir(parents=True, exist_ok=True)
    # Replace lowered hands with nearby hair; the renderer draws the new arms.
    scale = ART_SCALE
    scaled = lambda box: tuple(round(value * scale) for value in box)
    character = Image.open(CHARACTER).convert("RGBA").resize(
        (301 * scale, 301 * scale), Image.Resampling.LANCZOS
    )
    for box, donor in [
        ((88, 189, 116, 222), (63, 189, 91, 222)),
        ((176, 189, 204, 222), (205, 189, 233, 222)),
    ]:
        patch = character.crop(scaled(donor))
        mask = Image.new("L", patch.size)
        ImageDraw.Draw(mask).ellipse((0, -4 * scale, patch.width, patch.height + 4 * scale), fill=255)
        character.paste(patch, scaled(box[:2]), mask.filter(ImageFilter.GaussianBlur(1.5 * scale)))
    pixels = np.array(character)
    yy, xx = np.indices(pixels.shape[:2]) / scale
    pixels[pixels[:, :, 3] > 245, 3] = 255
    pixels[(xx > 228) & (yy > 190), 3] = 0
    pixels[(xx > 234) & (yy > 170), 3] = 0
    pixels[(xx > 220) & (yy > 210), 3] = 0
    pixels[:, :, 3] = (pixels[:, :, 3] * np.clip((224 - yy) / 12, 0, 1)).astype(np.uint8)
    body = Image.new("RGBA", (612 * scale, 354 * scale))
    body.alpha_composite(Image.fromarray(pixels), scaled((140, -4)))
    body.save(output / "body.png")
    build_expression_layers(CHARACTER, output, scale)

    # Front locks cover the shoulder seam; rear hair stays behind the sleeves.
    locks = Image.new("RGBA", body.size)
    for box in [(235, 147, 249, 171), (322, 145, 343, 171)]:
        locks.alpha_composite(body.crop(scaled(box)), scaled(box[:2]))
    locks.save(output / "front-hair.png")
    extract_arm(CHARACTER).crop((0, 56, 78, 115)).save(output / "cuff-hand.png")
    original = Image.open(CHARACTER).convert("RGBA").resize(
        (627, 627), Image.Resampling.LANCZOS
    )
    fabric = original.crop((228, 352, 241, 377))
    fabric.putalpha(255)
    fabric.save(output / "fabric.png")

    background = Image.open(STOCK / "resources/background.png").convert("RGBA")
    pad_pixels = np.array(background)
    py, px = np.indices(pad_pixels.shape[:2])
    pad_pixels[py < 160 + px * 0.18, 3] = 0
    eligible = ((pad_pixels[:, :, :3].min(axis=2) > 245)
                | (pad_pixels[:, :, 3] == 0))
    seeds = np.zeros(eligible.shape, dtype=bool)
    seeds[[0, -1], :] = eligible[[0, -1], :]
    seeds[:, [0, -1]] |= eligible[:, [0, -1]]
    pad_pixels[ndimage.binary_propagation(seeds, mask=eligible), 3] = 0
    pad = Image.fromarray(pad_pixels).crop((0, 170, 275, 314))
    mask = Image.new("L", pad.size)
    ImageDraw.Draw(mask).polygon([
        (0, 6), (79, 4), (271, 42), (273, 53), (183, 137), (165, 142), (0, 80),
    ], fill=255)
    pad.putalpha(Image.fromarray(np.minimum(np.array(mask), np.array(pad)[:, :, 3])))
    # Tint the original pad without changing its silhouette or edge shading.
    pad_tint = np.array(pad)
    light = pad_tint[:, :, :3].mean(axis=2) / 255.0
    low = np.array([50, 80, 108])
    high = np.array([211, 237, 244])
    pad_tint[:, :, :3] = np.clip(low + light[:, :, None] * (high - low), 0, 255)
    pad = Image.fromarray(pad_tint)
    pad.resize((348, 183), Image.Resampling.LANCZOS).save(output / "pad.png")

    for name, click in [("mouse", ""), ("mouse-left", "mouse_left"),
                        ("mouse-right", "mouse_right")]:
        render_mouse(geometry, STOCK, click).save(output / f"{name}.png")

    print(f"Prepared high-resolution character and mouse artwork in {output}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--output", type=Path, default=ROOT / "output/Dafeiyu-Jointed/assets",
        help="Destination asset directory (default: output/Dafeiyu-Jointed/assets)",
    )
    parser.add_argument(
        "--geometry", type=Path,
        help="Reuse explicitly supplied geometry; otherwise regenerate it with Node.js",
    )
    args = parser.parse_args()
    geometry_path = args.geometry
    if geometry_path is None:
        subprocess.run(["node", str(ROOT / "scripts/model-geometry.cjs")], cwd=ROOT, check=True)
        geometry_path = ROOT / "tmp/model-geometry.json"
    geometry = json.loads(geometry_path.read_text(encoding="utf-8"))
    build_assets(args.output.resolve(), geometry)


if __name__ == "__main__":
    main()
