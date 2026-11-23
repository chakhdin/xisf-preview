# 03: Compressed embedded thumbnails

**What to build:** A `<Thumbnail>` element with a `compression` attribute in a supported codec (zlib, lz4, lz4hc, with or without `+sh`) is decoded and used for small requests, as uncompressed thumbnails already are. Today `Core/XisfParser.cs` skips a compressed thumbnail and falls back to the main image. A thumbnail in an unsupported codec (zstd or anything unknown) or one that fails to decode still falls back to the main image, never to "no preview". GitHub issue #1.

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [ ] Tests build XISF files with a compressed thumbnail and a different main image and check that small requests return the thumbnail and large requests the main image.
- [ ] Tests check that a zstd thumbnail, an unknown codec and a corrupt compressed thumbnail fall back to the main image.
- [ ] Existing tests still pass.
- [ ] Verified by hand on Windows: an XISF with a compressed thumbnail shows its thumbnail in Explorer at 96 and 256 px.

## Comments
