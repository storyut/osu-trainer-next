#!/usr/bin/env python3
"""Regenerate osu-trainer-avalonia's committed icon assets from Assets/logo.svg.

Run by hand when the logo changes, not as part of the build -- `dotnet build` must not
depend on Edge or Python. The outputs are committed.

    python tools/make-icons.py

Requires: Microsoft Edge (headless rasterizer) and Pillow.
"""

import argparse
import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image

RENDER_SIZE = 1024
ICO_SIZES = [256, 128, 64, 48, 32, 24, 16]
PAD_FRACTION = 0.04  # per side, of the trimmed artwork -- keeps the ring off the edge

EDGE_CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]


def fail(message):
    print(f"make-icons: {message}", file=sys.stderr)
    sys.exit(1)


def find_edge(explicit):
    if explicit:
        if not os.path.isfile(explicit):
            fail(f"--edge path does not exist: {explicit}")
        return explicit
    for candidate in EDGE_CANDIDATES:
        if os.path.isfile(candidate):
            return candidate
    fail("Microsoft Edge not found. Searched:\n  " + "\n  ".join(EDGE_CANDIDATES))


def render_svg(edge, svg_path, out_png, size):
    """Rasterize the SVG via headless Edge onto a transparent backdrop.

    Both the --screenshot path and the file:/// URL must be absolute: a relative
    --screenshot fails with 'Access is denied', and a relative input is parsed as a
    hostname. Both observed while building this pipeline.
    """
    with tempfile.TemporaryDirectory() as profile_dir:
        subprocess.run(
            [
                edge,
                "--headless=new",
                "--disable-gpu",
                "--no-sandbox",
                "--hide-scrollbars",
                f"--user-data-dir={profile_dir}",
                "--default-background-color=00000000",
                f"--screenshot={os.path.abspath(out_png)}",
                f"--window-size={size},{size}",
                "file:///" + os.path.abspath(svg_path).replace("\\", "/"),
            ],
            check=False,
            capture_output=True,
        )
    if not os.path.isfile(out_png):
        fail(f"headless Edge produced no screenshot at {out_png} (input was {svg_path})")


def trim_and_pad(image):
    """Crop to the artwork's alpha bounding box, then re-pad evenly.

    The SVG's artwork fills only part of its canvas, so an untrimmed 16px icon would
    spend most of its pixels on transparency.
    """
    image = image.convert("RGBA")
    bbox = image.getbbox()
    if bbox is None:
        fail("rendered image is fully transparent -- the headless render produced an empty page")
    cropped = image.crop(bbox)

    side = max(cropped.size)
    pad = int(round(side * PAD_FRACTION))
    canvas = Image.new("RGBA", (side + 2 * pad, side + 2 * pad), (0, 0, 0, 0))
    canvas.paste(
        cropped,
        ((canvas.width - cropped.width) // 2, (canvas.height - cropped.height) // 2),
    )
    return canvas


def main():
    repo_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--svg", default=os.path.join(repo_root, "osu-trainer-avalonia", "Assets", "logo.svg"))
    parser.add_argument("--out-dir", default=os.path.join(repo_root, "osu-trainer-avalonia", "Assets"))
    parser.add_argument("--edge", default=None)
    args = parser.parse_args()

    if not os.path.isfile(args.svg):
        fail(f"svg not found: {args.svg}")
    if not os.path.isdir(args.out_dir):
        fail(f"output directory not found: {args.out_dir}")

    edge = find_edge(args.edge)
    work_dir = tempfile.mkdtemp(prefix="make-icons-")
    try:
        raw_png = os.path.join(work_dir, "render.png")
        render_svg(edge, args.svg, raw_png, RENDER_SIZE)
        artwork = trim_and_pad(Image.open(raw_png))

        ico_path = os.path.join(args.out_dir, "logo.ico")
        frames = [artwork.resize((s, s), Image.LANCZOS) for s in ICO_SIZES]
        frames[0].save(ico_path, format="ICO", sizes=[(s, s) for s in ICO_SIZES])
        print(f"wrote {ico_path} ({len(ICO_SIZES)} sizes)")

        png_path = os.path.join(args.out_dir, "logo-64.png")
        artwork.resize((64, 64), Image.LANCZOS).save(png_path, format="PNG")
        print(f"wrote {png_path}")
    finally:
        shutil.rmtree(work_dir, ignore_errors=True)


if __name__ == "__main__":
    main()
