# Third-party images: "Powered by BGG" logos

This directory holds two variants of BoardGameGeek's official "Powered by BGG" logo, both taken from the official logo pack that BoardGameGeek links from its XML API terms of use. One of them is shown, linked back to BoardGameGeek, in the footer of every public page as the attribution those terms ask for: the dark logo on the cabinet page when the cabinet stands on a light wall, and the white logo on every other page and on every other wall.

## `powered-by-bgg.svg`

`powered-by-bgg.svg` is the reversed RGB SVG variant, drawn in white for dark backgrounds.

| Item | Value |
| --- | --- |
| Name | Powered by BGG logo, reversed, RGB, SVG |
| Source | BoardGameGeek's official logo pack, linked from BoardGameGeek's XML API terms of use (the exact page address is not recorded here) |
| File SHA-256 | `b577fd17bd5f84ea575fb10ff1f7fa3895b54727728bfd4a4ed11300a722bc43` |
| Rights holder | BoardGameGeek |
| Terms | Used under BoardGameGeek's XML API terms of use, which require this logo, linked back to BoardGameGeek, on public-facing uses of its data |

## `powered-by-bgg-dark.svg`

`powered-by-bgg-dark.svg` is the standard RGB SVG variant, drawn in dark navy for light backgrounds. The file in the official logo pack is named `powered-by-bgg-rgb.svg`; it is stored here under a name that says where it is used.

| Item | Value |
| --- | --- |
| Name | Powered by BGG logo, standard, RGB, SVG |
| Source | BoardGameGeek's official logo pack, linked from BoardGameGeek's XML API terms of use (the exact page address is not recorded here) |
| File SHA-256 | `527357eff019297fa1a1e81002723c1eb5bbbf8ae80e5a31fdae17bf8f82d8cc` |
| Rights holder | BoardGameGeek |
| Terms | Used under BoardGameGeek's XML API terms of use, which require this logo, linked back to BoardGameGeek, on public-facing uses of its data |

## Handling

Both files are copied unchanged: they are not recoloured, resized, re-encoded or edited here, and only the file name of the second one differs from the pack. A unit test pins each SHA-256 above, so any change to either file fails the build. The logos are not covered by this repository's licence.

To replace one, take the new file from BoardGameGeek's official logo pack, update its hash above and in the unit test, and keep it unmodified.
