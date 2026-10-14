#!/bin/bash
# 在仓库根目录运行：
#   bash src/ToMP3/Assets/ffmpeg/build.sh artifacts/ffmpeg-build          # macOS arm64（默认）
#   bash src/ToMP3/Assets/ffmpeg/build.sh artifacts/ffmpeg-build win      # Windows x64
#
# macOS 目标仅需 Xcode 命令行工具，产物只依赖 macOS 系统库；
# Windows 目标需要 mingw-w64（brew install mingw-w64），产物只依赖 Windows 系统 DLL。
# 两个目标都用同一份 source/ 里的官方源码与同一段解码器筛选逻辑，保证解码器集合一致。
set -euo pipefail

target="${2:-mac}"

# 解码器筛选：从官方注册表保留全部内置音频解码器，避免手写格式白名单漏掉音频。
# 封面仅需 PNG/JPEG/BMP；视频转码和字幕不属于本应用功能。
# 8.0.3 的这八个音频解码器注册在 audio 区段之外，也必须保留。
# 两个平台共用，改这里两个目标同时生效。
decoders=png,mjpeg,bmp,eightsvx_exp,eightsvx_fib,anull,comfortnoise,dvaudio,rka,s302m,speex

vendor="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "${1:?请指定 artifacts 下的构建位置}"
work="$(cd "$1" && pwd)"
# 使用新目录，避免旧配置或本机 Homebrew 库混入。
work="$(mktemp -d "$work/build.XXXXXX")"
prefix="$work/prefix"

case "$target" in
  mac)
    export PATH=/usr/bin:/bin:/usr/sbin:/sbin
    export MACOSX_DEPLOYMENT_TARGET=12.0
    export SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
    export CC=clang
    unset CPATH LIBRARY_PATH PKG_CONFIG_PATH DYLD_LIBRARY_PATH DYLD_FALLBACK_LIBRARY_PATH
    cross=""
    ff_args=(--cc=clang --arch=arm64 --target-os=darwin)
    lame_args=(--build=aarch64-apple-darwin)
    exe_suffix=""
    ;;
  win)
    # 只留 Homebrew（nasm 与 mingw 工具链在这里）加系统目录：排除 MacPorts（/opt/local）
    # 和其他开发机路径，免得 configure 抓到别的 gm4/ar/头文件。
    export PATH="/opt/homebrew/bin:/usr/bin:/bin:/usr/sbin:/sbin"
    # 交叉编译时 FFmpeg 仍要用本机编译器生成 bin2c 这类主机工具（host_cc 默认叫 "gcc"）。
    # 必须显式给 SDKROOT，否则本机 clang 会退到 CommandLineTools 那个 .tbd 有问题的 SDK，
    # 主机工具链接失败。SDKROOT 只影响 Apple clang，不影响 mingw 交叉编译器。
    export SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
    unset CPATH LIBRARY_PATH PKG_CONFIG_PATH DYLD_LIBRARY_PATH DYLD_FALLBACK_LIBRARY_PATH
    cross="x86_64-w64-mingw32-"
    command -v "${cross}gcc" >/dev/null || {
      echo "找不到 ${cross}gcc：先运行 brew install mingw-w64" >&2; exit 1; }
    command -v nasm >/dev/null || {
      echo "找不到 nasm：先运行 brew install nasm（x86_64 汇编需要）" >&2; exit 1; }
    # --host-cc 钉住本机编译器，不让它按 PATH 找名叫 "gcc" 的东西。
    ff_args=(--arch=x86_64 --target-os=mingw32 --cross-prefix="$cross" --enable-cross-compile
             --host-cc=clang)
    # LAME 3.100 的 autoconf 不认识 mingw 三元组，显式喂工具链；
    # 它的 nasm 汇编只针对 32 位 i386，x86_64 目标用不到，直接关掉。
    lame_args=(--host=x86_64-w64-mingw32 --build=x86_64-apple-darwin --disable-nasm)
    exe_suffix=".exe"
    ;;
  *)
    echo "未知目标 '$target'（只支持 mac / win）" >&2; exit 1 ;;
esac

cd "$vendor/source"
printf '%s\n' \
  '6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818  ffmpeg-8.0.3.tar.xz' \
  'ddfe36cab873794038ae2c1210557ad34857a4b6bdc515785d1da9e175b1da1e  lame-3.100.tar.gz' | shasum -a 256 -c -
tar -xf lame-3.100.tar.gz -C "$work"
tar -xf ffmpeg-8.0.3.tar.xz -C "$work"

# Windows 目标没有系统 zlib（macOS 有 libz），从随附源码交叉编译一份静态库。
# PNG 封面解码依赖它，不能为了省事砍掉 --enable-zlib。
if [ "$target" = win ]; then
  zlib="$vendor/source/zlib-1.3.1.tar.gz"
  printf '%s\n' '9a93b2b7dfdac77ceba5a558a580e74667dd6fede4585b91eefb60f03b72df23  zlib-1.3.1.tar.gz' \
    | (cd "$vendor/source" && shasum -a 256 -c -)
  tar -xf "$zlib" -C "$work"
  ( cd "$work/zlib-1.3.1" &&
    CHOST=x86_64-w64-mingw32 ./configure --static --prefix="$prefix" &&
    make -j8 &&
    make install )
fi

cd "$work/lame-3.100"
# macOS 用默认 clang；Windows 走 --host 推断出的 mingw 工具链。
if [ "$target" = win ]; then
  CC="${cross}gcc" AR="${cross}ar" RANLIB="${cross}ranlib" \
    ./configure --prefix="$prefix" "${lame_args[@]}" \
    --disable-shared --enable-static --disable-frontend --disable-dependency-tracking
else
  ./configure --prefix="$prefix" "${lame_args[@]}" \
    --disable-shared --enable-static --disable-frontend --disable-dependency-tracking
fi
make -j8
make install

cd "$work/ffmpeg-8.0.3"
audio=false
while IFS= read -r line; do
  case "$line" in
    '/* audio codecs */') audio=true ;;
    '/* subtitles */') break ;;
  esac
  if $audio && [[ "$line" =~ ff_([a-zA-Z0-9_]+)_decoder ]]; then
    decoders+=,${BASH_REMATCH[1]}
  fi
done < libavcodec/allcodecs.c
[[ "$decoders" == *',flac,'* && "$decoders" == *',vorbis,'* ]] || exit 1

./configure --prefix="$prefix" "${ff_args[@]}" \
  --disable-autodetect --disable-network --disable-doc --disable-debug \
  --disable-shared --enable-static --disable-ffplay --enable-libmp3lame \
  --extra-cflags="-I$prefix/include" --extra-ldflags="-L$prefix/lib" \
  --disable-decoders --enable-decoder="$decoders" --enable-zlib \
  --disable-swscale --disable-filters \
  --enable-filter=abuffer,abuffersink,aformat,anull,aresample,atrim,asetpts,sine \
  --disable-encoders --enable-encoder=libmp3lame,flac,pcm_s16le \
  --disable-muxers --enable-muxer=mp3,flac,null,hash \
  --disable-devices --enable-indev=lavfi
# mingw 的 -latomic 会牵出 libwinpthread-1.dll（GCC 运行时，不是 Windows 系统 DLL）。
# 挂在 EXTRALIBS（所有库之后）用 -Bstatic 只把这一个库静态链进来，包里就不用再带 DLL。
extra_make=()
[ "$target" = win ] && extra_make=(EXTRALIBS="-Wl,-Bstatic -lwinpthread -Wl,-Bdynamic")
make -j8 "ffmpeg$exe_suffix" "ffprobe$exe_suffix" "${extra_make[@]+"${extra_make[@]}"}"

cp "ffmpeg$exe_suffix" "ffprobe$exe_suffix" "$vendor/"
cp COPYING.LGPLv2.1 "$vendor/FFmpeg-LICENSE.txt"
cp "$work/lame-3.100/COPYING" "$vendor/LAME-LICENSE.txt"
chmod 755 "$vendor/ffmpeg$exe_suffix" "$vendor/ffprobe$exe_suffix"

if [ "$target" = win ]; then
  # 只允许依赖 Windows 系统 DLL；出现别的名字说明混进了开发机或 mingw 运行库。
  deps=$("${cross}objdump" -p "$vendor/ffmpeg.exe" "$vendor/ffprobe.exe" | grep "DLL Name" | awk '{print $3}' | sort -u)
  echo "$deps"
  for d in $deps; do
    case "$d" in
      KERNEL32.dll|USER32.dll|ADVAPI32.dll|SHELL32.dll|ole32.dll|OLEAUT32.dll|WS2_32.dll|bcrypt.dll|ntdll.dll|msvcrt.dll|api-ms-win-*.dll|VCRUNTIME*.dll) ;;
      *) echo "意外的动态依赖：$d" >&2; exit 1 ;;
    esac
  done
else
  codesign --force --sign - "$vendor/ffmpeg" "$vendor/ffprobe"
  otool -L "$vendor/ffmpeg" "$vendor/ffprobe"
fi
