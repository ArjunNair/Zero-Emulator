# Building Zero

Zero can be built on Mac, Windows and Linux platforms in a few easy steps. None of it needs a paid developer account or a code signing certificate.

If you only want to run the emulator from source, skip all of this:

```bash
dotnet run --project src/Zero.App
```

## Prerequisites

| | Needed |
|---|---|
| All systems | [.NET 10 SDK](https://dotnet.microsoft.com/download) (any 10.0.x), and git to fetch the source |
| macOS | macOS 11 or later. `codesign`, `ditto`, `sips` and `iconutil` ship with macOS; if `codesign` is missing, run `xcode-select --install` |
| Windows | Windows 10 or later, PowerShell |
| Linux | A desktop with X11 or Wayland and ALSA, PulseAudio or PipeWire |

Check the SDK with:

```bash
dotnet --version
```

There is no C or C++ toolchain to install, and no SDL or Skia: both arrive as NuGet packages with
their native libraries included. The first build needs an internet connection to download them.

Every build below is *self-contained*: the output carries its own .NET runtime, so the machine that
runs it needs nothing installed.

## macOS

### With the script

From the repository root:

```bash
packaging/macos/build-mac.sh
```

It builds for the Mac you are on. Pass `arm64` (Apple Silicon), `x64` (Intel) or `both` to choose.
You get:

- `publish/macos-<arch>/Zero X.app` — the app, ready to run or drag into Applications
- `publish/Zero-X-<version>-macos-<arch>.zip` — the same app zipped, ready to share

The version comes from `<Version>` in `Directory.Build.props`.

### Without the script

The same five steps by hand, for Apple Silicon. For Intel, replace `arm64` with `x64` throughout.

1. Publish:

   ```bash
   dotnet publish src/Zero.App/Zero.App.csproj -c Release -r osx-arm64 --self-contained \
     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
     -o publish/osx-arm64
   ```

   `publish/osx-arm64/Zero` already runs from a terminal at this point. The remaining steps make it
   a double-clickable app.

2. Create the bundle and copy the build and the `Info.plist` into it:

   ```bash
   mkdir -p "publish/Zero X.app/Contents/MacOS" "publish/Zero X.app/Contents/Resources"
   cp -R publish/osx-arm64/. "publish/Zero X.app/Contents/MacOS/"
   cp packaging/macos/Info.plist "publish/Zero X.app/Contents/Info.plist"
   chmod +x "publish/Zero X.app/Contents/MacOS/Zero"
   ```

   `roms/`, `programs/` and `palettes/` must stay beside the `Zero` executable, which is why the
   whole folder goes into `Contents/MacOS`.

3. Optional: set the version shown in Finder (replace `1.0.0`):

   ```bash
   /usr/libexec/PlistBuddy -c "Set :CFBundleVersion 1.0.0" -c "Set :CFBundleShortVersionString 1.0.0" "publish/Zero X.app/Contents/Info.plist"
   ```

   The app icon is optional too. `packaging/macos/make-app.sh publish/osx-arm64 "publish/Zero X.app"`
   does step 2 and builds the icon, if you would rather not do those by hand.

4. Ad-hoc sign it. The `-` means "no identity", so no certificate is involved:

   ```bash
   codesign --force --deep --sign - "publish/Zero X.app"
   ```

   Apple Silicon refuses to run code with no signature at all; an ad-hoc one is enough.

5. Zip it for sharing. Use `ditto` rather than `zip`, which can damage the signature:

   ```bash
   ditto -c -k --keepParent "publish/Zero X.app" publish/Zero-X-macos-arm64.zip
   ```

### Running it on another Mac

The app is signed but not notarised, so a Mac that downloaded it will refuse to open it the first
time. Either:

- Try to open it once, then go to **System Settings → Privacy & Security** and click
  **Open Anyway**, or
- Clear the download flag in Terminal:

  ```bash
  xattr -dr com.apple.quarantine "/Applications/Zero X.app"
  ```

The Mac that built the app is not affected. Removing the prompt for everyone needs a Developer ID
certificate and notarisation, which requires a paid Apple Developer membership.

## Windows

In PowerShell, from the repository root:

```powershell
dotnet publish src/Zero.App/Zero.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish/win-x64
```

Use `win-arm64` instead of `win-x64` for Windows on ARM.

Run `publish\win-x64\Zero.exe`. The folder is the whole application: `roms`, `programs` and
`palettes` must stay beside `Zero.exe`. A few `.pdb` files from the native libraries end up in the
folder as well; they are safe to delete:

```powershell
Remove-Item publish/win-x64/*.pdb
```

To share it, zip the folder:

```powershell
Compress-Archive -Path publish/win-x64/* -DestinationPath publish/Zero-X-win-x64.zip
```

There is no installer; unzipping is the install. The build is unsigned, so on another PC
SmartScreen shows "Windows protected your PC" on the first run. Click **More info → Run anyway**.

## Linux

From the repository root:

```bash
dotnet publish src/Zero.App/Zero.App.csproj -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
  -o publish/linux-x64
```

Use `linux-arm64` instead of `linux-x64` for ARM machines such as a Raspberry Pi.

Run it:

```bash
chmod +x publish/linux-x64/Zero && publish/linux-x64/Zero
```

`roms`, `programs` and `palettes` must stay beside the `Zero` executable. To share it, use a tarball
so the executable bit survives:

```bash
tar -czf publish/Zero-X-linux-x64.tar.gz -C publish/linux-x64 .
```

Nothing needs signing on Linux. For a menu entry, copy `packaging/linux/zero.desktop` to
`~/.local/share/applications/` and change its `Exec=` line to the full path of the `Zero`
executable.

## Building for another system

Nothing in the build is tied to the machine doing it, so any of the `dotnet publish` commands above
works from any of the three systems: a Windows or Linux build can be made on a Mac, and so on. Two
things do need the target system:

- The macOS signing and zipping steps (`codesign`, `ditto`) only exist on macOS.
- A Linux or macOS build zipped on Windows loses its executable bit; run `chmod +x Zero` after
  unpacking.

`.github/workflows/build.yml` builds all six variants on every push.
