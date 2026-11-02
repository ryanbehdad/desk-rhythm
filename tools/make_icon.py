"""Generate src/DeskRhythm/Assets/DeskRhythm.ico.

The icon is committed to the repo, so you only need this script if you want to
change the design. Requires Pillow:  python -m pip install pillow
"""
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "src" / "DeskRhythm" / "Assets" / "DeskRhythm.ico"
SS = 1024  # supersampled canvas; scaled down for each icon size


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def draw_master() -> Image.Image:
    top, bottom = (61, 111, 165), (46, 139, 120)  # muted blue -> teal
    gradient = Image.new("RGBA", (SS, SS))
    gd = ImageDraw.Draw(gradient)
    for y in range(SS):
        gd.line([(0, y), (SS, y)], fill=lerp(top, bottom, y / (SS - 1)) + (255,))

    mask = Image.new("L", (SS, SS), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, SS - 1, SS - 1], radius=int(SS * 0.22), fill=255)
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    img.paste(gradient, (0, 0), mask)

    # A standing figure on a 24-unit grid, drawn with round strokes.
    d = ImageDraw.Draw(img)
    u = SS / 24
    w = int(2.3 * u)
    white = (255, 255, 255, 255)

    def line(points):
        pts = [(x * u, y * u) for x, y in points]
        d.line(pts, fill=white, width=w, joint="curve")
        for x, y in pts:
            d.ellipse([x - w / 2, y - w / 2, x + w / 2, y + w / 2], fill=white)

    hx, hy, hr = 12 * u, 4.9 * u, 2.1 * u
    d.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=white)
    line([(12, 9.0), (12, 14.6)])
    line([(8.4, 13.2), (12, 10.2), (15.6, 13.2)])
    line([(9.4, 20.2), (12, 14.6), (14.6, 20.2)])

    # Small amber "rhythm" dot.
    ax, ay, ar = 18.6 * u, 6.0 * u, 1.5 * u
    d.ellipse([ax - ar, ay - ar, ax + ar, ay + ar], fill=(231, 169, 75, 255))
    return img


def main() -> None:
    master = draw_master()
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    master.resize((256, 256), Image.LANCZOS).save(OUT, sizes=[(s, s) for s in sizes])
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
