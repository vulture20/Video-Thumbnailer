"""Builds every icon/banner file from the SVG sources in this folder.

Requirements: Python 3 with Pillow, Node.js with Playwright (Chromium), the Inter font
for the banner texts. Run from anywhere:  python3 assets/branding/build.py
"""
import json, subprocess, tempfile
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PNG = HERE / "png"
HICOLOR = ROOT / "packaging" / "linux" / "icons" / "hicolor"
GUI_ASSETS = ROOT / "src" / "VideoThumbnailer.Gui" / "Assets"
IMAGES = ROOT / "docs" / "images"
APP_ID = "video-thumbnailer"

SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256, 512, 1024]

def source_for(size: int) -> Path:
    if size <= 16:
        return HERE / "icon-16.svg"
    if size <= 32:
        return HERE / "icon-small.svg"
    return HERE / "icon.svg"

def write_icns(target: Path, png_dir: Path) -> None:
    """Writes an .icns with PNG payloads for every slot (Pillow skips the 16/32 px 1x slots)."""
    import struct
    slots = [("icp4", 16), ("icp5", 32), ("icp6", 64), ("ic07", 128), ("ic08", 256),
             ("ic09", 512), ("ic10", 1024), ("ic11", 32), ("ic12", 64), ("ic13", 512), ("ic14", 1024)]
    body = b""
    for code, size in slots:
        data = (png_dir / f"icon-{size}.png").read_bytes()
        body += code.encode() + struct.pack(">I", len(data) + 8) + data
    target.write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)

def main() -> None:
    subprocess.run(["python3", str(HERE / "make_banners.py")], check=True)
    PNG.mkdir(exist_ok=True)
    jobs = [[str(source_for(s)), str(PNG / f"icon-{s}.png"), s, s] for s in SIZES]
    jobs += [[str(HERE / "banner.svg"), str(IMAGES / "banner.png"), 1280, 320],
             [str(HERE / "social-preview.svg"), str(IMAGES / "social-preview.png"), 1280, 640]]
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
        json.dump(jobs, f)
    subprocess.run(["node", str(HERE / "render.js"), f.name], check=True)

    img = {s: Image.open(PNG / f"icon-{s}.png").convert("RGBA") for s in SIZES}

    # Windows: multi-size .ico (exe icon and Avalonia window icon)
    ico_sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    GUI_ASSETS.mkdir(parents=True, exist_ok=True)
    for target in (HERE / "app.ico", GUI_ASSETS / "app.ico"):
        img[256].save(target, format="ICO", sizes=[(s, s) for s in ico_sizes],
                      append_images=[img[s] for s in ico_sizes if s != 256])

    # macOS: .icns (16 ... 1024, incl. @2x variants)
    write_icns(HERE / "app.icns", PNG)

    # Linux: hicolor theme + scalable SVG
    for s in (16, 24, 32, 48, 64, 128, 256, 512):
        d = HICOLOR / f"{s}x{s}" / "apps"
        d.mkdir(parents=True, exist_ok=True)
        img[s].save(d / f"{APP_ID}.png", optimize=True)
    sc = HICOLOR / "scalable" / "apps"
    sc.mkdir(parents=True, exist_ok=True)
    (sc / f"{APP_ID}.svg").write_text((HERE / "icon.svg").read_text())

    # Optimise PNGs a little
    for p in list(PNG.glob("*.png")) + [IMAGES / "banner.png", IMAGES / "social-preview.png"]:
        Image.open(p).save(p, optimize=True)
    print("done")

if __name__ == "__main__":
    main()
