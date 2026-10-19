"""One-off: cuts the bee + ring out of the dark tile -> assets/icon-source/bee-ring-cutout.png (RGBA, 506x506, tile coordinates).
Needs rembg (pip install "rembg[cpu]" scipy). Not a colour key: a segmentation model (isnet-general-use) gives the soft mask,
textured enclosed holes are filled (the dark glossy eye stays opaque; flat tile gaps between legs / inside the ring
stay see-through), and the ring's orange glow
is kept as a soft, un-premultiplied halo measured against the local tile colour."""
import os
import numpy as np
from PIL import Image
from rembg import remove, new_session
from scipy import ndimage

R = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(R))
TILE = (387, 97, 893, 603)
src = Image.open(os.path.join(ROOT, 'assets/icon-source/bee-ring-source.png')).convert('RGB')
tile = src.crop(TILE)
rgb = np.asarray(tile).astype(np.float64)

seg = np.asarray(remove(tile, session=new_session('isnet-general-use'), only_mask=True, post_process_mask=False)).astype(np.float64) / 255
# Holes inside the model's mask: the eye (dark but glossy, high contrast) is part of the bee and gets filled; gaps between
# the legs and the space inside the ring are plain tile (flat, low contrast) and stay transparent. Tiny specks are filled.
lum = rgb.mean(axis=2)
base = seg > 0.5
holes, n = ndimage.label(ndimage.binary_fill_holes(base) & ~base)
solid = base.copy()
for i in range(1, n + 1):
    r = holes == i
    if r.sum() < 30 or lum[r].std() > 15: solid |= r
solid_soft = ndimage.gaussian_filter(solid.astype(np.float64), 0.7)
alpha = np.maximum(seg, np.where(solid, 1.0, solid_soft * (seg > 0.05)))

# local tile colour: median-blur of the tile with the subject masked out (the tile has a soft vignette)
bgmask = ~ndimage.binary_dilation(solid, iterations=12)
bg = np.zeros_like(rgb)
for c in range(3):
    ch = np.where(bgmask, rgb[..., c], np.nan)
    filled = ch.copy(); idx = ndimage.distance_transform_edt(np.isnan(ch), return_distances=False, return_indices=True)
    filled = ch[tuple(idx)]
    bg[..., c] = ndimage.gaussian_filter(filled, 6)

# glow: brighter-than-tile, warm pixels near the subject become a soft halo
near = ndimage.binary_dilation(solid, iterations=28) & ~solid
excess = np.clip((rgb - bg).max(axis=2), 0, None)
warm = rgb[..., 0] > rgb[..., 2] + 12
glow = ndimage.gaussian_filter(np.clip((excess - 10) / 70.0, 0, 1) * near * warm, 1.5)  # smoothed: no speckle
alpha = np.maximum(alpha, glow)

# un-premultiply against the tile colour so edges/glow don't carry the dark tile into light backgrounds
a3 = np.clip(alpha, 1e-3, 1)[..., None]
col = np.where(alpha[..., None] < 0.999, (rgb - (1 - a3) * bg) / a3, rgb)
col = np.clip(col, 0, 255)
out = np.dstack([col, alpha * 255]).round().astype(np.uint8)
Image.fromarray(out, 'RGBA').save(os.path.join(ROOT, 'assets/icon-source/bee-ring-cutout.png'))
print('ok')
