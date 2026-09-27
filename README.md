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
- Interactive 3D preview (left-drag rotate, right-drag pan, wheel zoom) with a
  **backlit** mode that simulates light shining through the print
- Export a watertight binary STL for the selected image, or all images to a folder

The model stands upright (Z = up), which is the usual orientation for printing lithophanes.

## Layout

| Path | Purpose |
|---|---|
| `Core/GrayImage.cs` | Image loading → luminance |
| `Core/HeightMap.cs` | Luminance → thickness grid (border, repeat, mirror…) |
| `Core/Surfaces.cs` | Shape definitions (arc-length parameterised profiles, dome) |
| `Core/LithophaneGenerator.cs` | Builds the closed solid mesh (front, back, walls, stand) |
| `Core/StlWriter.cs` | Binary STL output |
| `Rendering/SoftwareRenderer.cs` | Multi-threaded z-buffer preview renderer |
| `Controls/PreviewControl.cs` | Interactive preview control |
| `MainWindow.axaml(.cs)` | UI |
