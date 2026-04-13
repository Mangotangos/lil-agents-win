"""Generate docs/preview.gif — animated robots walking on a taskbar strip."""
import os, math
from PIL import Image, ImageDraw

os.makedirs("docs", exist_ok=True)

W, H = 520, 120
TASKBAR_H = 48
CHAR_W, CHAR_H = 44, 56
FPS = 12
TOTAL_FRAMES = 48

# Colors
BG           = (18,  20,  32)     # dark desktop
TASKBAR_BG   = (30,  32,  46)
TASKBAR_TOP  = (60,  65,  100)
BODY_BLUE    = (107, 158, 255)
BODY_MID     = (90,  143, 255)
BODY_DARK    = (74,  126, 217)
EYE_WHITE    = (240, 240, 255)
EYE_PUPIL    = (20,  20,  50)
ANTENNA_ORB  = (255, 154, 112)
CHAT_BG      = (20,  27,  50)
CHAT_BORDER  = (42,  58,  106)
CHAT_TEXT    = (200, 216, 255)
ACCENT       = (90,  143, 255)
WHITE        = (255, 255, 255)


def draw_robot(d: ImageDraw.ImageDraw, x: int, y: int,
               walk_frame: int, flip: bool) -> None:
    """Draw a simple robot at pixel (x, y) = bottom-left corner."""

    def cx(ox): return x + (CHAR_W - 1 - ox) if flip else x + ox
    def rx(ox1, oy1, ox2, oy2, **kw):
        x1, x2 = sorted([cx(ox1), cx(ox2)])
        d.rounded_rectangle([x1, oy1, x2, oy2], **kw)

    # ── antenna ──────────────────────────────────────────
    ax = cx(21)
    d.line([(ax, y - CHAR_H), (ax, y - CHAR_H - 8)], fill=BODY_DARK, width=2)
    d.ellipse([ax-4, y-CHAR_H-14, ax+4, y-CHAR_H-6], fill=ANTENNA_ORB)

    # ── head ─────────────────────────────────────────────
    hy1 = y - CHAR_H
    rx(6, hy1, 37, hy1+26, radius=8, fill=BODY_BLUE)

    # eyes
    for ex in (cx(12), cx(25)):
        d.ellipse([ex, hy1+6, ex+7, hy1+13], fill=EYE_WHITE)
        d.ellipse([ex+2, hy1+7, ex+5, hy1+12], fill=EYE_PUPIL)

    # mouth dots
    smile_y = hy1 + 18
    for i in range(3):
        mx = cx(14 + i*4)
        d.ellipse([mx, smile_y, mx+3, smile_y+2], fill=WHITE)

    # ── torso ────────────────────────────────────────────
    ty1 = y - CHAR_H + 28
    ty2 = y - CHAR_H + 48
    rx(9, ty1, 35, ty2, radius=5, fill=BODY_MID)
    cdx = cx(19)
    d.ellipse([cdx, ty1+7, cdx+6, ty1+13], fill=ANTENNA_ORB)

    # ── arms ─────────────────────────────────────────────
    arm_swing = int(math.sin(walk_frame / FPS * math.pi * 2) * 5)
    rx(1, ty1+2, 8,  ty1+18+arm_swing, radius=3, fill=BODY_BLUE)
    rx(36, ty1+2, 43, ty1+18-arm_swing, radius=3, fill=BODY_BLUE)

    # ── legs ─────────────────────────────────────────────
    leg_bob = int(math.sin(walk_frame / FPS * math.pi * 2) * 3)
    rx(10, ty2, 20, ty2+13-leg_bob, radius=3, fill=BODY_DARK)
    rx(23, ty2, 33, ty2+13+leg_bob, radius=3, fill=BODY_DARK)


def draw_chat_bubble(d, bx, by):
    """Draw a small chat popover above position bx,by."""
    bw, bh = 140, 52
    bx = max(4, min(bx - bw//2, W - bw - 4))
    by = by - bh - 8
    if by < 4: return
    # shadow
    d.rounded_rectangle([bx+2, by+2, bx+bw+2, by+bh+2],
                         radius=8, fill=(0,0,0,80))
    # panel
    d.rounded_rectangle([bx, by, bx+bw, by+bh], radius=8, fill=CHAT_BG)
    d.rounded_rectangle([bx, by, bx+bw, by+bh], radius=8,
                         outline=CHAT_BORDER, width=1)
    # title bar
    d.rounded_rectangle([bx, by, bx+bw, by+14], radius=8, fill=(28,36,64))
    d.text((bx+8, by+2), "● Claude", fill=ACCENT,
           font=None)
    # fake text lines
    for i, lw in enumerate([80, 110, 60]):
        ly = by + 20 + i * 11
        d.rounded_rectangle([bx+6, ly, bx+6+lw, ly+5],
                             radius=2, fill=(*CHAT_TEXT[:3], 80))


frames = []

# Character start positions and speeds
chars = [
    {"x": 60.0,  "speed": 1.6, "dir": 1,  "show_chat": False, "chat_frame": 0},
    {"x": 300.0, "speed": 1.3, "dir": -1, "show_chat": True,  "chat_frame": 0},
]

for f in range(TOTAL_FRAMES):
    img = Image.new("RGBA", (W, H), BG + (255,))
    d   = ImageDraw.Draw(img, "RGBA")

    # ── taskbar strip ────────────────────────────────────
    ty = H - TASKBAR_H
    d.rectangle([0, ty, W, H], fill=TASKBAR_BG)
    d.line([(0, ty), (W, ty)], fill=TASKBAR_TOP, width=1)

    # fake taskbar icons
    for ix in range(8, W - 8, 48):
        d.rounded_rectangle([ix, ty+8, ix+32, ty+38],
                             radius=6, fill=(40, 44, 64))

    # system clock area
    d.rounded_rectangle([W-76, ty+10, W-6, ty+36],
                         radius=4, fill=(35, 38, 58))

    # ── update + draw characters ──────────────────────────
    for c in chars:
        c["x"] += c["speed"] * c["dir"]
        if c["x"] > W - CHAR_W - 10:  c["dir"] = -1
        if c["x"] < 10:               c["dir"] =  1

        cx_px   = int(c["x"])
        char_y  = ty  # bottom of char = top of taskbar
        flip    = c["dir"] == -1

        # chat bubble for second character during middle frames
        if c["show_chat"] and 12 <= f <= 40:
            draw_chat_bubble(d, cx_px + CHAR_W//2, char_y)

        draw_robot(d, cx_px, char_y, f, flip)

    # convert to palette for GIF
    img_p = img.convert("P", palette=Image.ADAPTIVE, colors=128)
    frames.append(img_p)

frames[0].save(
    "docs/preview.gif",
    save_all=True,
    append_images=frames[1:],
    loop=0,
    duration=int(1000 / FPS),
    optimize=True,
)
print("docs/preview.gif written")
