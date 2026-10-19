"""Builds Wingmate's icons from assets/icon-source/bee-ring-source.png (bee with glowing ring on a dark rounded tile).
Outputs: src/Wingmate/Assets/wingmate.ico (16-256, tiled: windows/taskbar/tray), wingmate-exe.ico (16-256, transparent
cut-out: the exe file icon only, from assets/icon-source/bee-ring-cutout.png), wingmate-icon.png (256, launcher logo), home-icon.png (home buttons),
assets/icon-build/icon-preview.png. Small sizes (<=32 and the home button) use a zoomed tile so the bee stays readable."""
from PIL import Image, ImageDraw, ImageFilter
import os, sys
R = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(R))
src = Image.open(os.path.join(ROOT, 'assets/icon-source/bee-ring-source.png')).convert('RGB')

# the tile in the 1280x720 source (measured: x 385..894, y 96..602, corner radius ~100); inset 2 px to drop the grey rim
TILE = (387, 97, 893, 603); RADIUS = 0.19  # radius as a fraction of the side
# zoomed crop around the bee + ring for small sizes
ZOOM = (424, 112, 876, 564)

def mask(side, radius_frac, ss=4):
    m = Image.new('L', (side * ss, side * ss), 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, side * ss - 1, side * ss - 1), radius=int(side * ss * radius_frac), fill=255)
    return m.resize((side, side), Image.LANCZOS)

def render(box, size, radius_frac=RADIUS, sharpen=False):
    im = src.crop(box).resize((size, size), Image.LANCZOS).convert('RGBA')
    if sharpen: im = im.filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=1))
    im.putalpha(mask(size, radius_frac))
    return im

def icon_image(size):
    return render(ZOOM, size, sharpen=True) if size <= 32 else render(TILE, size)

A = os.path.join(ROOT, 'src/Wingmate/Assets')
sizes = [16, 24, 32, 48, 64, 128, 256]
imgs = [icon_image(s) for s in sizes]
# write a proper multi-image ICO (PNG-compressed entries), each size rendered separately
import io, struct
def write_ico(path, images):
    entries = []
    for im in images:
        b = io.BytesIO(); im.save(b, 'PNG'); entries.append((im.width, b.getvalue()))
    hdr = struct.pack('<HHH', 0, 1, len(entries)); off = 6 + 16 * len(entries); dirs = b''; data = b''
    for s, png in entries:
        dirs += struct.pack('<BBBBHHII', s % 256, s % 256, 0, 0, 1, 32, len(png), off + len(data)); data += png
    open(path, 'wb').write(hdr + dirs + data)
write_ico(os.path.join(A, 'wingmate.ico'), imgs)  # tiled: windows, taskbar, tray

# ---- exe file icon (ApplicationIcon, what Explorer / the desktop show): the bee + ring cut out, transparent, no tile.
# bee-ring-cutout.png is made once by make_cutout.py (segmentation model + hole/glow handling), in tile coordinates.
cut = Image.open(os.path.join(ROOT, 'assets/icon-source/bee-ring-cutout.png')).convert('RGBA')
def to_tile(box): return (box[0] - TILE[0], box[1] - TILE[1], box[2] - TILE[0], box[3] - TILE[1])
def exe_image(size):
    box = to_tile(ZOOM) if size <= 32 else (0, 0, cut.width, cut.height)  # same zoomed crop for small sizes as the tiled icon
    im = cut.crop(box).resize((size, size), Image.LANCZOS)
    if size <= 32:
        im = im.filter(ImageFilter.UnsharpMask(radius=0.6, percent=60, threshold=1))
        # very soft dark halo so the gold reads on light backgrounds and the edge stays defined on dark ones
        a = im.getchannel('A').filter(ImageFilter.GaussianBlur(0.5)).point(lambda v: int(v * 0.3))
        halo = Image.new('RGBA', im.size, (10, 8, 4, 0)); halo.putalpha(a)
        im = Image.alpha_composite(halo, im)
    return im
exe_imgs = [exe_image(s) for s in sizes]
write_ico(os.path.join(A, 'wingmate-exe.ico'), exe_imgs)

# preview of the exe icon: 16/24/32/48/256 on dark, light and checkerboard, plus 4x enlargements of the small ones
def checker(w, h, c=8):
    im = Image.new('RGBA', (w, h), (255, 255, 255, 255)); dd = ImageDraw.Draw(im)
    for y in range(0, h, c):
        for x in range(0, w, c):
            if (x // c + y // c) % 2: dd.rectangle((x, y, x + c - 1, y + c - 1), fill=(204, 204, 204, 255))
    return im
cols = [('dark (Explorer dark mode)', Image.new('RGBA', (500, 470), (32, 32, 32, 255))),
        ('light (Explorer light mode)', Image.new('RGBA', (500, 470), (255, 255, 255, 255))),
        ('checkerboard (transparency)', checker(500, 470))]
EX = Image.new('RGBA', (1540, 520), (60, 62, 70, 255)); de = ImageDraw.Draw(EX)
de.text((14, 8), "Wingmate.exe file icon (transparent) - 16 / 24 / 32 / 48 px actual size, 16/24/32 enlarged 4x, and 256 px", fill=(235, 235, 235))
for ci, (label, bgim) in enumerate(cols):
    db = ImageDraw.Draw(bgim); x = 12
    for s in (16, 24, 32, 48):
        bgim.alpha_composite(exe_image(s), (x, 14)); x += s + 14
    x = 12
    for s in (16, 24, 32):
        bgim.alpha_composite(exe_image(s).resize((s * 4, s * 4), Image.NEAREST), (x, 80)); x += s * 4 + 10
    bgim.alpha_composite(exe_image(256), (230, 200))
    EX.alpha_composite(bgim, (14 + ci * 510, 40)); de.text((14 + ci * 510, 26), label, fill=(235, 235, 235))
EX.save(os.path.join(R, 'exe-icon-preview.png'))
render(TILE, 256).save(os.path.join(A, 'wingmate-icon.png'))
render(ZOOM, 64, sharpen=True).save(os.path.join(A, 'home-icon.png'))  # shown at 14 px (crisp up to 400% DPI)

# preview: icon at 16/24/32/48 on dark and light backgrounds (1x and 2x), plus the home button in an alerts/checklist header
def scale(im, k): return im.resize((im.width * k, im.height * k), Image.NEAREST)
P = Image.new('RGBA', (900, 430), (24, 26, 32, 255)); d = ImageDraw.Draw(P)
d.text((16, 10), "Wingmate icon - actual pixels (top) and enlarged 4x nearest-neighbour (below)", fill=(230, 230, 230))
x = 16
for bg in ((24, 26, 32), (243, 243, 243)):
    d.rectangle((x - 8, 30, x + 200, 410), fill=bg + (255,))
    xx = x
    for s in (16, 24, 32, 48):
        P.alpha_composite(icon_image(s), (xx, 40)); d.text((xx, 40 + s + 4), f"{s}", fill=(140, 140, 140)); xx += s + 10
    yy = 120; xx = x
    for s in (16, 24, 32):
        P.alpha_composite(scale(icon_image(s), 4), (xx, yy)); d.text((xx, yy + s * 4 + 2), f"{s} px x4", fill=(140, 140, 140))
        if s == 24: xx = x; yy += 24 * 4 + 20
        else: xx += s * 4 + 10
    x += 230
# header mock with home button (14 px image in an 18 px button) at 100% and 150%, in the Default theme colours
for k, (hx, hy) in ((1, (480, 60)), (2, (480, 150))):
    W, H = 200, 26
    hd = Image.new('RGBA', (W * k, H * k), (0, 0, 0, 0)); g = ImageDraw.Draw(hd)
    g.rounded_rectangle((0, 0, W * k - 1, H * k - 1), radius=8 * k, fill=(20, 26, 36, 235), outline=(160, 180, 210, 85))
    g.text((8 * k, 7 * k), "Abyss alerts - NA", fill=(255, 213, 138))
    home = Image.open(os.path.join(A, 'home-icon.png')).resize((14 * k, 14 * k), Image.LANCZOS)
    hd.alpha_composite(home, ((W - 46) * k, 6 * k))
    g.line(((W - 22) * k, 9 * k, (W - 14) * k, 17 * k), fill=(230, 238, 247, 170), width=max(1, int(1.4 * k)))
    g.line(((W - 14) * k, 9 * k, (W - 22) * k, 17 * k), fill=(230, 238, 247, 170), width=max(1, int(1.4 * k)))
    P.alpha_composite(hd, (hx, hy)); d.text((hx, hy - 14), f"home button in a header, {100 * k}% scale", fill=(200, 200, 200))
P.alpha_composite(render(TILE, 128), (520, 270)); d.text((520, 256), "48+ px: full tile (128 shown)", fill=(200, 200, 200))
P.alpha_composite(render(ZOOM, 128, sharpen=True), (700, 270)); d.text((700, 256), "<=32 px + home: zoomed", fill=(200, 200, 200))
P.save(os.path.join(R, 'icon-preview.png'))
print('ok')
