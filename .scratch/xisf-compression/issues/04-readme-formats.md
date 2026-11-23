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

23/11/2026, comment:
Review: checked the three criteria, the issue text (the cited source is GitHub #1; there is no .scratch/xisf-compression/spec.md, so there were no spec stories to check) and every CODING_STANDARDS rule against README.md, README.it.md and README.ru.md. Criterion 1 is met: all three list zlib, lz4 and lz4hc, with or without +sh, for image data and embedded thumbnails. This matches XisfCompression.IsSupportedCodec and XisfCompressionSpec.Parse (01-02) and the thumbnail fallback in 03. Criterion 2 is met: zstd is named as not supported in all three, and the stated behaviour (no preview for a zstd main image, a zstd thumbnail falls back to the main image) matches the parser. Criterion 3 is met: the only other edit is the Preview Pane bullet, which the issue asks for (EN 'full uncompressed images' became 'full-resolution images', RU 'несжатых' dropped, IT had no such claim). Coding standards: no code, comments or tests changed; GLOSSARY.md is absent. Findings: none. Observation, left unchanged under criterion 3: the English I/O bullet says thumbnails read only the rows they need, which holds for uncompressed data but not for a compressed XISF block, which is read whole. Build is clean, and run-linux-tests.sh passes 338/338. Needs manual verification: none.
