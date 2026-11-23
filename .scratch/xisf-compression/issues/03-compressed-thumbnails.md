# 03: Compressed embedded thumbnails

**What to build:** A `<Thumbnail>` element with a `compression` attribute in a supported codec (zlib, lz4, lz4hc, with or without `+sh`) is decoded and used for small requests, as uncompressed thumbnails already are. Today `Core/XisfParser.cs` skips a compressed thumbnail and falls back to the main image. A thumbnail in an unsupported codec (zstd or anything unknown) or one that fails to decode still falls back to the main image, never to "no preview". GitHub issue #1.

**Blocked by:** 01, 02

**Status:** needs-verification

- [ ] Tests build XISF files with a compressed thumbnail and a different main image and check that small requests return the thumbnail and large requests the main image.
- [ ] Tests check that a zstd thumbnail, an unknown codec and a corrupt compressed thumbnail fall back to the main image.
- [ ] Existing tests still pass.
- [ ] Verified by hand on Windows: an XISF with a compressed thumbnail shows its thumbnail in Explorer at 96 and 256 px.

## Comments

23/11/2026, closed:
Done. Core/XisfParser.cs: the per-element decoding (geometry, compression spec and size checks, attachment/inline/embedded read, decompress, byte order, ProcessRawBuffer, STF) moved unchanged into LoadImage(element, isThumbnail). For requests of 1..512 px LoadRawFrame now tries the first <Thumbnail> through TryLoadThumbnail, which returns null on any failure (including exceptions), and falls back to the main <Image> when it gets null. So compressed thumbnails in zlib, lz4 or lz4hc, with or without +sh, decode through the same XisfCompression code as main images. A zstd or unknown codec, a bad compression attribute, a corrupt, truncated or wrongly sized stream, an attachment past the end of the file or bad base64 all fall back to the main image, never to no preview. Decision: the fallback covers every thumbnail that fails to decode, uncompressed ones included (before this, a broken uncompressed thumbnail gave no preview). Requests of 0 or more than 512 px use the main image as before. Shell/ and UI/ are unchanged. Tests: Tests/XisfThumbnailTests.cs builds a 64x48 main image and a different 8x6 thumbnail (inline and as an attachment). Small requests (16, 96, 256, 512) return the thumbnail for all six codecs and an upper-case codec name, with the same pixels as the uncompressed thumbnail. Requests of 0, 513 and 1024 return the main image. Fallback cases: zstd, zstd+sh, an unknown codec, corrupt zlib, truncated zlib and lz4, a wrong declared size, item size 0, a missing size, a thumbnail past EOF, bad base64 and a truncated uncompressed thumbnail. Build is clean, and run-linux-tests.sh passes 338/338. Needs manual verification: (1) on Windows, an XISF with a compressed thumbnail (for example saved from PixInsight with zlib or lz4 compression and a thumbnail) shows its thumbnail in Explorer at 96 px and 256 px; (2) an XISF whose thumbnail is zstd-compressed still shows the main image as its thumbnail.
