# Nemoviz BC Finder

[English](README.md) · **Hrvatski** · [Srpski](README.sr.md)

Nemoviz BC Finder pretražuje Bandcamp **po državi i žanru**. Pronađena izdanja
filtrira po imenu izvođača ili naslovu. Izdanje se može preslušati u samom
programu, u Bandcampovom ugrađenom playeru, ili otvoriti u pregledniku: stranicu
izdanja, stranicu za preuzimanje ili stranicu izvođača.

Napravljen je za rad s čitačem ekrana (JAWS, NVDA). Sve se radi tipkovnicom, svaka
kontrola ima ime, a poruke programa čitač izgovara sam, bez pomicanja fokusa.

Bandcamp sam ne nudi pretragu po državi izvođača. Ovaj program to radi preko
Bandcampova servisa **Discover**, istog koji koristi `bandcamp.com/discover`.

## Preuzimanje

**[Preuzmi Nemoviz BC Finder (prenosivi zip)](https://github.com/gradic76/nemoviz-bc-finder/releases/latest/download/Nemoviz-BC-Finder-portable.zip)**

Poveznica uvijek vodi na najnoviju inačicu. Starije inačice su na stranici
[Releases](https://github.com/gradic76/nemoviz-bc-finder/releases).

1. Raspakiraj zip u bilo koju mapu.
2. Pokreni `Nemoviz BC Finder.exe`.

Instalacija nije potrebna. Treba:

- Windows 10 ili 11, 64-bitni;
- .NET Framework 4.8 i Microsoft Edge WebView2 Runtime (Windows 10 i 11 imaju oboje).

Program je prenosiv. Jedino što pamti je jezik sučelja, i to u mapi `data` pokraj
sebe. Ako se u tu mapu ne može pisati, pamti ga u `%LOCALAPPDATA%\Nemoviz BC Finder`.
Predmemoriju playera (slike i zvuk koje preglednik spremi dok slušaš) program drži
u Windowsovoj privremenoj mapi, `%TEMP%\Nemoviz BC Finder`, i ograničava je na
20 MB, pa ne puni mapu programa.

Za uklanjanje je dovoljno obrisati mapu programa, a po želji i tu privremenu mapu.

## Kako se koristi

**Uvjeti pretrage**

- **Države** i **Žanrovi**: izaberi stavku u popisu pa pritisni **Dodaj**. Može ih
  biti više, i tada vrijedi bilo koja od njih. Prva stavka (*Bilo koja država*,
  *Bilo koji žanr*) prazni popis.
- **Poredak**: najnovije, najstarije, izvođač ili naslov od A do Ž i obrnuto.
  Poredak se mijenja odmah, bez nove pretrage.
- **Traži** (Alt+S) pokreće pretragu, a **Stop** ili Escape je prekida.
- **Još** (Alt+M) učitava sljedećih tisuću izdanja.

**Filtri nad rezultatima**

- **Ime**: prikazuje samo izdanja kojima izvođač ili naslov sadrži upisani tekst.
  Filtrira dok tipkaš; Enter filtrira odmah i prebacuje na popis.
- **Samo besplatno preuzimanje**: prikazuje samo izdanja koja se mogu besplatno
  preuzeti.

**Rezultati**

| tipka | što radi |
|---|---|
| Enter ili dvoklik | svira izdanje u playeru ispod popisa |
| Shift+Enter | otvara stranicu izdanja u pregledniku |
| Ctrl+Enter | otvara **stranicu za preuzimanje**: stranicu izdanja s već otvorenim Bandcampovim prozorom za kupnju ili preuzimanje |
| Ctrl+Shift+Enter | otvara stranicu izvođača |
| Applications, Shift+F10 ili desni klik | kontekstni izbornik s istim izborima |

Kod besplatnih izdanja se u tom prozoru upiše iznos 0 i nastavi na preuzimanje.

**Player**

| tipka | što radi |
|---|---|
| F5 | prethodna pjesma |
| F6 | sviraj ili pauza |
| F7 | sljedeća pjesma |
| F8 | stop, i povratak na početak pjesme |
| strelica lijevo, desno | premotavanje 5 sekundi, uz Shift 30 sekundi |

Iste četiri radnje imaju i tipke ispod popisa, a rade i medijske tipke na
tipkovnici, iz bilo kojeg programa (osim ako ih već drži neki drugi). Naslovna
traka prozora pokazuje što svira.

Player je Bandcampov, a njegove tipke čitač ekrana ne vidi; zato ga program vozi
svojim tipkama.

**Ostalo**

- **F1** otvara pomoć.
- Izbornik **Jezik** mijenja jezik sučelja: engleski, hrvatski ili srpski
  (latinica). *Automatski* bira prema jeziku sustava.
- **Pomoć → Licence** pokazuje licence, a **Pomoć → O programu** podatke o programu.

## Što program radi s Bandcampom, i što ne radi

- Koristi samo Bandcampov servis Discover, za pretragu, i Bandcampov ugrađeni
  player, za slušanje. Discover je jedina adresa pod `/api/` koju Bandcampov
  `robots.txt` dopušta, a ugrađeni player je Bandcampov službeni način slušanja
  izvan njihove stranice.
- Šalje najviše jedan upit svakih 1,5 sekunde.
- **Ništa ne preuzima** i ne čita stranice izdanja. Ne koristi Bandcampovu
  tražilicu, streamove mimo playera ni adrese za preuzimanje. Sve što se kupuje
  ili preuzima, radi se na Bandcampu, u tvom pregledniku.
- Pretraga po imenu nije Bandcampova, nego filtrira ono što je Discover već vratio,
  jer je Bandcampova tražilica programima zabranjena.

Program nije povezan s Bandcampom niti ga Bandcamp podržava. Glazba, omoti i
tekstovi pripadaju svojim izvođačima i izdavačima.

## Prijevodi

Jezici su obične tekstne datoteke u mapi `jezici` (`Ime.lang`, UTF-8). Svaki redak
ima oblik `engleski izvornik=prijevod`. Šprancu sa svim tekstovima programa daje
konzolna inačica:

```
bcf.exe --sprance Novi.lang
```

## Gradnja iz izvornog koda

Treba C# prevoditelj (`csc`) iz Visual Studija i referentni sklopovi za .NET
Framework 4.8. Putanje stoje na vrhu `gradi.sh`.

```
bash gradi.sh
```

Rezultat je u `bin\`: `Nemoviz BC Finder.exe` i `bcf.exe`. Drugi je isti program
s konzolom, za mjerenje i probe, i ne ide u paket.

## Licence

- Program: **GNU General Public License, inačica 3** ([LICENSE](LICENSE)).
  Slobodan je za upotrebu, dijeljenje i mijenjanje, uz uvjet da i izmijenjene
  inačice ostanu pod istom licencom, s izvornim kodom.
- Microsoft Edge WebView2 SDK: Microsoftova licenca u BSD stilu
- Popis država: [GeoNames](https://www.geonames.org/), CC BY 4.0
- Popis žanrova: preuzet s `bandcamp.com/discover`

Puni tekstovi su u mapi `Licenses`.

## Autori

Copyleft 2026 Nemoguća vizija, [www.nemoviz.org](https://www.nemoviz.org)

Koncept: Gordan Radić. Kodiranje: Claude by Anthropic
