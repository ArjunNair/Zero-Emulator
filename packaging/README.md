# Packaging

Everything is a self-contained single-file `dotnet publish`; nothing needs installing on the target.

```bash
dotnet publish src/Zero.App/Zero.App.csproj -c Release -r <rid> --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish/<rid>
```

RIDs: `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`, `linux-x64`, `linux-arm64`. The `roms/` and
`programs/` folders are copied next to the executable and must stay there.

- **macOS:** `packaging/macos/make-app.sh publish/osx-arm64 Zero.app` builds the bundle. Sign and
  notarise with a Developer ID before distributing, otherwise Gatekeeper blocks it (see the script).
- **Linux:** `packaging/linux/zero.desktop` is a starting point for an AppImage/Flatpak; SDL3 and
  Avalonia (X11/Wayland via XWayland) need no extra packages on mainstream distros.
- **Windows:** the single `Zero.exe` runs as is. The old Inno Setup script in `InnoScript/` predates
  the port and would need updating for the new file layout.

`.github/workflows/build.yml` runs the tests on all three OSes and publishes all six RIDs as artifacts.
