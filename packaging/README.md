# Packaging

Everything is a self-contained single-file `dotnet publish`: the output folder runs on a machine with
nothing installed on it.

## Prerequisites

The **.NET 10 SDK**, and nothing else. `global.json` pins it to 10.0.100 with `rollForward:
latestFeature`, so any 10.0.x SDK will do. Check with `dotnet --version`.

Nothing else is needed because:

- **No C or C++ toolchain.** The build is plain IL with a single-file bundle, not NativeAOT, so there
  is no linker or platform SDK involved. Publishing for Windows from a Mac does not need Visual
  Studio or the Windows SDK.
- **No system SDL3 or Skia.** Both arrive as NuGet packages carrying prebuilt native libraries for
  every RID (`ppy.SDL3-CS` and `SkiaSharp`), and the publish step picks the ones for the target and
  bundles them into the executable. There is nothing to `apt install` or `brew install`.
- **No ICU.** The app builds with `InvariantGlobalization`, so it does not want the system
  globalisation libraries at runtime.

On the machine that *runs* the build, the system still supplies the window system and the audio
server: X11 or Wayland and ALSA/PulseAudio/PipeWire on Linux, both already present on mainstream
desktops; nothing at all on Windows or macOS. Building the macOS `.app` icon uses `sips` and
`iconutil`, which ship with macOS — the script skips the icon rather than failing if they are absent.

## Just for your own machine

Pick the RID for the machine you are on and publish that one. On Apple Silicon:

```bash
dotnet publish src/Zero.App/Zero.App.csproj -c Release -r osx-arm64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o publish/osx-arm64

packaging/macos/make-app.sh publish/osx-arm64 "Zero X.app"      # optional: a double-clickable bundle
```

Swap the RID for your own: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`,
`linux-arm64`. `dotnet --info` prints the host's RID if you are unsure.

What you get is a folder of about 113 MB holding the executable, `roms/` and `programs/`. The two
folders must stay beside the executable.

- **Windows:** run `Zero.exe`. Delete the stray `*.pdb` files if you care; `-p:DebugType=none` does
  not strip the ones belonging to the native libraries.
- **macOS:** run the executable directly, or build the `.app` above for something you can keep in the
  Dock.
- **Linux:** `chmod +x Zero && ./Zero`. `packaging/linux/zero.desktop` is a starting point for a
  menu entry, an AppImage or a Flatpak.

If you only want to run it and not keep the build, `dotnet run --project src/Zero.App` is quicker and
needs no publish at all.

## All six at once

Every RID cross-publishes from any host — a Windows executable built on a Mac is a normal thing to
do here, because nothing in the build is native to the host.

```bash
for rid in win-x64 win-arm64 osx-x64 osx-arm64 linux-x64 linux-arm64; do
  dotnet publish src/Zero.App/Zero.App.csproj -c Release -r $rid --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o publish/$rid
done
rm -f publish/win-*/*.pdb
packaging/macos/make-app.sh publish/osx-arm64 "publish/Zero X-osx-arm64.app"
packaging/macos/make-app.sh publish/osx-x64   "publish/Zero X-osx-x64.app"
```

`.github/workflows/build.yml` already does all of this: it runs the tests on all three operating
systems, publishes each RID on its native runner, and uploads them as artifacts with the `.app`
built for both macOS RIDs.

## Unsigned builds

None of the above is code signed, which is fine for your own machine and a nuisance on anyone
else's.

- **macOS:** Gatekeeper refuses an unsigned bundle on a machine that did not build it. The first run
  has to be right-click → Open → Open. Signing and notarising properly needs a paid Apple Developer
  identity:
  ```bash
  codesign --deep --force --options runtime --sign "Developer ID Application: ..." "Zero X.app"
  xcrun notarytool submit ZeroX.zip --keychain-profile ... --wait && xcrun stapler staple "Zero X.app"
  ```
- **Windows:** SmartScreen shows an "unrecognised app" warning; More info → Run anyway. Authenticode
  signing needs a code signing certificate.
- **Linux:** nothing to sign.

The old Inno Setup script in `InnoScript/` predates the port and would need updating for the current
file layout before it could build an installer again.
