#!/usr/bin/env python3
"""从 Assets/icon.png 生成 Assets/icon.ico（Windows 可执行文件图标）。

在仓库根目录运行：python3 src/ToMP3/Assets/make-ico.py

只用到系统 sips 和 Python 标准库；ICO 直接内嵌 PNG 载荷（Vista 起支持），
不需要 BMP 加掩码那套。生成后提交 icon.ico，日常构建不再需要本脚本。
"""

import struct
import subprocess
import tempfile
from pathlib import Path

ASSETS = Path(__file__).resolve().parent
SIZES = [16, 32, 48, 256]


def main() -> None:
    src = ASSETS / "icon.png"
    with tempfile.TemporaryDirectory() as td:
        blobs = []
        for size in SIZES:
            out = Path(td) / f"{size}.png"
            subprocess.run(
                ["sips", "-z", str(size), str(size), str(src), "--out", str(out)],
                check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
            )
            blobs.append(out.read_bytes())

    # ICONDIR + 每个尺寸一条 ICONDIRENTRY；256 在目录里记 0。
    header = struct.pack("<HHH", 0, 1, len(SIZES))
    offset = 6 + 16 * len(SIZES)
    entries, data = b"", b""
    for size, blob in zip(SIZES, blobs):
        dim = 0 if size == 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        data += blob

    target = ASSETS / "icon.ico"
    target.write_bytes(header + entries + data)
    print(f"{target} <- {', '.join(f'{s}x{s}' for s in SIZES)}")


if __name__ == "__main__":
    main()
