# 02: lz4 and lz4hc image data

**What to build:** XISF images compressed with `lz4`, `lz4+sh`, `lz4hc` or `lz4hc+sh` get a preview and a thumbnail. lz4hc only changes how the data was compressed; both decode the same way. Write the LZ4 decoder in `Core/` (no NuGet package: the handlers run in-process in Explorer and the preview host, where extra dependencies risk load failures). Reuse the codec parsing, size checks and unshuffling from 01. GitHub issue #1.

First check in the XISF 1.0 specification whether lz4 data is the raw LZ4 block format or the LZ4 frame format, and note what you found in the issue comments; decode the format the spec names.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Tests decode LZ4 data with known output, including literal-only data, overlapping matches (offset smaller than match length) and long literal and match lengths (length bytes of 255). Test data is built in the test: a small LZ4 encoder in the test project or hand-written byte arrays, documented where they come from.
- [ ] Tests build small lz4 and lz4+sh XISF files (UInt16 and Float32) and check the decoded pixels against the uncompressed originals.
- [ ] Corrupt input never reads or writes outside the buffers: an offset before the start of the output, a length past the end of the input or output, or output shorter or longer than the declared uncompressed size gives null, never an exception escaping the parser. Tests cover each case.
- [ ] Existing tests still pass.
- [ ] Verified by hand on Windows: an lz4 or lz4hc XISF (saved from PixInsight with that codec) shows in the Explorer preview pane, as a 256 px thumbnail and in the viewer.

## Comments
