# Lithophane Maker

A desktop app (C# / .NET 10 / Avalonia, runs on Linux and Windows) that turns images into
lithophane STL files, modelled after https://3dp.rocks/lithophane/.

## Run

```bash
dotnet run                 # from this folder
# or build a self-contained executable:
dotnet publish -c Release -r linux-x64 --self-contained   # or -r win-x64
```

## Features

- Add several images (button or drag & drop) — PNG, JPG, BMP, GIF, WEBP…; EXIF rotation respected
- Image settings: positive / negative, mirror, flip, repeat X/Y, contrast (gamma), rotate 90°
- Shapes: flat, outer curve, inner curve, cylinder (lamp), heart, dome
- Model settings: maximum size, thickness, thinnest layer, border, resolution (points/mm),
  curve angle, stand depth/height
- **Light support** (base that holds the lithophane and the light):
  - slot for the lithophane with adjustable depth and clearance
  - *LED strip / string*: a channel behind flat/curved lithophanes, or a tray inside
    cylinders, hearts and domes
  - *Lamp*: round recess or through-hole, with presets for tea light, LED puck,
    E14 / E27 sockets and M10 lamp tube, or any custom size
  - cable hole plus a groove under the base so it sits flat
- Export layouts: separate files (`*_lithophane.stl` + `*_light_support.stl`), one file with
  the parts side by side, or one file assembled (printed as a single piece)
- Interactive 3D preview (left-drag rotate, right-drag pan, wheel zoom) with a
  **backlit** mode that simulates light shining through the print
- Export a watertight binary STL for the selected image, or all images to a folder
- **Projects** (`.lipo`): New / Open / Save / Save as (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S).
  A project stores all settings and embeds the images, so it still opens if the originals move.
  Drop a `.lipo` onto the window or pass it on the command line to open it.
- The app reopens where you left off: the last project (including unsaved changes) is
  restored on the next start

The model stands upright (Z = up), which is the usual orientation for printing lithophanes.

## Layout

| Path | Purpose |
|---|---|
| `Core/GrayImage.cs` | Image loading → luminance |
| `Core/HeightMap.cs` | Luminance → thickness grid (border, repeat, mirror…) |
| `Core/Surfaces.cs` | Shape definitions (arc-length parameterised profiles, dome) |
| `Core/LithophaneGenerator.cs` | Builds the closed solid mesh (front, back, walls, stand) |
| `Core/LightSupportGenerator.cs` | Light support base, modelled as a signed distance field |
| `Core/SurfaceNets.cs` | Distance field → watertight mesh |
| `Core/StlWriter.cs` | Binary STL output |
| `Core/LithophaneProject.cs` | `.lipo` project file (JSON: settings + embedded images) |
| `Rendering/SoftwareRenderer.cs` | Multi-threaded z-buffer preview renderer |
| `Controls/PreviewControl.cs` | Interactive preview control |
| `MainWindow.axaml(.cs)` | UI |
