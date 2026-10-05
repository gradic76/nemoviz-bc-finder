# Nemoviz BC Finder

**English** · [Hrvatski](README.hr.md) · [Srpski](README.sr.md)

Nemoviz BC Finder searches Bandcamp **by country and genre**. It filters what it
finds by artist or title. A release can be played in the program itself, in
Bandcamp's own embedded player, or opened in your browser: the release page, the
download page or the artist page.

It is made for screen readers (JAWS, NVDA). Everything works from the keyboard,
every control has a name, and the program's messages are spoken by the reader
without moving the focus.

Bandcamp itself has no search by the artist's country. This program does it
through Bandcamp's **Discover** service, the same one `bandcamp.com/discover` uses.

## Download

**[Download Nemoviz BC Finder (portable zip)](https://github.com/gradic76/nemoviz-bc-finder/releases/latest/download/Nemoviz-BC-Finder-portable.zip)**

The link always leads to the latest version. Older versions are on the
[Releases](https://github.com/gradic76/nemoviz-bc-finder/releases) page.

1. Unpack the zip into any folder.
2. Run `Nemoviz BC Finder.exe`.

No installation is needed. You need:

- Windows 10 or 11, 64-bit;
- .NET Framework 4.8 and the Microsoft Edge WebView2 Runtime (Windows 10 and 11
  have both).

The program is portable. The only thing it remembers is the interface language, in
the `data` folder beside it. If that folder cannot be written, it uses
`%LOCALAPPDATA%\Nemoviz BC Finder`. The player's cache (the pictures and sound the
browser keeps while you listen) is kept in the Windows temp folder,
`%TEMP%\Nemoviz BC Finder`, capped at 20 MB, so it does not fill the program folder.

To remove the program, delete its folder, and the temp folder if you like.

## How to use it

**Search conditions**

- **Countries** and **Genres**: pick an item in the list and press **Add**. Several
  can be added; any of them will do. The first item (*Any country*, *Any genre*)
  clears the list.
- **Sort**: newest, oldest, artist or title from A to Z and back. The order
  changes at once, without a new search.
- **Search** (Alt+S) starts, **Stop** or Escape stops.
- **More** (Alt+M) loads the next thousand releases.

**Filters on the results**

- **Name**: shows only the releases whose artist or title contains the text. It
  filters as you type; Enter filters at once and moves to the list.
- **Free downloads only**: shows only the releases that can be downloaded for free.

**Results**

| key | what it does |
|---|---|
| Enter or double click | plays the release in the player below the list |
| Shift+Enter | opens the release page in the browser |
| Ctrl+Enter | opens the **download page**: the release page with Bandcamp's buy or download dialog already open |
| Ctrl+Shift+Enter | opens the artist page |
| Applications, Shift+F10 or right click | context menu with the same choices |

For a free release, enter 0 as the amount in that dialog and go on to the download.

**Player**

| key | what it does |
|---|---|
| F5 | previous track |
| F6 | play or pause |
| F7 | next track |
| F8 | stop, back to the start of the track |
| left and right arrows | rewind and fast forward 5 seconds, with Shift 30 seconds |

The same four actions have buttons below the list, and the media keys work too,
from any program (unless another program already holds them). The title bar shows
what is playing.

The player is Bandcamp's, and a screen reader cannot see its buttons; that is why
the program drives it with its own keys.

**Other**

- **F1** opens the help.
- The **Language** menu changes the interface language: English, Croatian or
  Serbian (Latin). *Automatic* follows the system language.
- **Help → Licences** shows the licences, **Help → About** the program details.

## What the program does with Bandcamp, and what it does not

- It uses only Bandcamp's Discover service, for searching, and Bandcamp's embedded
  player, for listening. Discover is the one address under `/api/` that Bandcamp's
  `robots.txt` allows, and the embedded player is Bandcamp's official way of
  listening outside their site.
- It sends at most one request every 1.5 seconds.
- **It downloads nothing** and does not read release pages. It does not use
  Bandcamp's search, streams outside the player or download addresses. Anything
  bought or downloaded is done on Bandcamp, in your browser.
- The name search is not Bandcamp's; it filters what Discover has already returned,
  because Bandcamp's search is closed to programs.

The program is not affiliated with or endorsed by Bandcamp. The music, artwork and
texts belong to their artists and labels.

## Translations

Languages are plain text files in the `jezici` folder (`Name.lang`, UTF-8). Each
line is `English original=translation`. The console version writes a template with
every text in the program:

```
bcf.exe --sprance New.lang
```

## Building from source

You need the C# compiler (`csc`) from Visual Studio and the .NET Framework 4.8
reference assemblies. The paths are at the top of `gradi.sh`.

```
bash gradi.sh
```

The result is in `bin\`: `Nemoviz BC Finder.exe` and `bcf.exe`. The second is the
same program with a console, for measuring and testing, and is not shipped.

## Licences

- The program: **GNU General Public License, version 3** ([LICENSE](LICENSE)). Free
  to use, share and change, as long as changed versions stay under the same licence,
  with their source code.
- Microsoft Edge WebView2 SDK: Microsoft's BSD-style licence
- Country list: [GeoNames](https://www.geonames.org/), CC BY 4.0
- Genre list: taken from `bandcamp.com/discover`

The full texts are in the `Licenses` folder.

## Authors

Copyleft 2026 Nemoguća vizija, [www.nemoviz.org](https://www.nemoviz.org)

Concept: Gordan Radić. Coding: Claude by Anthropic
