"""Draws the 1024x1024 thumbnail options into release/options/, same look as the Train Timer thumbnail.

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


# Shared layout for every zezinho thumbnail: same icon height, title size and gaps, so the set looks alike
ICON_H, TITLE_CAP, SUB_CAP, GAP, MAX_TEXT_W = 470, 112, 54, 46, 860


def _cap_size(path, cap):
    """Font size whose capital letters are `cap` px tall at 1x."""
    box = ImageFont.truetype(path, 200).getbbox("H")
    return 200 * cap / (box[3] - box[1])


def _text_ink(parts, spacing):
    """The text on its own transparent layer, cropped to its visible ink so it centres exactly."""
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    width = sum(probe.textlength(ch, font=f) for ch, f, _ in parts) + spacing * (len(parts) - 1)
    layer = Image.new("RGBA", (int(width) + p(40), p(300)), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    x = p(20)
    for ch, font, colour in parts:
        ld.text((x, p(150)), ch, font=font, fill=colour, anchor="lm")
        x += ld.textlength(ch, font=font) + spacing
    return layer.crop(layer.getbbox())


def finish(img, out, title, sub, sub_initials=()):
    """`img` holds only the icon (supersampled). Scales it, sets the title and subtitle under it and saves."""
    box = ImageChops.difference(img, Image.new("RGB", img.size, BG)).getbbox()  # the background is not black
    icon = img.crop(box)
    k = p(ICON_H) / icon.height
    icon = icon.resize((round(icon.width * k), round(icon.height * k)), Image.LANCZOS)
    # keep the icon where it was drawn horizontally, so shapes with a side part (a crown, a button) stay balanced
    icon_x = round(W / 2 + (box[0] - W / 2) * k)

    size = p(_cap_size(FONT_BOLD, TITLE_CAP))
    while True:
        font = ImageFont.truetype(FONT_BOLD, round(size))
        title_img = _text_ink([(ch, font, CREAM) for ch in title], 0)
        if title_img.width <= p(MAX_TEXT_W):
            break
        size *= 0.97

    # the subtitle keeps its wide spacing when it fits; long ones tighten the spacing first, then shrink
    size = p(_cap_size(FONT_SEMI, SUB_CAP))
    while True:
        # the subtitle is bold too: thinner strokes smear when the Forge shrinks the image to a 192px card
        regular, bold = ImageFont.truetype(FONT_BOLD, round(size)), ImageFont.truetype(FONT_BOLD, round(size))
        base = CREAM if sub_initials else ORANGE
        parts = [(ch, bold if i in sub_initials else regular, ORANGE if i in sub_initials else base) for i, ch in enumerate(sub)]
        sub_img = None
        for factor in (0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2):
            sub_img = _text_ink(parts, size * factor)
            if sub_img.width <= p(MAX_TEXT_W):
                break
        if sub_img.width <= p(MAX_TEXT_W):
            break
        size *= 0.97

    total = icon.height + title_img.height + sub_img.height + 2 * p(GAP)
    top = (W - total) / 2
    canvas = Image.new("RGB", (W, W), BG)
    canvas.paste(icon, (icon_x, int(top)))
    y = top + icon.height + p(GAP)
    canvas.paste(title_img, (int(W / 2 - title_img.width / 2), int(y)), title_img)
    y += title_img.height + p(GAP)
    canvas.paste(sub_img, (int(W / 2 - sub_img.width / 2), int(y)), sub_img)
    out.parent.mkdir(parents=True, exist_ok=True)
    result = canvas.resize((1024, 1024), Image.LANCZOS)
    result.save(out, optimize=True)
    return result


def hexagon(d, cx, cy, r, width, colour=ORANGE):
    pts = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(-90, 270, 60)]
    d.line(pts + [pts[0], pts[1]], fill=colour, width=width, joint="curve")


def cart(d, ox, oy, k, colour=CREAM, slots=True):
    """Cart drawn in a 320x210 box whose top left is (ox, oy), scaled by k."""
    q = lambda x, y: (ox + p(x * k), oy + p(y * k))
    d.line([q(0, 0), q(40, 0), q(72, 140), q(274, 140)], fill=colour, width=int(p(22 * k)), joint="curve")
    d.polygon([q(48, 28), q(320, 28), q(288, 114), q(66, 114)], fill=colour)
    if slots:
        for x in (114, 170, 226):
            d.line([q(x, 44), q(x - 6, 100)], fill=BG, width=int(p(14 * k)))
    for wx in (100, 252):
        d.ellipse([q(wx - 24, 162), q(wx + 24, 210)], fill=colour)


def tick(d, x, y, size, colour, width):
    d.line([(x, y + size * 0.5), (x + size * 0.38, y + size * 0.88), (x + size, y)], fill=colour, width=width, joint="curve")


def icon_hex_cart(d):
    hexagon(d, p(512), p(420), p(250), p(26))
    cart(d, p(352), p(338), 1)


def icon_cart_tick(d):
    cart(d, p(312), p(300), 1.25)
    d.ellipse([p(600), p(190), p(760), p(350)], fill=ORANGE)
    tick(d, p(640), p(238), p(80), BG, p(20))


def icon_clipboard(d):
    d.rounded_rectangle([p(352), p(190), p(672), p(620)], radius=p(28), outline=CREAM, width=p(22))
    d.rounded_rectangle([p(436), p(164), p(588), p(222)], radius=p(16), fill=ORANGE)
    for i, y in enumerate((p(300), p(390), p(480))):
        if i < 2:
            tick(d, p(400), y - p(20), p(44), ORANGE, p(14))
        else:
            d.rounded_rectangle([p(400), y - p(22), p(444), y + p(22)], radius=p(8), outline=ORANGE, width=p(10))
        d.rounded_rectangle([p(468), y - p(9), p(620), y + p(9)], radius=p(9), fill=CREAM)


def icon_bag(d):
    hexagon(d, p(512), p(420), p(250), p(26))
    d.arc([p(442), p(250), p(582), p(390)], 180, 360, fill=CREAM, width=p(22))
    d.rounded_rectangle([p(392), p(320), p(632), p(560)], radius=p(26), fill=CREAM)
    for hx in (p(452), p(572)):
        d.ellipse([hx - p(12), p(352), hx + p(12), p(376)], fill=BG)
    tick(d, p(458), p(420), p(108), ORANGE, p(22))


def icon_crate_tag(d):
    d.rounded_rectangle([p(332), p(310), p(642), p(600)], radius=p(16), outline=CREAM, width=p(22))
    d.line([p(332), p(410), p(642), p(410)], fill=CREAM, width=p(18))
    d.line([p(342), p(590), p(632), p(420)], fill=CREAM, width=p(18))
    tag = Image.new("RGBA", (p(300), p(300)), (0, 0, 0, 0))
    td = ImageDraw.Draw(tag)
    td.polygon([(p(60), p(110)), (p(110), p(60)), (p(250), p(60)), (p(250), p(160)), (p(110), p(160))], fill=ORANGE)
    td.ellipse([p(100), p(98), p(124), p(122)], fill=BG)
    tag = tag.rotate(-30, resample=Image.BICUBIC)
    return tag, (p(520), p(150))


def icon_cart_list(d):
    hexagon(d, p(512), p(420), p(250), p(26))
    cart(d, p(372), p(398), 0.85, slots=False)
    for i, y in enumerate((p(270), p(318))):
        d.rounded_rectangle([p(430), y - p(9), p(600 - i * 50), y + p(9)], radius=p(9), fill=ORANGE)


OPTIONS = {
    "A-hex-cart": icon_hex_cart,
    "B-cart-tick": icon_cart_tick,
    "C-clipboard": icon_clipboard,
    "D-bag-hex": icon_bag,
    "E-crate-tag": icon_crate_tag,
    "F-hex-cart-list": icon_cart_list,
}


def render(draw_icon, out):
    img = Image.new("RGB", (W, W), BG)
    d = ImageDraw.Draw(img)
    extra = draw_icon(d)
    if extra:
        layer, pos = extra
        img.paste(layer, pos, layer)

    return finish(img, out, "SHOPPING", "LIST")


def main():
    folder = ROOT / "release" / "options"
    folder.mkdir(parents=True, exist_ok=True)
    images = [(name, render(fn, folder / f"ShoppingList-thumbnail-{name}.png")) for name, fn in OPTIONS.items()]
    render(icon_clipboard, ROOT / "release" / "ShoppingList-thumbnail.png")

    # contact sheet to compare them side by side
    cell = 340
    sheet = Image.new("RGB", (cell * 3 + 40, (cell + 40) * 2 + 20), (30, 30, 30))
    sd = ImageDraw.Draw(sheet)
    label_font = ImageFont.truetype(FONT_SEMI, 22)
    for i, (name, im) in enumerate(images):
        x, y = 10 + (i % 3) * (cell + 10), 10 + (i // 3) * (cell + 40)
        sheet.paste(im.resize((cell, cell), Image.LANCZOS), (x, y))
        sd.text((x + 4, y + cell + 6), name, font=label_font, fill=CREAM)
    sheet.save(folder / "options.png")
    print(folder)


if __name__ == "__main__":
    main()
