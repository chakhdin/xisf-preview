# 04: README format tables

**What to build:** The supported-formats table and the feature list in `README.md`, `README.it.md` and `README.ru.md` say that XISF image data and thumbnails may be uncompressed or compressed with zlib, lz4 or lz4hc (with or without byte shuffling), and that zstd is not supported yet. Today they say "uncompressed" only (for example the XISF row of the table and the Preview Pane bullet "full uncompressed images", which is about XISF data, so reword it). Keep each file's language, style and existing wording. GitHub issue #1.

**Blocked by:** 01, 02, 03

**Status:** done

- [ ] The three READMEs list the same codecs, matching what the parser decodes after 01–03.
- [ ] zstd is named as not supported in all three.
- [ ] No other README content changes.

## Comments

23/11/2026, closed:
Done. README.md, README.it.md and README.ru.md: the XISF row of the supported-formats table now lists uncompressed or zlib, lz4, lz4hc, with or without byte shuffling; a new paragraph under each table says XISF image data and embedded thumbnails may be uncompressed or compressed with zlib, lz4 or lz4hc, with or without byte shuffling (+sh), that zstd is not supported yet (zstd images get no preview or thumbnail; a zstd thumbnail is skipped in favour of the main image, matching 01-03). The Preview Pane bullet 'full uncompressed images' became 'full-resolution images' in English and 'полноразмерного просмотра кадров' (dropped 'несжатых') in Russian; the Italian bullet already said 'a piena risoluzione' with no compression claim, so it is unchanged. No other README content changed. Build clean, run-linux-tests.sh 338/338. Needs manual verification: none
