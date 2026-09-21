#!/usr/bin/env python3
"""Write a multi-size ICO: charcoal tile, teal mark, gold spark."""

from __future__ import annotations

import math
import struct
from pathlib import Path


def clamp(v: float) -> int:
    return max(0, min(255, int(v)))


def render(size: int) -> list[tuple[int, int, int, int]]:
    pixels: list[tuple[int, int, int, int]] = []
    cx = cy = (size - 1) / 2.0
    radius = size * 0.46
    for y in range(size):
        for x in range(size):
            dx = x - cx
            dy = y - cy
            dist = math.hypot(dx, dy)
            # rounded square
            ax, ay = abs(dx) / radius, abs(dy) / radius
            box = max(ax, ay)
            if box > 1.05:
                pixels.append((0, 0, 0, 0))
                continue
            edge = 1.0 - min(1.0, max(0.0, (1.05 - box) * 8))
            r = 14 + 8 * (1 - box)
            g = 16 + 6 * (1 - box)
            b = 22 + 10 * (1 - box)
            a = 255 if box < 0.98 else clamp(255 * (1.05 - box) / 0.07)

            # inner teal hex-ish diamond
            hexn = (abs(dx) * 0.72 + abs(dy)) / (radius * 0.55)
            if hexn < 1:
                glow = 1 - hexn
                r = r * 0.25 + 20 * glow
                g = g * 0.25 + 210 * glow
                b = b * 0.25 + 200 * glow

            # gold spark upper-right
            spark = math.hypot(dx - radius * 0.28, dy + radius * 0.30)
            if spark < radius * 0.16:
                t = 1 - spark / (radius * 0.16)
                r = r * (1 - t) + 230 * t
                g = g * (1 - t) + 186 * t
                b = b * (1 - t) + 90 * t

            r = r * (1 - edge * 0.35)
            g = g * (1 - edge * 0.35)
            b = b * (1 - edge * 0.35)
            pixels.append((clamp(b), clamp(g), clamp(r), clamp(a)))
    return pixels


def dib(size: int, pixels: list[tuple[int, int, int, int]]) -> bytes:
    # BITMAPINFOHEADER + BGRA XOR + 1-bit AND mask
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        size,
        size * 2,
        1,
        32,
        0,
        0,
        0,
        0,
        0,
        0,
    )
    xor = bytearray()
    for y in range(size - 1, -1, -1):
        row = pixels[y * size : (y + 1) * size]
        for b, g, r, a in row:
            xor += struct.pack("BBBB", b, g, r, a)
    and_stride = ((size + 31) // 32) * 4
    and_mask = bytes(and_stride * size)
    return header + bytes(xor) + and_mask


def write_ico(path: Path, sizes: list[int]) -> None:
    images = [dib(s, render(s)) for s in sizes]
    count = len(sizes)
    header = struct.pack("<HHH", 0, 1, count)
    offset = 6 + 16 * count
    entries = b""
    for size, data in zip(sizes, images):
        w = 0 if size >= 256 else size
        h = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(data), offset)
        offset += len(data)
    path.write_bytes(header + entries + b"".join(images))


if __name__ == "__main__":
    out = Path(__file__).resolve().parents[1] / "src" / "KilrkrowLauncher" / "Assets" / "launcher.ico"
    out.parent.mkdir(parents=True, exist_ok=True)
    write_ico(out, [16, 24, 32, 48, 64, 256])
    print("wrote", out, "bytes", out.stat().st_size)
