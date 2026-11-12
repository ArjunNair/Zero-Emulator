# Packaging

How to make a runnable build on macOS, Windows and Linux is in [Build.md](../Build.md). This file
covers what is in this folder and the details Build.md leaves out.

## What is here

- `macos/build-mac.sh` — publishes, bundles, ad-hoc signs and zips `Zero X.app` in one go.
- `macos/make-app.sh` — wraps a `dotnet publish` output folder into an app bundle and builds its
  icon from `src/Zero.App/Assets/zero.ico`. Used by `build-mac.sh` and by CI.
- `macos/Info.plist` — the bundle's property list. `build-mac.sh` overwrites its version with the
  one in `Directory.Build.props`.
- `linux/zero.desktop` — a starting point for a menu entry, an AppImage or a Flatpak.

## Why the SDK is the only prerequisite

- **No C or C++ toolchain.** The build is plain IL with a single-file bundle, not NativeAOT, so there
  is no linker or platform SDK involved. Publishing for Windows from a Mac does not need Visual
  Studio or the Windows SDK.
- **No system SDL3 or Skia.** Both arrive as NuGet packages carrying prebuilt native libraries for
  every RID (`ppy.SDL3-CS` and `SkiaSharp`), and the publish step picks the ones for the target and
  bundles them into the executable.
- **No ICU.** The app builds with `InvariantGlobalization`, so it does not want the system
  globalisation libraries at runtime.

`global.json` pins the SDK to 10.0.100 with `rollForward: latestFeature`, so any 10.0.x SDK will do.

## All six at once

Every RID cross-publishes from any host:

```bash
for rid in win-x64 win-arm64 osx-x64 osx-arm64 linux-x64 linux-arm64; do
  dotnet publish src/Zero.App/Zero.App.csproj -c Release -r $rid --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o publish/$rid
done
rm -f publish/win-*/*.pdb
```

On a Mac, `packaging/macos/build-mac.sh both` then turns the two macOS builds into signed, zipped
apps.

`.github/workflows/build.yml` does the same on every push: it runs the tests on all three operating
systems, publishes each RID on its native runner, and uploads them as artifacts with the `.app`
built for both macOS RIDs. The CI bundles are not signed or zipped by `build-mac.sh`.

## Signing properly

The builds are ad-hoc signed (macOS) or unsigned (Windows), which is why other machines warn on the
first run; Build.md explains how users get past that. Removing the warnings needs paid identities:

- **macOS:** a Developer ID certificate, then notarisation:
  ```bash
  codesign --deep --force --options runtime --sign "Developer ID Application: ..." "Zero X.app"
  xcrun notarytool submit ZeroX.zip --keychain-profile ... --wait && xcrun stapler staple "Zero X.app"
  ```
- **Windows:** Authenticode signing with a code signing certificate.
- **Linux:** nothing to sign.

## No installer

There is no Windows installer. The published folder is the whole application, so unzipping it
somewhere is the install. If one is wanted later it would be written against the current layout --
one executable beside `roms/`, `programs/` and `palettes/` -- rather than revived from anything
older.
