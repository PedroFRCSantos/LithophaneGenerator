# How it works

This document explains how an image becomes a printable STL file. It is aimed at contributors and anyone
curious about the geometry.

## Overview

```mermaid
flowchart LR
    A[Image file] -->|GrayImage| B[Luminance 0..1]
    B -->|HeightMap| C[Thickness grid in mm]
    C --> D{Shape}
    D -->|Surface| E[Back surface + normals]
    E -->|AddSlab| F[Closed lithophane mesh]
    S[Settings] -->|LightSupportGenerator| G[Signed distance field]
    G -->|SurfaceNets| H[Support mesh]
    F --> I[Arrange / Assemble]
    H --> I
    I -->|StlWriter| J[Binary STL]
    I -->|SoftwareRenderer| K[3D preview]
```

All geometry code lives in `Core/` and has no UI dependencies. The only external dependency is SkiaSharp,
which is used for image decoding.

## 1. Image → luminance (`GrayImage`)

The image is decoded with SkiaSharp into RGBA and converted to a single luminance channel in `[0, 1]`
(1 = white). Transparent pixels are blended onto white, and EXIF orientation is applied. `Sample(x, y)`
does bilinear interpolation so the mesh resolution does not depend on the image resolution.

## 2. Luminance → thickness (`HeightMap`)

The model is first built in a flat, *unrolled* space: `x` runs along the width (the arc length for curved
shapes) and `z` runs up. The grid size follows from **Maximum size** × **Resolution**. For the preview, the
resolution is scaled down so the grid stays under a node budget of 150 000 nodes.

For every node:

1. Nodes in the **border** get the full `Thickness`. Closed shapes have no side border, because the image
   wraps around.
2. Otherwise the image coordinate is computed, with mirror, flip and repeat (tiling) applied.
3. The luminance is sampled and raised to the **gamma** power.
4. `darkness = 1 − luminance` (or `luminance` in negative mode).
5. `thickness = MinThickness + darkness × (Thickness − MinThickness)`.

## 3. Shapes (`Surfaces`)

A `Surface` maps an unrolled point `(x, z)` to a 3D position on the model's back surface, plus the
direction in which the relief grows.

- **Flat, outer curve, inner curve, cylinder, heart** are `ProfileSurface`s: a 2D curve extruded
  vertically. Each curve is **reparameterised by arc length** and scaled so its length equals the unrolled
  width. The image is therefore never stretched, even on the uneven heart curve. Normals come from the
  curve's tangent.
- **Dome** is a `DomeSurface`: a spherical cap that is open at the top. The image width wraps around the
  rim and the image height climbs towards the pole.

## 4. Building the solid (`LithophaneGenerator.AddSlab`)

For every grid node the generator creates two vertices:

- a **back** vertex on the surface
- a **front** vertex offset along the normal by the node's thickness

Front and back faces are triangulated as quads. Side walls close the boundary; for closed shapes the grid
wraps in `u`, so only the top and bottom edges need walls. The result is a **closed, manifold solid**, and
the winding is flipped if the signed volume comes out negative. An optional stand is added as a second slab
under the bottom edge.

## 5. Light support (`LightSupportGenerator` + `SurfaceNets`)

The support has slots, channels, recesses, holes and grooves that follow arbitrary curves. Building that
directly from triangles would be fragile, so it is modelled as a **signed distance field (SDF)** instead:

- `ProfileField` gives, for any XY point, the signed offset from the footprint curve and the arc-length
  position of the nearest point. Open curves are extended straight past their ends.
- The base is built from CSG-style `min`/`max` combinations of simple distances: the outer body, minus the
  slot (thickness + clearance), minus the LED channel/tray or lamp recess/hole, minus the cable hole and
  the groove underneath.
- For closed shapes, the lamp is centred on the approximate **largest inscribed circle** of the
  footprint. The lamp diameter is reduced if it would not fit.
- The field is sampled on a regular voxel grid. The spacing is chosen so the grid stays within a cell
  budget: 500 000 cells for the preview and 4 000 000 for export.

`SurfaceNets` turns the field into a mesh (naive surface nets: one vertex per cell the surface crosses,
one quad per crossed grid edge). The outermost layer of samples is forced to "outside", which guarantees a
watertight result.

When settings conflict (for example, a base too low for the cable groove), the generator adjusts the
geometry and returns human-readable notes, which the UI shows with a ⚠.

## 6. Layout and export (`Arrange`, `StlWriter`)

`GenerateParts` returns the lithophane and the support in a shared coordinate frame, with the lithophane
standing in the slot. `Arrange` then produces the files for the chosen layout:

- **Separate files**: each part placed on the bed on its own (`_lithophane`, `_light_support` suffixes).
- **Side by side**: the support moved behind the lithophane with a 5 mm gap, in one mesh.
- **Assembled**: the parts as they fit together, in one mesh.

`PlaceOnBed` centres every mesh in X/Y and puts it on Z = 0. `StlWriter` writes **binary STL**: an 80-byte
header, a triangle count, and 50 bytes per triangle.

## 7. Preview (`SoftwareRenderer`, `PreviewControl`)

The preview does not use the GPU. It is a small **multi-threaded z-buffer rasteriser** that writes into a
`WriteableBitmap`, so it works the same on every platform and inside VMs.

A mesh is made of **parts** (`MeshPart`): the lithophane, the support (blue) and, in the preview only,
the light fixture. Each vertex carries its wall thickness and an *illumination* value.

- **Normal mode:** flat shading with a headlight slightly above and to the left of the camera. The
  fixture is drawn grey, tinted with the light colour where it glows.
- **Backlit mode** (`LightSimulation`: light colour, power, filament colour, all sRGB):
  1. Colours are converted to linear RGB. The filament colour sets a per-channel absorption
     `μ_c = μ_white − ln(f_c) / t_ref` (μ_white = 1.5 /mm for solid white PLA, t_ref = 1.5 mm).
  2. Each lithophane vertex gets `light × power × illumination × exp(−μ_c · t)`.
  3. Pixels interpolate these colours, add a tiny ambient term, and go through a tone-mapping +
     sRGB lookup table (`1 − exp(−3.5x)`, then gamma 1/2.2).

### The light fixture and illumination (`LightFixture`, `Illumination`)

`LightSupportGenerator` also returns a `LightFixture`: a small mesh of the light (built from
`Primitives`: swept strips and LED chips, cylinders for tea lights and pucks, a socket + neck + sphere
for bulbs) and a list of **emitter points** in the same frame as the lithophane. LEDs and pucks emit
upwards (Lambertian around +Z); bulbs emit in every direction.

`Illumination.Compute` then evaluates, for every back-surface node of the lithophane, an inverse-square
sum over the emitters with the emitter's and the receiver's cosine factors. A ray from an emitter must
clear the top of the wall between the slot and the light, which gives the shadow just above the base.
The result is normalised to the brightest node and compressed (`0.15 + 0.85·√x`) to stand in for light
bouncing around inside the base. Because this depends only on geometry, changing the light colour or
power is a re-render, not a regeneration.

`OrbitCamera` handles yaw, pitch, distance and pan, and frames the mesh from the front when a new model is
loaded. Preview generation runs on a background task, is debounced by 250 ms, and is cancelled when
settings change again.

## 8. Projects (`LithophaneProject`)

A `.lipo` file is indented JSON containing:

```jsonc
{
  "Version": 1,
  "Settings": { "Shape": "Cylinder", "MaxSize": 200, "Thickness": 3, ... },
  "Layout": "SeparateFiles",
  "LampPreset": 0,
  "Backlit": false,
  "SelectedImage": 0,
  "Images": [
    { "SourcePath": "/home/me/photo.jpg", "Data": "<base64 of the original file>", "Rotation": 1 }
  ]
}
```

Enums are stored as strings, so the files are readable and robust to reordering. Saving writes to a
temporary file first and then renames it, so a failed save never corrupts an existing project. Files with
a newer `Version` are rejected with a clear message.

Session restore uses the same format: on close, unsaved work is written to `session.lipo` in the user
configuration folder, together with a small `state.json` that records the last project path and whether
it had unsaved changes.

## 9. Localization (`Localization/Loc.cs`, `Assets/Lang`)

Every user-visible string is a key in a flat JSON table, one file per language (`en.json` is the reference
and the fallback). `Loc.Current` loads the English table plus the active one and exposes:

- `Loc.Get(key, args)` for code — formatted with the **OS culture** (`CultureInfo.CurrentCulture`), so
  numbers keep the system decimal separator whatever the UI language;
- `{l:T Key}` in XAML — a markup extension that returns a one-way `Binding` on `Loc.Language` with a
  converter that looks the key up, so a single `PropertyChanged` re-evaluates every bound text when the
  language changes;
- `Loc.Bind(key)` for controls built in code (the help tooltips).

The language is the OS UI language when supported, else English; the user's override is stored in
`state.json` (`SessionState.Language`) and applied in `App.OnFrameworkInitializationCompleted` before the
window is built. `MainWindow.ApplyLanguage()` handles what bindings cannot: `FlowDirection`
(right-to-left for `ar`/`ur`), the title, info and status lines, image sizes, and a re-selection of
every ComboBox because a closed ComboBox keeps a copy of the item text it was selected with (handlers are
muted by `_relocalizing` meanwhile). `PreviewControl` and `Sketch` override `BypassFlowDirectionPolicies`
so hand-drawn content is never mirrored. Warnings from `Core` are returned as `Note(Key, Value)` records
and formatted by the UI, which keeps `Core` free of UI strings.

Sketch labels come from `Sketch.*` keys; `Sketch.Text()` scales the font a little for dense scripts,
shrinks a label that would not fit and clamps it onto the canvas. Non-Latin scripts rely on per-glyph
font fallback to the system fonts.
