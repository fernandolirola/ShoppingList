"""Draws the hideout toolbar cart icon (white glyph on transparent, like the game's own icons)."""
import sys
from PIL import Image, ImageDraw

S = 4  # supersample
size = 128
img = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
w = (255, 255, 255, 255)
lw = 10 * S

def p(x, y):
    return (x * S, y * S)

# handle and basket outline
d.line([p(14, 26), p(30, 26), p(42, 82), p(100, 82)], fill=w, width=lw, joint="curve")
d.polygon([p(34, 38), p(112, 38), p(102, 70), p(41, 70)], fill=w)
# basket slots
for x in (56, 72, 88):
    d.line([p(x, 44), p(x - 2, 64)], fill=(0, 0, 0, 0), width=5 * S)
# wheels
for cx in (50, 92):
    d.ellipse([p(cx - 9, 92), p(cx + 9, 110)], fill=w)

img = img.resize((size, size), Image.LANCZOS)
img.save(sys.argv[1])
print("saved", sys.argv[1])
