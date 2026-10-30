"""Draws release/ShoppingList-thumbnail.png (1024x1024), same look as the Train Timer thumbnail.

Usage: python -I tools/thumbnail.py
"""
import math
import pathlib

from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
S = 4  # supersampling
W = 1024 * S
BG = (17, 17, 17)
ORANGE = (242, 170, 60)
CREAM = (239, 235, 224)
FONT_BOLD = "C:/Windows/Fonts/segoeuib.ttf"
FONT_SEMI = "C:/Windows/Fonts/seguisb.ttf"


def p(v):
    return v * S


def main():
    img = Image.new("RGB", (W, W), BG)
    d = ImageDraw.Draw(img)

    # the hideout's hexagon, pointy top like the area icons
    cx, cy, r = p(512), p(420), p(250)
    hexagon = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)]
    d.line(hexagon + [hexagon[0], hexagon[1]], fill=ORANGE, width=p(26), joint="curve")

    # shopping cart: handle, basket with slots, wheels
    line = p(22)
    d.line([(p(352), p(338)), (p(392), p(338)), (p(424), p(478)), (p(626), p(478))], fill=CREAM, width=line, joint="curve")
    d.polygon([(p(400), p(366)), (p(672), p(366)), (p(640), p(452)), (p(418), p(452))], fill=CREAM)
    for x in (p(466), p(522), p(578)):
        d.line([(x, p(382)), (x - p(6), p(438))], fill=BG, width=p(14))
    for wx in (p(452), p(604)):
        d.ellipse([wx - p(24), p(500), wx + p(24), p(548)], fill=CREAM)

    def paste_centred(layer, y_top):
        box = layer.getbbox()
        piece = layer.crop(box)
        img.paste(piece, (int(W / 2 - piece.width / 2), int(y_top)), piece)

    def line_layer(parts, spacing):
        probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
        width = sum(probe.textlength(ch, font=f) for ch, f, _ in parts) + spacing * (len(parts) - 1)
        layer = Image.new("RGBA", (int(width) + p(40), p(300)), (0, 0, 0, 0))
        ld = ImageDraw.Draw(layer)
        x = p(20)
        for ch, font, colour in parts:
            ld.text((x, p(150)), ch, font=font, fill=colour, anchor="lm")
            x += ld.textlength(ch, font=font) + spacing
        return layer

    title_font = ImageFont.truetype(FONT_BOLD, p(150))
    title_layer = line_layer([(ch, title_font, CREAM) for ch in "SHOPPING"], 0)
    sub_font = ImageFont.truetype(FONT_SEMI, p(64))
    sub_layer = line_layer([(ch, sub_font, ORANGE) for ch in "LIST"], p(64) * 0.9)

    # one gap between icon, title and subtitle, chosen so the top and bottom margins match
    icon_box = ImageChops.difference(img, Image.new("RGB", img.size, BG)).getbbox()
    title_h = title_layer.getbbox()[3] - title_layer.getbbox()[1]
    sub_h = sub_layer.getbbox()[3] - sub_layer.getbbox()[1]
    gap = (W - icon_box[1] - icon_box[3] - title_h - sub_h) / 2
    title_top = icon_box[3] + gap
    paste_centred(title_layer, title_top)
    paste_centred(sub_layer, title_top + title_h + gap)

    out = ROOT / "release" / "ShoppingList-thumbnail.png"
    out.parent.mkdir(exist_ok=True)
    img.resize((1024, 1024), Image.LANCZOS).save(out, optimize=True)
    print(out)


if __name__ == "__main__":
    main()
