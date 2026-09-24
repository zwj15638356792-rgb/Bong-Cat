"""Extract the character's hand and rasterize the original BongoCat mouse."""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage


def extract_arm(source_path: Path) -> Image.Image:
    """Return the original cuff and back of the hand, aligned vertically."""
    source = Image.open(source_path).convert("RGBA").resize(
        (627, 627), Image.Resampling.LANCZOS
    )
    box = (186, 378, 249, 447)
    arm = source.crop(box)
    mask = Image.new("L", arm.size)
    points = [
        (223, 378), (243, 381), (248, 383), (240, 393), (233, 402),
        (230, 412), (223, 422), (219, 430), (214, 431), (209, 439),
        (205, 444), (197, 446), (191, 441), (186, 433), (188, 428),
        (196, 421), (190, 420), (186, 415), (186, 412), (197, 407),
        (199, 399), (207, 390),
    ]
    ImageDraw.Draw(mask).polygon(
        [(x - box[0], y - box[1]) for x, y in points], fill=255
    )
    arm.putalpha(mask.filter(ImageFilter.GaussianBlur(0.65)))
    arm = arm.rotate(28, resample=Image.Resampling.BICUBIC, expand=True)
    arm = arm.crop(arm.getbbox()).resize((78, 115), Image.Resampling.LANCZOS)

    # Center the scanlines so the cuff remains aligned with the wrist joint.
    pixels = np.array(arm)
    centered = np.zeros_like(pixels)
    for y, row in enumerate(pixels):
        occupied = np.flatnonzero(row[:, 3] > 40)
        if len(occupied):
            shift = round(38.5 - (occupied[0] + occupied[-1]) / 2)
            centered[y] = np.roll(row, shift, axis=0)
    centered[:18] = centered[18]
    return Image.fromarray(centered)


def render_mouse(geometry: dict, stock: Path, click: str = "") -> Image.Image:
    """Rasterize only the mouse and optional click highlight meshes.

    Canvas dimensions and texture sampling match the original asset pipeline.
    No temporary model copies or mutable module globals are needed.
    """
    width, height, scale = 1224, 708, 2
    frame = Image.new("RGBA", (width, height))
    textures = {}
    meshes = [mesh for mesh in geometry["meshes"] if mesh["id"] in ("mouse", click)]
    for mesh in sorted(meshes, key=lambda item: item["order"]):
        texture_index = mesh["texture"]
        if texture_index not in textures:
            path = stock / f"demomodel.1024/texture_{texture_index:02d}.png"
            textures[texture_index] = np.asarray(
                Image.open(path).convert("RGBA"), dtype=float
            )
        texture = textures[texture_index]
        positions = np.array(mesh["positions"]).reshape(-1, 2)
        positions = np.column_stack([
            306 + positions[:, 0] * 354, 177 - positions[:, 1] * 354
        ]) * scale
        uvs = np.array(mesh["uvs"]).reshape(-1, 2)
        uvs = uvs * [texture.shape[1], -texture.shape[0]] + [0, texture.shape[0]]
        result = np.zeros((height, width, 4), dtype=np.uint8)
        for triangle in np.array(mesh["indices"]).reshape(-1, 3):
            target = positions[triangle]
            xmin, ymin = np.maximum(np.floor(target.min(axis=0)).astype(int), 0)
            xmax, ymax = np.minimum(
                np.ceil(target.max(axis=0)).astype(int), [width - 1, height - 1]
            )
            if xmin > xmax or ymin > ymax:
                continue
            xx, yy = np.meshgrid(np.arange(xmin, xmax + 1), np.arange(ymin, ymax + 1))
            try:
                barycentric = np.linalg.solve(
                    np.vstack([target.T, np.ones(3)]),
                    np.stack([xx.ravel() + 0.5, yy.ravel() + 0.5, np.ones(xx.size)]),
                )
            except np.linalg.LinAlgError:
                continue
            valid = ((barycentric.min(axis=0) >= -0.002)
                     & (barycentric.max(axis=0) <= 1.002))
            coords = uvs[triangle].T @ barycentric
            pixels = np.column_stack([
                ndimage.map_coordinates(
                    texture[:, :, channel], [coords[1] - 0.5, coords[0] - 0.5],
                    order=1, mode="constant",
                )
                for channel in range(4)
            ])
            region = result[yy.ravel(), xx.ravel()]
            region[valid] = np.clip(pixels[valid], 0, 255).astype(np.uint8)
            result[yy.ravel(), xx.ravel()] = region
        frame.alpha_composite(Image.fromarray(result))
    return frame.crop((188, 386, 376, 558)).resize((138, 126), Image.Resampling.LANCZOS)
