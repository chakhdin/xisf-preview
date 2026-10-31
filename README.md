<p align="right">
  <b>English</b> | 
  <a href="README.it.md">Italiano</a> | 
  <a href="README.ru.md">Русский</a>
</p>

# XISF & FITS FastViewer & Shell Extensions

A high-performance Windows Shell extension and lightweight viewer for astronomical image formats: **XISF** (PixInsight Extensible Image Serialization Format) and **FITS** (`.fits`, `.fit`, `.fts`).

---

## Features

- **Windows Explorer Thumbnails**: Native `IThumbnailProvider` shell extension providing fast, high-quality thumbnails directly in File Explorer.
- **Windows Preview Pane**: Native `IPreviewHandler` integration to view full uncompressed images in the Explorer preview pane (`Alt + P`).
- **PixInsight-Compliant AutoSTF**: Implements PixInsight's Screen Transfer Function auto-stretch (spec section 8.5.7) using median and MAD statistics. RGB images are stretched per channel (unlinked), which neutralizes color casts on linear frames; the viewer's histogram panel shows the R/G/B values ("Auto (per channel)") and switches to a linked stretch as soon as you drag a slider.
- **High-Performance I/O Engine**: Sequential bulk reads straight into memory (thumbnails seek and read only the rows they need) and parallelized decoding for fast loading of multi-megapixel CCD/CMOS raw frames.
- **Standalone FastViewer**:
  - Auto-fits window dimensions on launch.
  - Multi-threaded background pre-fetching for instant **Next** (`>`) / **Previous** (`<`) folder browsing.
  - Interactive HUD stretch controls with live histogram.
  - Non-intrusive setup: registers only the Explorer preview and thumbnail providers — never touches your existing default file associations.

---

## Supported Formats

| Format | Extensions | Bit Depths / Data Types |
|---|---|---|
| **XISF** | `.xisf` | 8-bit UInt, 16-bit UInt, 32-bit Float (Planar & Interleaved, uncompressed) |
| **FITS** | `.fits`, `.fit`, `.fts` | 8-bit UInt, 16-bit Int/UInt (BZERO/BSCALE), 32-bit Int, -32 IEEE Float |
| **FITS (tile-compressed)** | `.fits`, `.fit`, `.fts` | RICE_1, 32-bit integer, one row per tile, lossless (e.g. HOPS output) |

Tile-compressed FITS frames above 16 megapixels are not decoded: no preview or thumbnail is produced for them.

---

## Installation

1. Download the latest installer executable: `XisfFastViewerSetup.exe` from the [Releases](https://github.com/chakhdin/xisf-preview/releases) tab.
2. Run `XisfFastViewerSetup.exe` with administrative privileges.
3. Restart Windows Explorer or sign out and back in if thumbnails do not immediately refresh.

> **Note**: The installer only registers the Explorer Preview and Thumbnail handlers. It does not add FastViewer to the "Open with..." menu and does not touch which program opens `.xisf`/`.fits`/`.fit`/`.fts` files by default — your existing file associations (e.g., PixInsight, ASIFitsView) are left completely untouched.

---

## Keyboard Shortcuts (FastViewer)

| Key | Action |
|---|---|
| `PageDown` / `Right Arrow` | Next astronomical image in folder |
| `PageUp` / `Left Arrow` | Previous astronomical image in folder |
| `Space` | Re-apply PixInsight AutoSTF (per channel on RGB) |
| `F` | Fit image to window |
| `H` | Toggle Histogram / STF HUD panel |
| `Mouse Wheel` | Smooth zoom centered on cursor |
| `Left Click + Drag` | Pan across image |
| `Esc` | Close viewer |

---

## Building from Source

### Prerequisites
- [.NET Framework 4.8 Developer Pack / SDK](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- [Inno Setup 6](https://jrsoftware.org/isdl.php)
- A strong-name key file `key.snk` in the repository root. It is gitignored, so a fresh clone does not contain it and the build fails with a missing-key error. Generate your own with `sn -k key.snk` from a Developer Command Prompt (the .NET Framework SDK tool); the key only gives the assembly a strong name and is not tied to anything else in the repository.

### Compilation
```powershell
# 1. Build the Release binaries
dotnet build -c Release

# 2. Compile the installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss

# 3. SHA-256 of the installer (publish it in the release notes)
Get-FileHash bin\Installer\XisfFastViewerSetup.exe -Algorithm SHA256