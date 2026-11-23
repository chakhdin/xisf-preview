# 05: compressed data split into subblocks

**What to build:** compressed XISF data blocks that carry a `subblocks` attribute decode, for images and thumbnails, with every supported codec (zlib, lz4, lz4hc, with or without +sh). PixInsight 1.9.6 writes it on ordinary files: a 1365x1024x4 UInt8 image saved with zlib has `compression="zlib:5591040" subblocks="137311,4194304:1376,1396736"`, so its data is split into 4 MiB pieces. Today the attribute is ignored, the whole block goes to the decoder as one stream, decoding fails and the preview pane and viewer show nothing (thumbnails survive only because PixInsight's small thumbnails have no subblocks). GitHub issue #1.

What the XISF 1.0 specification says (section 10.6, read 23/11/2026; quoted):
- Syntax: `subblocks="c1,u1:c2,u2:...:cN,uN"`, the compressed and uncompressed lengths in bytes of each subblock; no limit on the number of subblocks.
- "Each subblock shall be compressed independently, unless it is stored as an uncompressed subblock, and the subblocks shall be stored contiguously, in the order given by the subblocks attribute." So each zlib subblock is a full zlib stream with its own header and Adler-32, each lz4 subblock its own LZ4 block.
- "For compression codecs with byte shuffling, the byte shuffling transform shall be applied to the entire data block before its division into subblocks, and the reverse transform after all subblocks have been decompressed and concatenated." Unshuffle once, on the joined data.
- Uncompressed subblocks: a subblock with ci = ui "stores its ui bytes unmodified" (for +sh codecs, bytes of the shuffled block). A decoder "shall verify that each compressed subblock decompresses to exactly ui bytes, and shall treat the data block as invalid otherwise."
- The spec does not state that the sums must match, but the decoder requires it: the ui sum must equal the declared uncompressed size and the ci sum must equal the stored block size; otherwise null.

**Blocked by:** 01, 02, 03

**Status:** ready-for-agent

- [ ] One implementation in `Core/XisfCompression.cs` serves images and thumbnails: parse `subblocks`, check the sums before allocating, decompress each subblock into its slice of the output buffer with the existing zlib and LZ4 decoders (each subblock checked to give exactly ui bytes), copy subblocks with ci = ui as they are, then unshuffle once. Without the attribute the path is exactly as today.
- [ ] The allocation cap still holds: nothing big is allocated when the declared sizes are out of proportion to the stored bytes (per subblock, the codec's expansion bound applies to ci).
- [ ] Attachment, inline and embedded blocks all honour `subblocks` (for embedded blocks, read it from the same element as `compression`).
- [ ] Tests build XISF files with 2 and 3 subblocks for zlib, zlib+sh, lz4 and lz4hc+sh (an item size of 2 or 4, with subblock boundaries that do not fall on item boundaries), one file mixing a compressed and an uncompressed subblock, and a compressed thumbnail with subblocks; decoded pixels match the uncompressed originals, at full size and downscaled.
- [ ] Bad input gives null (for thumbnails: falls back to the main image as in 03), never an exception: malformed attribute (missing comma, empty pair, non-numeric, zero pairs), sums that do not match, a subblock decompressing to more or fewer bytes than ui, a subblock running past the end of the block, a corrupt subblock. Tests cover each case.
- [ ] Existing tests still pass.
- [ ] Verified by hand on Windows: PixInsight 1.9.6 files `c:\Download\Image011.xisf` (zlib), `Image012.xisf` (lz4), `Image013.xisf` (lz4hc) show in the Explorer preview pane, as thumbnails and in the viewer; `Image01.xisf` (zstd) still gives the generic icon.

## Comments
