"""Remove the white + light-blue background from the BS3D logo (flood fill from the edges + soft edge alpha)."""
import sys, numpy as np
from PIL import Image
from scipy import ndimage as nd
src, out = sys.argv[1], sys.argv[2]
a = np.asarray(Image.open(src).convert('RGB')).astype(np.float32)
h, w, _ = a.shape
WHITE = np.array([255, 255, 255], np.float32); BLUE = np.array([192, 222, 246], np.float32)
dW = np.linalg.norm(a - WHITE, axis=2); dB = np.linalg.norm(a - BLUE, axis=2)
# distance to the white-blue colour segment (covers the anti-aliased rim of the blue circle too)
seg = BLUE - WHITE
t = np.clip(((a - WHITE) @ seg) / (seg @ seg), 0, 1)
bgcol = WHITE + t[..., None] * seg
d = np.linalg.norm(a - bgcol, axis=2)
cand = d < 28                                   # background-coloured pixels
lab, n = nd.label(cand)
border = set(np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))) - {0}
bg = np.isin(lab, list(border))
# enclosed pockets: blue ones anywhere (gaps showing the circle), white ones only in the lettering band
sizes = nd.sum(np.ones_like(lab), lab, index=np.arange(n + 1))
for i in range(1, n + 1):
    if i in border or sizes[i] < 40: continue
    m = lab == i
    ys = np.where(m)[0]
    if dB[m].mean() < 18 or (ys.min() > h * 0.74 and dW[m].mean() < 18): bg |= m
# soft alpha in a 2 px band around the background, with the bg colour un-mixed (no halo)
band = nd.binary_dilation(bg, iterations=2) & ~bg
alpha = np.where(bg, 0.0, 1.0)
soft = np.clip((d - 6) / 70.0, 0, 1)
alpha[band] = soft[band]
alpha = nd.gaussian_filter(alpha, 0.5) * (~bg) + 0  # tiny smoothing, keep bg fully clear
alpha = np.clip(np.where(bg, 0, alpha), 0, 1)
# drop stray specks / the thin rim line of the blue circle: keep only big solid parts (+3 px of soft edge)
core = alpha > 0.98
cl, cn = nd.label(core)
cs = nd.sum(core, cl, index=np.arange(cn + 1))
keep = np.isin(cl, np.where(cs >= 400)[0][np.where(cs >= 400)[0] > 0])
alpha = alpha * nd.binary_dilation(keep, iterations=3)
A = np.maximum(alpha, 1e-3)[..., None]
rgb = np.where(band[..., None], (a - (1 - A) * bgcol) / A, a)
rgb = np.clip(rgb, 0, 255)
img = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), 'RGBA')
bb = img.getbbox(); img = img.crop(bb)
s = max(img.size); sq = Image.new('RGBA', (s, s), (0, 0, 0, 0)); sq.paste(img, ((s - img.width) // 2, (s - img.height) // 2))
sq.save(out); print('saved', sq.size, 'bbox', bb)
