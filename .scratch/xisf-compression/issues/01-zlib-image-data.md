# 01: zlib and zlib+sh image data

**What to build:** XISF images whose data block is compressed with `zlib` or `zlib+sh` get a preview and a thumbnail like uncompressed ones. Today `Core/XisfParser.cs` returns null for any image with a `compression` attribute (the check next to `geometry` and `location`). GitHub issue #1.

The attribute is `compression="codec:uncompressed-size[:item-size]"`, for example `compression="zlib+sh:<uncompressed size>:2"` on a UInt16 RGB Planar image stored at `location="attachment:<position>:<size>"`. `+sh` means the uncompressed bytes were byte-shuffled before compression with the given item size: all first bytes of every item, then all second bytes, and so on, with the trailing `uncompressed-size mod item-size` bytes stored unchanged. The XISF 1.0 specification (data block compression and byte shuffling) is the reference; if you cannot read it, write down in the issue comments which details you assumed.

Decode with `System.IO.Compression.DeflateStream` (net48, no new package): skip the 2-byte zlib header, inflate, then unshuffle when `+sh` is present. Put the codec parsing, inflating and unshuffling in `Core/`, in a form the later codecs (lz4, see 02) can share, and keep the existing flow (byte order, `ProcessRawBuffer`, STF) unchanged after the bytes are restored.

**Blocked by:** none

**Status:** ready-for-agent

- [ ] Tests build small zlib and zlib+sh XISF files in the test (UInt8, UInt16 and Float32; at least one Planar and one Interleaved; an attachment and an inline or embedded block) and check the decoded pixels against the uncompressed originals.
- [ ] The declared uncompressed size must equal width × height × channels × bytes per sample before anything is allocated, and the compressed size is checked against the file length as the attachment path does; a mismatch, an unknown codec, a bad item size or a truncated or corrupt stream gives null, never an exception. Tests cover each case, including a tiny file that declares a huge uncompressed size.
- [ ] Inflating never writes more than the declared uncompressed size (a stream that inflates further is rejected).
- [ ] Downscaled requests (thumbnails) on compressed data still work: the result has the same dimensions as for the uncompressed image at the same target size.
- [ ] Existing tests still pass; uncompressed files take the same path as before (the subsampled read for thumbnails is unchanged).
- [ ] Verified by hand on Windows: a zlib+sh XISF (for example saved from PixInsight with zlib + byte shuffling) shows in the Explorer preview pane, as a 256 px thumbnail and in the viewer.

## Comments
