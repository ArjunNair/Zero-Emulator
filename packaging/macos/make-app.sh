#!/usr/bin/env bash
# Wrap a `dotnet publish` output folder into Zero.app.
# usage: make-app.sh <publish-dir> <output.app>
# Signing/notarisation (needs an Apple Developer identity) is left to the caller, e.g.:
#   codesign --deep --force --options runtime --sign "Developer ID Application: ..." Zero.app
#   xcrun notarytool submit Zero.zip --keychain-profile ... --wait && xcrun stapler staple Zero.app
set -euo pipefail
src="$1"; app="$2"
here="$(cd "$(dirname "$0")" && pwd)"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$src"/. "$app/Contents/MacOS/"
cp "$here/Info.plist" "$app/Contents/Info.plist"
# Icon: build .icns from the Windows .ico if the tools are present (macOS only).
ico="$here/../../src/Zero.App/Assets/zero.ico"
if command -v sips >/dev/null && command -v iconutil >/dev/null && [ -f "$ico" ]; then
  tmp="$(mktemp -d)"; mkdir -p "$tmp/Zero.iconset"
  sips -s format png -Z 256 "$ico" --out "$tmp/base.png" >/dev/null 2>&1 || true
  if [ -f "$tmp/base.png" ]; then
    for s in 16 32 128 256; do sips -Z $s "$tmp/base.png" --out "$tmp/Zero.iconset/icon_${s}x${s}.png" >/dev/null; done
    cp "$tmp/Zero.iconset/icon_32x32.png" "$tmp/Zero.iconset/icon_16x16@2x.png"
    cp "$tmp/Zero.iconset/icon_256x256.png" "$tmp/Zero.iconset/icon_128x128@2x.png"
    iconutil -c icns "$tmp/Zero.iconset" -o "$app/Contents/Resources/Zero.icns" || true
  fi
  rm -rf "$tmp"
fi
chmod +x "$app/Contents/MacOS/Zero"
echo "created $app"
