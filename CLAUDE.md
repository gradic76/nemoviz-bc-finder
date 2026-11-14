# CLAUDE.md — Nemoviz BC Finder

Radni jezik je hrvatski, **na TI**; identifikatori, komentari i commit poruke su engleski.

Mali samostalni program (2026-09-28, Gordanov zahtjev): pretraga Bandcampa **po državi i
žanru** za alternativnu scenu koje nema na komercijalnim platformama. **Nije dio NHR-a**
(Nemoviz HiFi Rack). WPF, .NET Framework, WebView2 za Bandcampov ugrađeni player.

## Stanje

- **Objavljen 2026-10-05:** javni repozitorij https://github.com/gradic76/nemoviz-bc-finder
  (grana `main`, jedan commit „Nemoviz BC Finder 1.0"), izdanje **v1.0**. Izravna poveznica
  `…/releases/latest/download/Nemoviz-BC-Finder-portable.zip` — zato zip **nema broj
  inačice u imenu**; sljedeća inačica = novo izdanje sa zipom istog imena.
- Licenca koda **GPL-3** (`LICENSE`, u paketu `GPL-3.0.txt`); rečenica „za osobnu,
  nekomercijalnu upotrebu" je maknuta jer se kosi s GPL-om. Autorstvo svugdje: „Koncept:
  Gordan Radić. Kodiranje: Claude by Anthropic". About: Copyleft 2026 Nemoguća vizija,
  www.nemoviz.org.
- README na tri jezika (engleski prvi na GitHubu, hrvatski, srpski). Razvojne bilješke u
  `PROČITAJ.txt`. Prijevodi: `jezici\*.lang` (hrvatski, srpski latinica, ekavica); predložak
  za novi jezik daje konzolna kopija `bcf.exe --sprance`.
- Gradnja `gradi.sh` → `bin\Nemoviz BC Finder.exe` (winexe) i `bin\bcf.exe` (konzola za
  mjerenja: `--proba`, `--player-proba`, `--okvir`, `--sprance`). Paket
  `dist\Nemoviz-BC-Finder-portable.zip`, a raspakirana kopija je `dist\Nemoviz BC Finder`
  — **osvježi oboje**, Gordan pokreće iz raspakirane. Zip se radi s `tar.exe`
  (Compress-Archive piše obrnute kose crte), i to `/c/Windows/System32/tar.exe`: u Git Bashu
  je `tar.exe` Gitov GNU tar koji tiho napravi tar umjesto zipa. Mapa `data` ne ide u zip. Gradnja se provjerava (`grep -q "^gotovo"`)
  prije pokretanja.

## Što radi

- Uvjeti: **Države** i **Žanrovi** (combo + Add + Clear + polje samo za čitanje; prva stavka
  „Any"; više izbora = bilo koji), **Poredak** (datum gore/dolje, izvođač i naslov A–Ž/Ž–A,
  mijenja se bez nove pretrage). Ispod: Traži (Alt+S), Još (Alt+M, sljedećih 1000 po paru),
  Stop/Escape; zatim **Ime** (filtrira već dohvaćeno, Enter filtrira i skače u popis; od
  2026-11-14 upisano ime prije Traži znači dohvat svih parova do kraja, bez Još, a bez države
  i žanra pretraga odbija, jer cijeli Bandcamp ne može proći) i kvačica
  **Samo besplatno preuzimanje** (filtrira, ne pita Bandcamp ništa novo).
- **Ništa se ne pamti** između pokretanja osim jezika sučelja (Gordan: uvjeti i filtri
  nikako). Jezični uvjet je **izbačen** 2026-09-28 („presporo a i nema baš previše smisla").
- Redak: izvođač, album, mjesto/država, žanr (država i žanr samo kad nije izabran točno
  jedan), datum (samo dan, u Bandcampovom UTC-u), broj pjesama, „besplatno preuzimanje".
- **Album se otvara na mjestu** (2026-11-14, kao u Racku): Enter na albumu pokazuje pjesme u
  drugom popisu (iz `data-player-data` stranice EmbeddedPlayera, jedan upit po albumu), singl
  odmah svira; u pjesmama Enter svira album od te pjesme (učita album pa Next N puta, jer
  `track=ID` u embedu svira samo tu pjesmu), Backspace natrag. Sviraj ovdje je u izborniku.
- **Player:** Bandcampov uski EmbeddedPlayer (`size=small`, 42 px) u WebView2, u Tab redu;
  predmemorija u `%TEMP%\Nemoviz BC Finder\webview`, ograničena (prije je u `data` narasla na
  265 MB). Tipke: Enter svira, Shift+Enter stranica izdanja, Ctrl+Enter stranica za preuzimanje
  (`?action=download`), Ctrl+Shift+Enter stranica izvođača; F5 prethodna, F6 play/pauza,
  F7 sljedeća, F8 pravi stop (vrati na početak i stane); strelice lijevo/desno 5 s, sa Shiftom
  30 s (ne u tekstnom polju, combou, izborniku ni u samom playeru); medijske tipke globalno
  (ako ih drži NHR, program to kaže). Pauza ne pritišće Bandcampovu tipku nego zaustavlja zvuk
  stranice (na singlu je F6 inače vraćao na početak). Naslovna traka „N. Naslov - Izvođač".
  Gumbi Bandcampova playera nemaju imena za čitač — zato ih pritišću naše tipke.
- Izbornik Jezik nosi trenutni jezik u imenu („Jezik: Automatski (Hrvatski)"), Pomoć: F1
  (kratka, NE PROČITAJ.txt), Licence, O programu.

## Pravila (Bandcamp)

- Samo `/api/discover/1/discover_web` (robots.txt ga dopušta) i EmbeddedPlayer, **1 upit /
  1,5 s**, UA „NemovizBCFinder/<inačica>". Zabranjeno: ostalo pod `/api/`, `/search`, `/stream`,
  `/download_check`; stranice izdanja se ne čitaju; poveznica za preuzimanje se ne vadi (to bi
  zaobišlo ono što je izvođač postavio, npr. e-mail za besplatno preuzimanje). Zvuk se nikad ne
  vadi mimo njihova playera.
- Zamke: .NET na TLS 1.2 dobiva 403 (treba Tls13); `include_result_types` "t" se odbija, "a"
  su izdanja; jedan upit = jedna država, više oznaka u upitu = I, pa se „bilo koji" gradi iz
  parova; `result_count` daje ukupno (Hrvatska ≈ 8679).
- 2026-11-13 Bandcamp je sva imena polja prebacio u camelCase (`resultCount`, `trackCount`);
  `Polje()` prihvaća oba oblika. Simptom: „učitano 0 od 0".
- Stranica: `size` prima do 1000, ali Bandcamp gradi 1000 oko 7 s, 200 oko 1 s; uz razmak 1,5 s
  200 je jednako brzo i lakše za njih (izmjereno 2026-11-14: cijela Hrvatska 87 s).

## Povijest mape

Do 2026-11-06 projekt je bio u `D:\Bandcamp Finder`; razgovori o njemu vođeni su u
sesijama projekta Media Player (sada `D:\Claude\Nemoviz HiFi Rack`, sesije a33f13be,
4df33e72, e8fe855b — izmiješane s NHR-om, zato su ondje ostale). Ovaj sažetak izvučen je iz
njih 2026-11-06.
