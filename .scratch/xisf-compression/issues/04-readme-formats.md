# 04: README format tables

**What to build:** The supported-formats table and the feature list in `README.md`, `README.it.md` and `README.ru.md` say that XISF image data and thumbnails may be uncompressed or compressed with zlib, lz4 or lz4hc (with or without byte shuffling), and that zstd is not supported yet. Today they say "uncompressed" only (for example the XISF row of the table and the Preview Pane bullet "full uncompressed images", which is about XISF data, so reword it). Keep each file's language, style and existing wording. GitHub issue #1.

**Blocked by:** 01, 02, 03

**Status:** ready-for-agent

- [ ] The three READMEs list the same codecs, matching what the parser decodes after 01–03.
- [ ] zstd is named as not supported in all three.
- [ ] No other README content changes.

## Comments
