<p align="center">
  <img src="docs/images/logo.png" alt="Lithophane Maker logo" width="128" height="128">
</p>

<h1 align="center">Lithophane Maker</h1>

A cross-platform desktop app that turns photos into **3D-printable lithophanes**. It exports watertight
STL files for flat panels, curved panels, cylinder lamps, hearts and domes. It can also generate a matching **light support**
base for an LED strip, a tea light or a lamp socket.

Built with C# / .NET 10 and [Avalonia UI](https://avaloniaui.net/). It runs on **Linux and Windows**, needs no
internet connection, and was inspired by [3dp.rocks/lithophane](https://3dp.rocks/lithophane/).

![Main window: image list and settings on the left, interactive 3D preview on the right](docs/images/main-window.png)

---

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Getting started](#getting-started)
- [Quick start](#quick-start)
- [Documentation](#documentation)
- [Project structure](#project-structure)
- [License](#license)

## Features

- **Many images at once**: add PNG, JPG, BMP, GIF, WEBP… with the button or by drag & drop. EXIF
  rotation is respected.
- **Image controls**: positive / negative, mirror, flip, repeat (tile) in X and Y, contrast (gamma), and
  90° rotation.
- **Six shapes**: flat, outer curve, inner curve, cylinder (lamp), heart and dome.
- **Model controls**: maximum size, maximum and minimum thickness, border, mesh resolution, curve angle,
  and an optional stand.
- **Light support base**, generated to fit the lithophane:
  - a slot the lithophane slides into, with adjustable depth and clearance
  - an **LED strip / string** channel behind flat and curved panels, or a tray inside cylinders,
    hearts and domes
  - a **lamp** recess or through-hole, with presets for tea lights, LED pucks, E14 / E27 sockets and M10
    lamp tubes (or any custom size)
  - a cable hole plus a groove under the base, so the base still sits flat with the cable in place
- **Three export layouts**: separate files, one file with the parts side by side, or one file with the
  lithophane standing in its support (printed as a single piece).
- **Interactive 3D preview** (rotate, pan, zoom) that also draws the **light source** inside the
  support (LED strip, tea light, puck or bulb).
- **Backlit simulator**: pick the **light colour** (warm/cool white, colours or custom), its **power**
  and the **filament colour**; the preview computes how much light reaches each point of the wall
  from the actual light position and how much gets through the plastic (100 % infill).
- **Built-in help**: hover over any setting to get a short explanation with a sketch of what the
  parameter means.
- **10 languages**: English, 中文, हिन्दी, Español, العربية, Français, বাংলা, Português, Русский, اردو —
  the interface, messages and help sketches follow your system language, with a switcher in the
  toolbar (Arabic and Urdu lay the window out right-to-left).
- **Watertight binary STL** export for the selected image, or for every image into a folder.
- **Projects (`.lipo`)** store all settings and **embed the images**, so a project still opens after the
  original files are moved.
- **Session restore**: the app reopens where you left off, including unsaved changes.

## Screenshots

### Backlit simulator

Turn on **Backlit view** to see how the print will look with a light behind it. Thin areas glow and
thick areas stay dark. Choose the light colour and power and the filament colour to match what you
will actually print and use.

![Backlit preview of a flat lithophane](docs/images/backlit.png)

![Light simulation: LED strip behind a panel, cool white at 250 %, red filament, blue light](docs/images/light-simulation.png)

*Top: an LED strip in a base behind a flat panel; cool white light at 250 %. Bottom: red filament with
a warm white tea light; white filament with a blue light at 200 %.*

When a light support is enabled, the preview also shows the light itself, and the simulation uses its
real position, so you can see how evenly an LED strip or a bulb lights the print.

![Light fixtures: tea light, E27 bulb, LED strip, and a tea light inside a cylinder](docs/images/light-fixtures.png)

### Shapes

| Flat (with stand) | Outer curve | Inner curve |
|:---:|:---:|:---:|
| ![Flat](docs/images/shape-flat.png) | ![Outer curve](docs/images/shape-outer-curve.png) | ![Inner curve](docs/images/shape-inner-curve.png) |
| **Cylinder (lamp)** | **Heart** | **Dome** |
| ![Cylinder](docs/images/shape-cylinder.png) | ![Heart](docs/images/shape-heart.png) | ![Dome](docs/images/shape-dome.png) |

### Light supports

| LED strip channel behind a curved panel | Tea-light base under a cylinder lamp |
|:---:|:---:|
| ![LED strip support](docs/images/support-led.png) | ![Lamp support](docs/images/support-lamp.png) |

The support is drawn in blue in the preview. See the [user guide](docs/USER_GUIDE.md#light-support) for
every option.

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (only needed to build; a self-contained build runs
  without it)
- Linux (X11 or Wayland via XWayland) or Windows 10/11

### Run from source

```bash
git clone <this-repository-url>
cd LithophaneGUI
dotnet run -c Release
```

### Build a standalone executable

```bash
# Linux
dotnet publish -c Release -r linux-x64 --self-contained -o publish/linux

# Windows
dotnet publish -c Release -r win-x64 --self-contained -o publish/windows
```

Run `publish/linux/LithophaneGUI` or `publish\windows\LithophaneGUI.exe`.

### Install on Linux (application menu + `.lipo` file type)

```bash
dotnet publish -c Release -r linux-x64 --self-contained -o publish/linux
packaging/linux/install.sh publish/linux
```

This installs the app for the current user, without root. It adds:

- the app files in `~/.local/share/lithophane-maker`
- a `lithophane-maker` command in `~/.local/bin`
- a **Lithophane Maker** entry with its icon in the application menu
- the `.lipo` file type, so double-clicking a project opens it

To remove all of this, run `packaging/linux/install.sh --uninstall`.

### Languages and fonts

The app starts in your system language if it is one of the ten supported ones, otherwise in English;
the combo box at the top right of the preview switches languages at any time and remembers your
choice. Numbers keep the decimal separator of your system. Scripts other than Latin and Cyrillic
(Chinese, Devanagari, Bengali, Arabic) use the system's fonts, so on a minimal Linux install add
`fonts-noto` and `fonts-noto-cjk` (or your distribution's equivalent) if you see empty boxes instead
of letters. Translations live in `Assets/Lang/*.json`; corrections are welcome.

![Lithophane Maker in Portuguese, Chinese, Arabic (right-to-left) and Hindi](docs/images/languages.png)

![Help tooltips in Chinese, Hindi, Arabic and Urdu](docs/images/help-tooltip-languages.png)

### Command line

```bash
LithophaneGUI photo1.jpg photo2.png   # start with these images
LithophaneGUI my-lamp.lipo            # open a project
```

## Quick start

1. Click **Add images…** or drag photos onto the window.
2. Pick a **Shape** under *Model settings* and set the **Maximum size** (the longest side of the image,
   in mm).
3. Check the result in the 3D preview. Turn on **Backlit view** to judge the contrast.
4. Optional: choose a **Light source** under *Light support* to generate a base.
5. Click **Export STL…** and slice the file. Print it upright at 100% infill in white or natural PLA.
6. Press **Ctrl+S** to save the project as a `.lipo` file so you can come back to it later.

The [user guide](docs/USER_GUIDE.md) explains every setting and includes printing tips.

## Documentation

| Document | What's inside |
|---|---|
| [User guide](docs/USER_GUIDE.md) | Every setting, projects, export layouts, printing tips, troubleshooting |
| [How it works](docs/ARCHITECTURE.md) | Generation pipeline, mesh construction, light support modelling, preview renderer |

## Project structure

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
| `Controls/ParameterHelp.cs` | Help tooltips: topics and the code-drawn sketches |
| `Localization/` | `Loc` (string tables, language detection, live switching) and the `{l:T Key}` XAML extension |
| `Assets/Lang/*.json` | One flat key → text table per language (`en.json` is the reference and fallback) |
| `MainWindow.axaml(.cs)` | UI |
| `Assets/` | Logo (`logo.svg`, plus `logo-small.svg` tuned for 16–32 px) and the app icon (`icon.ico`) |
| `packaging/linux/` | `.desktop` entry, `.lipo` MIME type, icons and `install.sh` for Linux |

The model stands upright (Z = up), which is the usual orientation for printing lithophanes.

## License

Released under the [MIT License](LICENSE).
