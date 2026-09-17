"""Render docs/hero.gif — an animated hero banner for the README.
Original artwork (no game assets): Cinzel title, breathing gold glow, drifting
embers and a pulsing blue summon sign (the co-op mechanic this app restores)."""
import math, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H = 1100, 380
FRAMES = 24
random.seed(7)

FONT = "src/DesCoop.App/Fonts/Cinzel.ttf"
TIMES = "C:/Windows/Fonts/times.ttf"

GOLD = (196, 164, 104)
GOLD_HI = (230, 205, 150)
TEXT = (230, 220, 198)
SOUL = (140, 190, 240)      # summon-sign blue
SOUL_DEEP = (70, 130, 210)
MUTED = (150, 140, 122)

def font(path, size):
    return ImageFont.truetype(path, size)

f_title = font(FONT, 74)
f_sub = font(FONT, 22)
f_tag = font(TIMES, 20)
f_small = font(FONT, 15)

# static background layer (vignette + faint stone speckle), built once
def build_bg():
    bg = Image.new("RGB", (W, H), (12, 11, 10))
    px = bg.load()
    cx, cy = W / 2, H / 2 - 20
    maxd = math.hypot(cx, cy)
    for y in range(H):
        for x in range(0, W, 1):
            d = math.hypot(x - cx, y - cy) / maxd
            v = 1 - 0.85 * (d ** 1.7)          # vignette falloff
            r = int(20 * v) + 8
            g = int(16 * v) + 7
            b = int(13 * v) + 6
            px[x, y] = (r, g, b)
    # faint warm speckle, like stone/parchment grain
    d = ImageDraw.Draw(bg)
    for _ in range(2600):
        x, y = random.randint(0, W - 1), random.randint(0, H - 1)
        a = random.randint(2, 10)
        d.point((x, y), fill=(20 + a, 17 + a, 13 + a // 2))
    return bg

BG = build_bg()

# embers: (x, base_y, speed, size, phase, drift)
EMBERS = []
for _ in range(46):
    EMBERS.append((
        random.uniform(0, W),
        random.uniform(0, H),
        random.uniform(6, 20),
        random.uniform(0.7, 2.3),
        random.uniform(0, math.tau),
        random.uniform(-0.5, 0.5),
    ))

def full_glow(cx, cy, rx, ry, color, blur):
    """Ellipse glow drawn on a full-canvas layer so the blur fades to zero
    inside the image (no visible rectangular paste seam after quantization)."""
    g = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(g).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=color)
    return g.filter(ImageFilter.GaussianBlur(blur))

def draw_center_text(base, draw, text, fnt, y, fill, glow=None):
    w = draw.textlength(text, font=fnt)
    x = (W - w) / 2
    if glow:
        layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((x, y), text, font=fnt, fill=glow)
        layer = layer.filter(ImageFilter.GaussianBlur(9))
        base.paste(layer, (0, 0), layer)
    draw.text((x, y), text, font=fnt, fill=fill)
    return x, w

frames = []
for i in range(FRAMES):
    t = i / FRAMES
    pulse = 0.5 + 0.5 * math.sin(t * math.tau)          # 0..1 breathing
    img = BG.copy()

    # breathing golden glow behind the title
    gl = full_glow(W / 2, 130, 300, 130, (150, 110, 45, int(60 + 45 * pulse)), 70)
    img.paste(gl, (0, 0), gl)

    # embers rising
    elayer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ed = ImageDraw.Draw(elayer)
    for (ex, ey, spd, sz, ph, dr) in EMBERS:
        yy = (ey - (t * spd * FRAMES)) % (H + 40) - 20
        xx = (ex + dr * (t * FRAMES)) % W
        fl = 0.5 + 0.5 * math.sin(t * math.tau * 2 + ph)
        a = int(60 + 120 * fl)
        c = (GOLD_HI[0], GOLD_HI[1], GOLD_HI[2], a)
        ed.ellipse([xx - sz, yy - sz, xx + sz, yy + sz], fill=c)
    elayer = elayer.filter(ImageFilter.GaussianBlur(0.6))
    img.paste(elayer, (0, 0), elayer)

    d = ImageDraw.Draw(img)

    # title
    draw_center_text(img, d, "DEMON'S SOULS", f_title, 74, TEXT, glow=(196, 150, 70, 200))
    d = ImageDraw.Draw(img)

    # rule + SEAMLESS CO-OP
    subw = d.textlength("SEAMLESS  CO-OP", font=f_sub)
    sx = (W - subw) / 2
    d.text((sx, 165), "SEAMLESS  CO-OP", font=f_sub, fill=GOLD)
    ry = 177
    d.line([(sx - 130, ry), (sx - 24, ry)], fill=(90, 74, 45), width=1)
    d.line([(sx + subw + 24, ry), (sx + subw + 130, ry)], fill=(90, 74, 45), width=1)

    # tagline
    tag = "Co-op across the whole game on RPCS3 — one hosts, the other joins."
    tw = d.textlength(tag, font=f_tag)
    d.text(((W - tw) / 2, 212), tag, font=f_tag, fill=MUTED)

    # --- pulsing blue summon sign on the "ground" ---
    scy = 305
    sign_pulse = 0.5 + 0.5 * math.sin(t * math.tau + 0.6)
    # outer soul glow
    gcol = (SOUL_DEEP[0], SOUL_DEEP[1], SOUL_DEEP[2], int(45 + 60 * sign_pulse))
    g = full_glow(W / 2, scy, 180, 75, gcol, 40)
    img.paste(g, (0, 0), g)
    # sign ring (perspective ellipse on the floor)
    ring = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ring)
    rw, rh = 150, 46
    a = int(150 + 90 * sign_pulse)
    rd.ellipse([W/2 - rw, scy - rh, W/2 + rw, scy + rh], outline=(SOUL[0], SOUL[1], SOUL[2], a), width=3)
    rd.ellipse([W/2 - rw*0.6, scy - rh*0.6, W/2 + rw*0.6, scy + rh*0.6], outline=(SOUL[0], SOUL[1], SOUL[2], int(a*0.6)), width=2)
    # rising soul wisp in the middle
    wisp_h = 34 + 10 * sign_pulse
    rd.line([(W/2, scy), (W/2, scy - wisp_h)], fill=(SOUL[0], SOUL[1], SOUL[2], int(a*0.7)), width=3)
    ring = ring.filter(ImageFilter.GaussianBlur(0.7))
    img.paste(ring, (0, 0), ring)

    # two phantom diamonds flanking the sign (host + helper), alternating shimmer
    d = ImageDraw.Draw(img)
    for k, dx in enumerate((-235, 235)):
        ph = 0.5 + 0.5 * math.sin(t * math.tau + k * math.pi)
        cxp, cyp = W / 2 + dx, scy
        s = 9
        dcol = (SOUL[0], SOUL[1], SOUL[2], int(120 + 120 * ph))
        dl = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(dl).polygon([(cxp, cyp - s), (cxp + s, cyp), (cxp, cyp + s), (cxp - s, cyp)], fill=dcol)
        dl = dl.filter(ImageFilter.GaussianBlur(1.2))
        img.paste(dl, (0, 0), dl)

    frames.append(img.convert("RGB"))

# stable palette to avoid inter-frame flicker on the gradients
pal = frames[len(frames)//2].quantize(colors=200, method=Image.MEDIANCUT)
qframes = [f.quantize(palette=pal, dither=Image.Dither.FLOYDSTEINBERG) for f in frames]
qframes[0].save("docs/hero.gif", save_all=True, append_images=qframes[1:],
                duration=83, loop=0, optimize=True, disposal=2)
print("saved docs/hero.gif")
