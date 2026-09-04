#!/usr/bin/env sh
set -eu

source_image=${1:?Usage: generate-icons.sh <source-image>}
repo_root=$(CDPATH='' cd -- "$(dirname -- "$0")/../.." && pwd)
linux_icons_dir=$repo_root/eng/packaging/linux/icons/hicolor
macos_packaging_dir=$repo_root/eng/packaging/macos
icon_name=io.github.thomas-fazzari.gbc-net.png

case "$source_image" in
/*) ;;
*) source_image=$repo_root/$source_image ;;
esac

render_icon() {
        size=$1
        output=$2
        background=${3:-none}
        scale=${4:-100}
        content_size=$((size * scale / 100))

        mkdir -p "$(dirname -- "$output")"
        magick "$source_image" \
                -filter point \
                -resize "${content_size}x${content_size}" \
                -gravity center \
                -background "$background" \
                -extent "${size}x${size}" \
                -strip \
                "$output"
}

for size in 16 22 24 32 48 64 128 256 512; do
        render_icon "$size" "$linux_icons_dir/${size}x${size}/apps/$icon_name"
done

if command -v iconutil >/dev/null 2>&1; then
        iconset_dir=$(mktemp -d)/GbcNet.iconset
        trap 'rm -rf "${iconset_dir%/*}"' EXIT HUP INT TERM

        render_icon 16 "$iconset_dir/icon_16x16.png" '#190C2E' 92
        render_icon 32 "$iconset_dir/icon_16x16@2x.png" '#190C2E' 92
        render_icon 32 "$iconset_dir/icon_32x32.png" '#190C2E' 92
        render_icon 64 "$iconset_dir/icon_32x32@2x.png" '#190C2E' 92
        render_icon 128 "$iconset_dir/icon_128x128.png" '#190C2E' 92
        render_icon 256 "$iconset_dir/icon_128x128@2x.png" '#190C2E' 92
        render_icon 256 "$iconset_dir/icon_256x256.png" '#190C2E' 92
        render_icon 512 "$iconset_dir/icon_256x256@2x.png" '#190C2E' 92
        render_icon 512 "$iconset_dir/icon_512x512.png" '#190C2E' 92
        render_icon 1024 "$iconset_dir/icon_512x512@2x.png" '#190C2E' 92
        iconutil --convert icns --output "$macos_packaging_dir/GbcNet.icns" "$iconset_dir"
fi
