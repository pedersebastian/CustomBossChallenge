"""Makes the website's boss portraits (#206) from the PNGs BossPortraitsTest saves from the game.

    python -I web/scripts/portraits.py <folder with the PNGs> web/public/bosses

Every <Boss>.png and <Boss>Elite.png becomes a 128 x 128 .webp of the same name, centred on a transparent square if it
isn't square already. Needs Pillow (pip install pillow). See web/public/bosses/README.md.
"""

import sys
from pathlib import Path

from PIL import Image

SIZE = 128


def convert(source: Path, target: Path) -> int:
    with Image.open(source) as opened:
        image = opened.convert("RGBA")
    side = max(image.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.paste(image, ((side - image.width) // 2, (side - image.height) // 2))
    square.resize((SIZE, SIZE), Image.LANCZOS).save(target, "WEBP", quality=80, method=6)
    return target.stat().st_size


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    source, target = Path(sys.argv[1]), Path(sys.argv[2])
    pngs = sorted(source.glob("*.png"))
    if not pngs:
        sys.exit(f"No PNGs in {source}")
    target.mkdir(parents=True, exist_ok=True)
    for png in pngs:
        out = target / (png.stem + ".webp")
        print(f"{out.name}: {convert(png, out) / 1024:.1f} KB")


if __name__ == "__main__":
    main()
