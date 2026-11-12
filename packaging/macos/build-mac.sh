#!/usr/bin/env bash
# Build a runnable, shareable Zero X.app without an Apple Developer certificate.
#
# usage: packaging/macos/build-mac.sh [arm64|x64|both]      (default: this Mac's architecture)
#
# For each architecture it:
#   1. publishes a self-contained single-file build        -> publish/osx-<arch>/
#   2. wraps it in an app bundle (make-app.sh)              -> publish/macos-<arch>/Zero X.app
#   3. stamps the version from Directory.Build.props into the bundle's Info.plist
#   4. ad-hoc signs the bundle (no certificate involved)
#   5. zips it with ditto, which keeps the signature intact -> publish/Zero-X-<version>-macos-<arch>.zip
#
# The result is not notarised, so on a Mac that downloaded it Gatekeeper asks for approval on the
# first run. See Build.md.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
cd "$root"

if [ "$(uname -s)" != "Darwin" ]; then
  echo "error: this script needs macOS (codesign, ditto). See Build.md for other systems." >&2
  exit 1
fi
if ! command -v dotnet >/dev/null; then
  echo "error: the .NET 10 SDK is not installed. Get it from https://dotnet.microsoft.com/download" >&2
  exit 1
fi

case "${1:-host}" in
  host)
    case "$(uname -m)" in
      arm64)  archs="arm64" ;;
      x86_64) archs="x64" ;;
      *) echo "error: unknown architecture $(uname -m)" >&2; exit 1 ;;
    esac ;;
  arm64|x64) archs="$1" ;;
  both)      archs="arm64 x64" ;;
  *) echo "usage: $0 [arm64|x64|both]" >&2; exit 1 ;;
esac

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)"
version="${version:-0.0.0}"

for arch in $archs; do
  rid="osx-$arch"
  out="publish/$rid"
  app="publish/macos-$arch/Zero X.app"
  zip="publish/Zero-X-$version-macos-$arch.zip"

  echo "==> publishing $rid"
  rm -rf "$out"
  dotnet publish src/Zero.App/Zero.App.csproj -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o "$out"

  echo "==> bundling"
  mkdir -p "$(dirname "$app")"
  "$here/make-app.sh" "$out" "$app"
  /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $version" \
                          -c "Set :CFBundleShortVersionString $version" "$app/Contents/Info.plist"

  echo "==> ad-hoc signing"
  codesign --force --deep --sign - "$app"
  codesign --verify --deep --strict "$app"

  echo "==> zipping"
  rm -f "$zip"
  ditto -c -k --keepParent "$app" "$zip"

  echo "built $app"
  echo "      $zip"
done
