# Language table: English and Dutch side by side

Generated from the code that ships, so what you read is what visitors get: the server tables for the page, and the page script table. Nothing here is hand-copied.

BGG titles, designers, mechanics and storage locations are never translated: they appear exactly as BoardGameGeek and the owner wrote them. The BGG credit text ("Powered by BGG") and the toggle names (EN, NL) are the same in both languages on purpose.

Open point D1 (the Dutch site name) is the first row, `SiteText.SiteName`: the Dutch draft is **Spellenkast**; the alternative is keeping **Games Cabinet** in both languages.

Please answer by key: "approve", or "edit `<key>`: <new Dutch text>". Edits go into both the server table and the script table, and the parity tests stay green.

## Server: page labels

From the server tables for the page; written into the first byte of the page. `{0}` and `{1}` are filled in by the server.

| Key | English | Dutch |
| --- | --- | --- |
| `SiteText.SiteName` | Games Cabinet | Spellenkast |
| `SiteText.LanguageLabel` | Language | Taal |
| `SiteText.EnglishName` | EN, English | EN, English (same) |
| `SiteText.DutchName` | NL, Nederlands | NL, Nederlands (same) |
| `SiteText.SkipLink` | Skip to the list of games | Ga naar de lijst met spellen |
| `SiteText.GamesListHeading` | All games | Alle spellen |
| `SiteText.CabinetLabel` | Cabinet | Kast |
| `SiteText.CabinetHint` | Use the arrow keys to move between games and press Enter to open one. | Gebruik de pijltoetsen om tussen spellen te bewegen en druk op Enter om er een te openen. |
| `SiteText.FillingHeading` | The cabinet is being filled. | De kast wordt gevuld. |
| `SiteText.FillingBody` | The games are being copied over from BoardGameGeek. Check back in a few minutes. | De spellen worden overgenomen van BoardGameGeek. Kom over een paar minuten terug. |
| `SiteText.NoScript` | The cabinet needs JavaScript to be drawn. Please turn it on and reload. | De kast heeft JavaScript nodig om getekend te worden. Zet het aan en laad de pagina opnieuw. |
| `SiteText.VersionFormat` | Version {0} ({1}) | Versie {0} ({1}) |
| `SiteText.LogoAlt` | Powered by BGG | Powered by BGG (same) |
| `SiteText.SyncNow` | Sync now | Nu synchroniseren |
| `SiteText.LastSyncedFormat` | Last synced {0} | Laatst gesynchroniseerd op {0} |
| `SiteText.Version(1.2.3, abc1234)` | Version 1.2.3 (abc1234) | Versie 1.2.3 (abc1234) |
| `SiteText.LastSynced(6 October 2026 at 12:32 UTC)` | Last synced 6 October 2026 at 12:32 UTC | Laatst gesynchroniseerd op 6 oktober 2026 om 12:32 UTC |

## Server: sync status sentences

The server writes these on first paint (UTC); the page script then rewrites them from the visitor's own clock, using the script table below.

| Key | English | Dutch |
| --- | --- | --- |
| `Status.NeverSynced` | Not synced yet | Nog niet gesynchroniseerd |
| `Status.Relative(30 s)` | Synced just now | Zojuist gesynchroniseerd |
| `Status.Relative(60 s)` | Synced 1 minute ago | 1 minuut geleden gesynchroniseerd |
| `Status.Relative(300 s)` | Synced 5 minutes ago | 5 minuten geleden gesynchroniseerd |
| `Status.Relative(3600 s)` | Synced 1 hour ago | 1 uur geleden gesynchroniseerd |
| `Status.Relative(10800 s)` | Synced 3 hours ago | 3 uur geleden gesynchroniseerd |
| `Status.Relative(86400 s)` | Synced yesterday | Gisteren gesynchroniseerd |
| `Status.Relative(259200 s)` | Synced 3 days ago | 3 dagen geleden gesynchroniseerd |
| `Status.ExactUtc(2026-10-06 12:32)` | 6 October 2026 at 12:32 UTC | 6 oktober 2026 om 12:32 UTC |
| `Status.StaleRecent(exact)` | Showing the last sync from {exact}. Recent syncs haven't gone through. | Je ziet de laatste synchronisatie van {exact}. Recente synchronisaties zijn niet gelukt. |
| `Status.StaleHeldBack(exact)` | Showing the last sync from {exact}. A much smaller collection from BGG is waiting for the next sync to confirm. | Je ziet de laatste synchronisatie van {exact}. Een veel kleinere collectie van BGG wacht op bevestiging bij de volgende synchronisatie. |

## Script: fixed strings

From the page script table (`COPY_BY_LANGUAGE`). `numberLocale` is not wording: it picks the decimal separator.

| Key | English | Dutch |
| --- | --- | --- |
| `loading` | Loading the cabinet... | De kast wordt geladen... |
| `errorHeading` | The cabinet could not be loaded. | De kast kon niet worden geladen. |
| `errorBody` | Check your connection and try again. | Controleer je verbinding en probeer het opnieuw. |
| `retry` | Try again | Opnieuw proberen |
| `untitled` | Untitled game | Spel zonder titel |
| `close` | Close | Sluiten |
| `bggLink` | View on BoardGameGeek | Bekijk op BoardGameGeek |
| `newTabHint` | (opens in a new tab) | (opent in een nieuw tabblad) |
| `noDetails` | More details arrive after the next sync. | Meer details volgen na de volgende synchronisatie. |
| `playTimeLabel` | play time | speelduur |
| `minutesShort` | min | min (same) |
| `weightWords` | Light / Medium-light / Medium / Medium-heavy / Heavy | Licht / Vrij licht / Gemiddeld / Vrij zwaar / Zwaar |
| `weightWordsLower` | light / medium-light / medium / medium-heavy / heavy | licht / vrij licht / gemiddeld / vrij zwaar / zwaar |
| `ageLabel` | min. age | min. leeftijd |
| `storedIn` | Stored in | Staat in |
| `ownedExpansions` | Owned expansions | Uitbreidingen in de kast |
| `ratingLabel` | BGG rating | BGG-score |
| `expansionForHeading` | Expansion for | Uitbreiding op |
| `listExpansion` | expansion | uitbreiding |
| `numberLocale` | en | nl |
| `syncNow` | Sync now | Nu synchroniseren |
| `syncing` | Syncing... | Bezig met synchroniseren... |
| `noteChanged` | Collection updated. BGG can take a few minutes to show recent edits. | Collectie bijgewerkt. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien. |
| `noteUnchanged` | No changes found. BGG can take a few minutes to show recent edits. | Geen wijzigingen gevonden. Het kan een paar minuten duren voordat BGG recente wijzigingen laat zien. |
| `noteFailed` | BGG didn't respond. The last collection is still showing. | BGG reageerde niet. De laatste collectie blijft zichtbaar. |
| `noteHeldBack` | BGG returned far fewer games than before, so the last collection is still showing. | BGG gaf veel minder spellen terug dan eerder, dus de laatste collectie blijft zichtbaar. |
| `noteRunning` | A sync is already running. | Er loopt al een synchronisatie. |
| `noteOffline` | Couldn't start the sync. Check your connection and try again. | De synchronisatie kon niet starten. Controleer je verbinding en probeer het opnieuw. |
| `expansionLabel` | Expansion | Uitbreiding |

## Script: strings built from values

Each function is called with sample values, shown in the key column.

| Key | English | Dutch |
| --- | --- | --- |
| `playersLabel(1)` | player | speler |
| `playersLabel(4)` | players | spelers |
| `weightLabel("2,5")` | weight 2,5 / 5 | zwaarte 2,5 / 5 |
| `designersLabel(1)` | Designer | Ontwerper |
| `designersLabel(3)` | Designers | Ontwerpers |
| `mechanicsLabel(1)` | Mechanic | Mechanisme |
| `mechanicsLabel(3)` | Mechanics | Mechanismen |
| `listPlayers("2-4", 4)` | 2-4 players | 2-4 spelers |
| `listPlayers("1", 1)` | 1 player | 1 speler |
| `listMinutes("60")` | 60 minutes | 60 minuten |
| `listExpansionFor("Base Game")` | expansion for Base Game | uitbreiding op Base Game |
| `syncedAgo(30)` | Synced just now | Zojuist gesynchroniseerd |
| `syncedAgo(300)` | Synced 5 minutes ago | 5 minuten geleden gesynchroniseerd |
| `syncedAgo(10800)` | Synced 3 hours ago | 3 uur geleden gesynchroniseerd |
| `syncedAgo(86400)` | Synced yesterday | Gisteren gesynchroniseerd |
| `syncedAgo(259200)` | Synced 3 days ago | 3 dagen geleden gesynchroniseerd |
| `exactTime(date 2026-10-06 12:32 UTC)` | 6 October 2026 at 12:32 UTC | 6 oktober 2026 om 12:32 UTC |
| `lastSynced("{exact}")` | Last synced {exact} | Laatst gesynchroniseerd op {exact} |
| `staleRecent("{exact}")` | Showing the last sync from {exact}. Recent syncs haven't gone through. | Je ziet de laatste synchronisatie van {exact}. Recente synchronisaties zijn niet gelukt. |
| `staleHeldBack("{exact}")` | Showing the last sync from {exact}. A much smaller collection from BGG is waiting for the next sync to confirm. | Je ziet de laatste synchronisatie van {exact}. Een veel kleinere collectie van BGG wacht op bevestiging bij de volgende synchronisatie. |
| `syncAgainIn("4:30")` | Sync again in 4:30 | Opnieuw synchroniseren over 4:30 |
| `syncAgainName(30000)` | Sync again in less than a minute | Opnieuw synchroniseren over minder dan een minuut |
| `syncAgainName(270000)` | Sync again in 5 minutes | Opnieuw synchroniseren over 5 minuten |
| `syncAgainName(60000)` | Sync again in 1 minute | Opnieuw synchroniseren over 1 minuut |
| `youCanSyncAgain(30000)` | You can sync again in less than a minute. | Je kunt over minder dan een minuut opnieuw synchroniseren. |
| `youCanSyncAgain(270000)` | You can sync again in 5 minutes. | Je kunt over 5 minuten opnieuw synchroniseren. |
| `expansionFor("Base Game")` | Expansion for Base Game | Uitbreiding op Base Game |
| `expansionName("Extra Pack")` | Extra Pack, expansion | Extra Pack, uitbreiding |
| `layerName("Extra Pack", "Base Game")` | Extra Pack, expansion for Base Game | Extra Pack, uitbreiding op Base Game |
| `moreLabel(3)` | +3 more | +3 meer |
| `moreName(1, "Base Game")` | +1 more expansion for Base Game | +1 meer uitbreiding op Base Game |
| `moreName(3, "Base Game")` | +3 more expansions for Base Game | +3 meer uitbreidingen op Base Game |

## Script: the game-night facts for sample values

Players 2 to 4, play time 45 to 90 minutes, weight 3.4, minimum age 12.

| Key | English | Dutch |
| --- | --- | --- |
| `facts` | 2–4 players ; 45–90 min play time ; Medium weight 3.4 / 5 ; 12+ min. age | 2–4 spelers ; 45–90 min speelduur ; Gemiddeld zwaarte 3,4 / 5 ; 12+ min. leeftijd |
