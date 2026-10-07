# BoardGameGeek access check

`build/bgg-access-check.py` finds out, with the real application token, what
BoardGameGeek actually returns to this application. Some of the answers cannot
be read from documentation: whether the private inventory location of a game
is returned to an application token, how queued answers behave, which
collection fields and units come back, and whether a User-Agent is required.

The repository is public, so the check is built to report only the shape of
what it sees. It is run once by hand, and its result is kept privately.

## What it asks BoardGameGeek

All requests go to the one API host over HTTPS, one at a time, at least six
seconds apart, with a hard cap of 14 requests in total. Redirects are never
followed, and the token is only ever sent to that host.

| Call | What it asks | What it is for |
| --- | --- | --- |
| A | The owned collection without expansions, with statistics and the selected version | The request the sync makes for base games: queued answers, field shapes, box dimensions |
| B | The same, plus the private information flag | Whether the private inventory location comes back with only the token |
| C | The owned expansions only | The expansion split and whether it overlaps call A |
| D | The owned collection without any subtype filter | Whether expansions are mislabelled as base games |
| E | The owned expansions plus the private information flag | Whether private information also comes back for expansions |
| F | One public game, with a User-Agent | A baseline for a cheap authenticated call |
| G | The same public game, without a User-Agent | Whether a User-Agent is required |
| H | The owned collection with no token | The exact shape of a refusal |
| I | The owned collection with a deliberately wrong token | Whether a bad token is refused the same way |

If a queued answer comes back, the check waits and asks again with growing
pauses, at most six times, and counts each of those requests against the cap.
If any of calls A to G is refused with 401, 403 or 429, the run stops there and
prints what it has. The check never tries to provoke a rate limit.

`python3 build/bgg-access-check.py --plan` prints this list with parameter
names only. It reads nothing and uses no network.

## What it prints

For each call: the label, the status code, whole seconds taken, how many queued
answers came first, a short list of response headers (content type and length,
cache control, retry-after, server, a challenge marker and any rate-limit
headers), the names of any cookies, the body size and the body class (items,
errors, message, other XML, web page, empty or other).

For a collection answer it adds counts and ranges only:

- the declared item total against the parsed item count
- attribute and child element names, with how many items carry each
- the distribution of subtype and own values
- how many object ids and collection ids repeat
- for the selected version: how often width, length and depth are present,
  whether they sit in an attribute or in text, how many are zero, and their
  smallest, median and largest non-zero values
- how names are carried and whether entity markers remain in them
- whether year, play count, comment, image and thumbnail are present
- for private information: how many items carry it, its attribute names, how
  many have a non-empty inventory location, how many distinct values there are
  and the longest value's length
- how call C and call D compare with call A

## What it never prints

It never prints the token, the BoardGameGeek username, the contact address,
any game title, any location value, any address of any kind, or any markup.
The check does not take any secret from the command line: it reads the token,
the username and an optional contact address from the server's env file
itself.

The whole report is built as one piece of text first. Before it is printed it
is scanned for the token, the username, the contact address, every title and
every location value seen in any answer, the text `http` and the `<`
character. If any of those is found, nothing is printed except a one-line
notice and the check exits with code 4.

`python3 build/bgg-access-check.py --self-test` proves this on built-in
invented answers that carry made-up titles, a made-up location and sentinel
credentials: the counts are kept, the values are dropped, and the guard fires
when a sentinel is deliberately put into the report. The same test runs in the
repository's lint checks.

## Exit codes

| Code | Meaning |
| --- | --- |
| 0 | The check completed |
| 2 | Wrong or missing arguments |
| 3 | Not configured: the token or the username is missing from the env file; nothing was sent |
| 4 | Output withheld because it would have contained a sensitive value |
| 5 | A stop rule fired; the shape gathered so far is printed |

## Before a run

1. In BoardGameGeek, set an inventory location in the private information of
   two or three owned games. Any values will do; they never leave the server.
2. On the server, add the settings to the env file by hand and keep it
   readable by the application user only:

   ```
   Bgg__Token=<token created for the registered application>
   Bgg__Username=<the account whose owned collection the cabinet shows>
   Bgg__ContactUrl=<optional: the public repository address>
   ```

   Never paste the token into a chat, a ticket or any file in the repository.
3. Check that Python 3 is installed on the server:
   `ssh <container> python3 --version`.

## Running it

From a workstation, stream the script to the server and run it as the
application user. `<container>` stands for your own SSH host alias:

```
ssh <container> 'sudo -n -u cabinet python3 - --run' < build/bgg-access-check.py
```

The script is read from standard input, so nothing is installed on the server.
It prints progress labels on standard error while it works and the full report
on standard output when it finishes, which can take a few minutes because of
the spacing between requests.

## Keeping the result

Read the printed shape yourself and keep the conclusions in private notes, or
as plain sentences that hold no real value. Do not commit the raw output of a run, and
never copy a real title, location or username into the repository.
