#!/bin/bash
# 在仓库根目录运行：bash src/ToMP3/Assets/ffmpeg/build.sh artifacts/ffmpeg-build
# 仅构建机需要 Xcode 命令行工具；生成的工具只依赖 macOS 系统库。
set -euo pipefail
export PATH=/usr/bin:/bin:/usr/sbin:/sbin
export MACOSX_DEPLOYMENT_TARGET=12.0
export SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
export CC=clang
unset CPATH LIBRARY_PATH PKG_CONFIG_PATH DYLD_LIBRARY_PATH DYLD_FALLBACK_LIBRARY_PATH
vendor="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "${1:?请指定 artifacts 下的构建位置}"
work="$(cd "$1" && pwd)"
# 使用新目录，避免旧配置或本机 Homebrew 库混入。
work="$(mktemp -d "$work/build.XXXXXX")"
prefix="$work/prefix"
cd "$vendor/source"
printf '%s\n' \
  '6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818  ffmpeg-8.0.3.tar.xz' \
  'ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e  lame-3.100.tar.gz' | shasum -a 256 -c -
tar -xf lame-3.100.tar.gz -C "$work"
tar -xf ffmpeg-8.0.3.tar.xz -C "$work"
cd "$work/lame-3.100"
./configure --prefix="$prefix" --build=aarch64-apple-darwin \
  --disable-shared --enable-static --disable-frontend --disable-dependency-tracking
make -j8
make install
cd "$work/ffmpeg-8.0.3"
./configure --prefix="$prefix" --cc=clang --arch=arm64 --target-os=darwin \
  --disable-autodetect --disable-network --disable-doc --disable-debug \
  --disable-shared --enable-static --disable-ffplay --enable-libmp3lame \
  --extra-cflags="-I$prefix/include" --extra-ldflags="-L$prefix/lib" \
  --disable-encoders --enable-encoder=libmp3lame,flac,pcm_s16le \
  --disable-muxers --enable-muxer=mp3,flac,null,hash \
  --disable-devices --enable-indev=lavfi
make -j8 ffmpeg ffprobe
cp ffmpeg ffprobe "$vendor/"
cp COPYING.LGPLv2.1 "$vendor/FFmpeg-LICENSE.txt"
cp "$work/lame-3.100/COPYING" "$vendor/LAME-LICENSE.txt"
chmod 755 "$vendor/ffmpeg" "$vendor/ffprobe"
codesign --force --sign - "$vendor/ffmpeg" "$vendor/ffprobe"
otool -L "$vendor/ffmpeg" "$vendor/ffprobe"
