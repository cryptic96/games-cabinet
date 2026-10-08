# Review sheet

The review sheet is a set of PNG pages that shows, for every game in the stored
collection, which picture the cabinet uses and why. It is built by the service
executable itself in an operator mode, from the stored collection and the stored
pictures, with the same `Art` settings the running service reads. A sheet
therefore shows exactly what the deployed cabinet does, not an approximation.

The sheet is for checking three things against the real boxes on the shelf:
which picture each game uses, whether the spine colours read well, and whether the
box sizes look right. When something is off, the answer is usually one of the
`Art` settings in the server env file (the detector thresholds, the shape margin
or the orientation switch); change it, restart the service, and build the sheet
again. No picture is fetched again for that.

## What a page shows

Each page holds up to eight games, one row per game, in collection order. The
numbers continue across pages, so a game can be named by its number. The page
header states the detector thresholds and the shape margin in use. The rules
text is wrapped onto as many lines as the page width needs, and the rows start
below the last line.

| Column | What it shows |
| --- | --- |
| No. | The position in collection order |
| Title | The title on one line, shortened with an ellipsis when it is long |
| A: owned version | The picture of the edition that is owned, 160 pixels high, or the word `none` |
| B: main image | The game's main picture, 160 pixels high, or the word `none` |
| Verdict of A | `flat`, `3D shot` or `unsure` with the detector's score to two decimals, or `no verdict` when there is no usable picture of the owned edition; a picture with a transparent background reads `3D shot, cut-out`, and a box photographed so close that it runs along all four sides reads `3D shot, tight crop` |
| Chosen | `chosen: version image`, `chosen: main image` or `generated cover`; a 3 pixel tan outline marks the chosen picture in column A or B |
| Result | The box front as the cabinet draws it: the chosen picture shown whole in a box of the drawn proportions, on the colours along its four edges, 160 pixels high |
| Spine | A 40 by 160 pixel strip in the spine colour with the title in its text colour |
| Size source | `real size`, `cover shape`, `estimate` or `default` |

A picture that failed to download, could not be read, or whose file is missing
from the picture directory shows `none`, and the Chosen column names the fallback
that applied.

## The pages hold private data

The pages contain the owner's own titles and pictures. They are never committed,
never attached to an issue or a pull request, and never published. They live in
the server's state directory and on a private folder of the workstation until
they have been looked at, and are then deleted. The command prints counts only
(pages, games, and how many verdicts and choices of each kind), never a title or
a picture path, so its output is safe to paste.

## Building a sheet on the server

The command runs as the service user, with the service's environment, so that it
reads the same state directory and settings:

```sh
sudo systemd-run --wait --pipe --quiet \
  --uid=cabinet --gid=cabinet \
  --property=EnvironmentFile=/etc/cabinet/cabinet.env \
  --working-directory=/opt/cabinet/current/app \
  /usr/bin/dotnet Cabinet.Service.dll review-sheet \
  --state /var/lib/cabinet --out /var/lib/cabinet/review-sheet
```

It writes `review-01.png`, `review-02.png` and so on into the output directory,
creating it when needed, and prints one line of counts. The command has no web
route; nothing on the site can start it, and it needs no network.

Options:

| Option | Meaning |
| --- | --- |
| `--state <directory>` | The state directory that holds the stored collection and the `art` directory. Required. |
| `--out <directory>` | Where the pages are written. Required. |
| `--font <file>` | A TrueType font for the text, instead of the DejaVu fonts. |
| `--bold-font <file>` | A bold TrueType font; the regular font is used when it is not given. |

Exit codes:

| Code | Meaning |
| --- | --- |
| 0 | The pages were written |
| 2 | The arguments or an `Art` setting were not understood |
| 3 | The state directory holds no stored collection |
| 4 | No usable font; install `fonts-dejavu-core` or pass `--font` |
| 5 | The output directory could not be written |

## Fonts

The text is drawn with an explicit font file, because a container with no fonts
would otherwise produce pages with no words on them. Provisioning installs
`fonts-dejavu-core`, and the command looks for the DejaVu files in the standard
Debian and Ubuntu location first and the common alternative second. On a
container that was provisioned before the font was added, install it by hand:

```sh
sudo apt-get install --yes fonts-dejavu-core
```

## Fetching the pages and deleting them

Copy the pages to a private folder on the workstation, then remove them from the
server. The first command streams the directory over the existing SSH access:

```sh
ssh <container> 'sudo tar -C /var/lib/cabinet -cf - review-sheet' | tar -xf - -C <private folder>
ssh <container> 'sudo rm -r /var/lib/cabinet/review-sheet'
```

`<container>` is the name of the container in the SSH configuration and
`<private folder>` is a folder outside any repository checkout.
