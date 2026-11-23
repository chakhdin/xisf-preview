# 02: lz4 and lz4hc image data

**What to build:** XISF images compressed with `lz4`, `lz4+sh`, `lz4hc` or `lz4hc+sh` get a preview and a thumbnail. lz4hc only changes how the data was compressed; both decode the same way. Write the LZ4 decoder in `Core/` (no NuGet package: the handlers run in-process in Explorer and the preview host, where extra dependencies risk load failures). Reuse the codec parsing, size checks and unshuffling from 01. GitHub issue #1.

First check in the XISF 1.0 specification whether lz4 data is the raw LZ4 block format or the LZ4 frame format, and note what you found in the issue comments; decode the format the spec names.

**Blocked by:** 01

**Status:** needs-verification

- [ ] Tests decode LZ4 data with known output, including literal-only data, overlapping matches (offset smaller than match length) and long literal and match lengths (length bytes of 255). Test data is built in the test: a small LZ4 encoder in the test project or hand-written byte arrays, documented where they come from.
- [ ] Tests build small lz4 and lz4+sh XISF files (UInt16 and Float32) and check the decoded pixels against the uncompressed originals.
- [ ] Corrupt input never reads or writes outside the buffers: an offset before the start of the output, a length past the end of the input or output, or output shorter or longer than the declared uncompressed size gives null, never an exception escaping the parser. Tests cover each case.
- [ ] Existing tests still pass.
- [ ] Verified by hand on Windows: an lz4 or lz4hc XISF (saved from PixInsight with that codec) shows in the Explorer preview pane, as a 256 px thumbnail and in the viewer.

## Comments

23/11/2026, comment:
Spec check: the XISF 1.0 specification (pixinsight.com/doc/docs/XISF-1.0-spec, section 2.2 Changes in Revision 1, Corrections) says 'LZ4 and LZ4_HC data are stored in the LZ4 block format' (Zstandard uses one frame per subblock). It also says subblocks are compressed independently and stored contiguously, and byte shuffling applies to the whole block before it is split. So lz4 and lz4hc are decoded as raw LZ4 blocks, not LZ4 frames. I could not read the full codec sections (10.6.6-10.6.9) because the page was truncated.

23/11/2026, closed:
Done. Core/XisfCompression.cs: IsSupportedCodec accepts lz4 and lz4hc, with or without +sh. Both go to one new DecodeLz4Block (raw LZ4 block format, as the spec comment above says). Codec parsing, the geometry size check, the attachment bounds check and Unshuffle are reused from 01 without changes. Before allocating, the declared size is capped at 255 x the compressed length + 64, which is the most an LZ4 block can expand. The decoder checks every literal length against the remaining input and output, rejects offset 0 and any offset before the start of the output, checks every match length against the remaining output and checks that the input does not end inside a length extension or an offset. The output must be exactly the declared size. It copies overlapping matches byte by byte. It does not enforce the end-of-block rules (last 5 bytes literal), so blocks from other encoders are accepted as long as they stay in bounds. Subblocks are still not supported (same as 01). No NuGet package was added, and Shell/ and UI/ are unchanged. Tests: Tests/XisfLz4Tests.cs has a small greedy LZ4 encoder written from the LZ4 block format description and hand-written blocks with their decoding documented inline. They cover literal-only blocks, overlapping matches (offset 2 and offset 1), long literal and match lengths with 255 bytes, a nibble of 15 with a 0 extension, UInt16 and Float32 lz4, lz4+sh, lz4hc and lz4hc+sh attachments, inline lz4+sh and downscaling. Corrupt-input tests cover an offset before the output start, offset 0, a literal length past the input or output end, a match length past the output end, a truncated offset or length, output shorter or longer than declared, empty input, 2000 random garbage blocks (none throw), a corrupt file and a huge declared size. Build is clean, and run-linux-tests.sh passes 314/314. Needs manual verification: (1) an lz4 or lz4hc XISF saved from PixInsight with that codec shows in the Explorer preview pane; (2) the same file shows as a 256 px thumbnail; (3) it opens in the viewer.
