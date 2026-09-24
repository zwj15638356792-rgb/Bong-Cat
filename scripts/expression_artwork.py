"""Align the supplied portrait expressions with the original character's face.

Only the eye and mouth artwork changes. The idle image keeps its head silhouette,
hair, clothes and position, and remains the default expression at runtime.
"""

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

EXPRESSIONS = ("blink", "happy", "surprised")


def _feature_mask(character: Image.Image) -> Image.Image:
    """A feathered mask for the eye sockets and mouth in the 1254px source."""
    mask = Image.new("L", character.size)
    draw = ImageDraw.Draw(mask)
    draw.polygon([
        (437, 495), (458, 475), (484, 453), (513, 450), (528, 468),
        (545, 483), (560, 506), (558, 548), (543, 569), (492, 573),
        (467, 563), (449, 544), (435, 520),
    ], fill=255)
    draw.polygon([
        (637, 494), (646, 468), (668, 448), (701, 446), (720, 453),
        (738, 476), (753, 494), (763, 514), (746, 550), (721, 567),
        (674, 570), (646, 552), (636, 520),
    ], fill=255)
    draw.ellipse((555, 550, 646, 622), fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(2.0))
    rgb = np.asarray(character)[:, :, :3].astype(int)
    # Blue irises are separate components, while the forehead and side locks
    # form one large connected hair region. Preserve that exact region.
    labels, _ = ndimage.label(rgb[:, :, 2] - rgb[:, :, 0] > 12)
    hair = labels == labels[300, 600]
    hair = ndimage.binary_dilation(hair, iterations=1)
    alpha = np.array(mask)
    alpha[hair] = 0
    return Image.fromarray(alpha)


def build_expression_layers(character_source, output_dir, scale=4):
    """Write full-canvas face overlays at the same scale as the body texture.

    Source files stay at their original 1254px resolution. The final character
    image is sampled to 301*scale exactly once, matching build-jointed-assets.py.
    Returns the expression names for callers that need to record a manifest.
    """
    character_source = Path(character_source)
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    character = Image.open(character_source).convert("RGBA")
    if character.size != (1254, 1254):
        raise ValueError("Expression landmarks require the original 1254x1254 character")
    if not isinstance(scale, int) or scale < 1:
        raise ValueError("Expression scale must be a positive integer")
    mask = _feature_mask(character)
    alpha = np.asarray(mask)
    for name in EXPRESSIONS:
        path = character_source.parent / f"portrait_{name}.png"
        source = Image.open(path).convert("RGBA")
        if source.size != character.size:
            raise ValueError(f"Expression source must be 1254x1254: {path}")
        # Facial landmarks fitted to the idle character. Portraits are closer
        # views, so moving the entire head would change the established shape.
        sx, sy, dx, dy = 0.724, 0.751, 156, 7
        aligned = source.transform(
            character.size, Image.Transform.AFFINE,
            (1 / sx, 0, -dx / sx, 0, 1 / sy, -dy / sy),
            Image.Resampling.BICUBIC,
        )
        rgba = np.array(aligned)
        rgb = rgba[:, :, :3].astype(int)
        source_hair = rgb[:, :, 2] - rgb[:, :, 0] > 12
        source_hair[465:573, 453:560] = False
        source_hair[455:565, 637:748] = False
        overhang = source_hair & (alpha > 0)
        # Portrait bangs differ by a few pixels. Remove only any tiny overhang
        # inside the facial patch; the original hair remains completely intact.
        if overhang.any():
            skin = ((rgb[:, :, 0] - rgb[:, :, 2] > 12)
                    & (rgb[:, :, 0] > 160) & (rgb[:, :, 1] > 110))
            nearest = ndimage.distance_transform_edt(
                ~skin, return_distances=False, return_indices=True
            )
            rgba[overhang, :3] = rgba[
                nearest[0][overhang], nearest[1][overhang], :3
            ]
        patch = Image.fromarray(rgba)
        patch.putalpha(mask)
        patch = patch.resize((301 * scale, 301 * scale), Image.Resampling.LANCZOS)
        canvas = Image.new("RGBA", (612 * scale, 354 * scale))
        canvas.alpha_composite(patch, (140 * scale, -4 * scale))
        canvas.save(output_dir / f"face-{name}.png")
    return EXPRESSIONS
