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
- **PixInsight-Compliant Linked AutoSTF**: Implements PixInsight's standard linked Screen Transfer Function (Eq. 8.5.7) using joint channel median and MAD statistics to eliminate color casts on linear frames.
- **High-Performance I/O Engine**: Memory-mapped bulk reads and parallelized decoding for fast loading of multi-megapixel CCD/CMOS raw frames.
- **Standalone FastViewer**:
  - Auto-fits window dimensions on launch.
  - Multi-threaded background pre-fetching for instant **Next** (`>`) / **Previous** (`<`) folder browsing.
  - Interactive HUD stretch controls with live histogram.
  - Non-intrusive default setup: registers Explorer preview and thumbnail providers without hijacking your existing default file associations.

---

## Supported Formats

| Format | Extensions | Bit Depths / Data Types |
|---|---|---|
| **XISF** | `.xisf` | 8-bit UInt, 16-bit UInt, 32-bit Float (Planar & Interleaved, uncompressed) |
| **FITS** | `.fits`, `.fit`, `.fts` | 8-bit UInt, 16-bit Int/UInt (BZERO/BSCALE), 32-bit Int, -32 IEEE Float |

---

## Installation

1. Download the latest installer executable: `XisfFastViewerSetup.exe` from the [Releases](https://github.com/chakhdin/xisf-preview/releases) tab.
2. Run `XisfFastViewerSetup.exe` with administrative privileges.
3. Restart Windows Explorer or sign out and back in if thumbnails do not immediately refresh.

> **Note**: By default, the installer only registers the Shell Preview and Thumbnail handlers and adds FastViewer to the Windows "Open with..." menu. Your existing file associations (e.g., PixInsight, ASIFitsView) remain unchanged unless you explicitly check the optional association task during setup.

---

## Keyboard Shortcuts (FastViewer)

| Key | Action |
|---|---|
| `PageDown` / `Right Arrow` | Next astronomical image in folder |
| `PageUp` / `Left Arrow` | Previous astronomical image in folder |
| `Space` | Re-apply linked PixInsight AutoSTF |
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

### Compilation
```powershell
# 1. Build the Release binaries
dotnet build -c Release

# 2. Compile the installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss