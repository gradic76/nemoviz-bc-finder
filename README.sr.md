# Nemoviz BC Finder

[English](README.md) · [Hrvatski](README.hr.md) · **Srpski**

Nemoviz BC Finder pretražuje Bandcamp **po državi i žanru**. Pronađena izdanja
filtrira po imenu izvođača ili naslovu. Izdanje može da se presluša u samom
programu, u Bandcampovom ugrađenom plejeru, ili da se otvori u pregledaču:
stranica izdanja, stranica za preuzimanje ili stranica izvođača.

Napravljen je za rad sa čitačem ekrana (JAWS, NVDA). Sve se radi tastaturom, svaka
kontrola ima ime, a poruke programa čitač izgovara sam, bez pomeranja fokusa.

Bandcamp sam ne nudi pretragu po državi izvođača. Ovaj program to radi preko
Bandcampovog servisa **Discover**, istog koji koristi `bandcamp.com/discover`.

## Preuzimanje

**[Preuzmi Nemoviz BC Finder (prenosivi zip)](https://github.com/gradic76/nemoviz-bc-finder/releases/latest/download/Nemoviz-BC-Finder-portable.zip)**

Link uvek vodi na najnoviju verziju. Starije verzije su na stranici
[Releases](https://github.com/gradic76/nemoviz-bc-finder/releases).

1. Raspakuj zip u bilo koju fasciklu.
2. Pokreni `Nemoviz BC Finder.exe`.

Instalacija nije potrebna. Potrebno je:

- Windows 10 ili 11, 64-bitni;
- .NET Framework 4.8 i Microsoft Edge WebView2 Runtime (Windows 10 i 11 imaju oba).

Program je prenosiv. Jedino što pamti je jezik interfejsa, i to u fascikli `data`
pored sebe. Ako u tu fasciklu ne može da se piše, pamti ga u
`%LOCALAPPDATA%\Nemoviz BC Finder`. Keš plejera (slike i zvuk koje pregledač sačuva
dok slušaš) program drži u privremenoj fascikli Windowsa, `%TEMP%\Nemoviz BC Finder`,
i ograničava ga na 20 MB, pa ne puni fasciklu programa.

Za uklanjanje je dovoljno obrisati fasciklu programa, a po želji i tu privremenu
fasciklu.

## Kako se koristi

**Uslovi pretrage**

- **Države** i **Žanrovi**: izaberi stavku sa spiska pa pritisni **Dodaj**. Može ih
  biti više, i tada važi bilo koja od njih. Prva stavka (*Bilo koja država*,
  *Bilo koji žanr*) prazni spisak.
- **Redosled**: najnovije, najstarije, izvođač ili naslov od A do Ž i obrnuto.
  Redosled se menja odmah, bez nove pretrage.
- **Traži** (Alt+S) pokreće pretragu, a **Stop** ili Escape je prekida.
- **Još** (Alt+M) učitava sledećih hiljadu izdanja.

**Filteri nad rezultatima**

- **Ime**: prikazuje samo izdanja čiji izvođač ili naslov sadrži upisani tekst.
  Filtrira dok kucaš; Enter filtrira odmah i prebacuje na spisak.
- **Samo besplatno preuzimanje**: prikazuje samo izdanja koja mogu besplatno da se
  preuzmu.

**Rezultati**

| taster | šta radi |
|---|---|
| Enter ili dvoklik | pušta izdanje u plejeru ispod spiska |
| Shift+Enter | otvara stranicu izdanja u pregledaču |
| Ctrl+Enter | otvara **stranicu za preuzimanje**: stranicu izdanja sa već otvorenim Bandcampovim prozorom za kupovinu ili preuzimanje |
| Ctrl+Shift+Enter | otvara stranicu izvođača |
| Applications, Shift+F10 ili desni klik | kontekstni meni sa istim izborima |

Kod besplatnih izdanja u tom prozoru se upiše iznos 0 i nastavi na preuzimanje.

**Plejer**

| taster | šta radi |
|---|---|
| F5 | prethodna pesma |
| F6 | pusti ili pauza |
| F7 | sledeća pesma |
| F8 | stop, i povratak na početak pesme |
| strelica levo, desno | premotavanje 5 sekundi, uz Shift 30 sekundi |

Iste četiri radnje imaju i dugmad ispod spiska, a rade i medijski tasteri na
tastaturi, iz bilo kog programa (osim ako ih već drži neki drugi). Naslovna traka
prozora pokazuje šta svira.

Plejer je Bandcampov, a njegovu dugmad čitač ekrana ne vidi; zato ga program vozi
svojim tasterima.

**Ostalo**

- **F1** otvara pomoć.
- Meni **Jezik** menja jezik interfejsa: engleski, hrvatski ili srpski (latinica).
  *Automatski* bira prema jeziku sistema.
- **Pomoć → Licence** prikazuje licence, a **Pomoć → O programu** podatke o
  programu.

## Šta program radi sa Bandcampom, i šta ne radi

- Koristi samo Bandcampov servis Discover, za pretragu, i Bandcampov ugrađeni
  plejer, za slušanje. Discover je jedina adresa pod `/api/` koju Bandcampov
  `robots.txt` dozvoljava, a ugrađeni plejer je Bandcampov zvanični način slušanja
  van njihovog sajta.
- Šalje najviše jedan upit na svakih 1,5 sekundi.
- **Ništa ne preuzima** i ne čita stranice izdanja. Ne koristi Bandcampov
  pretraživač, strimove mimo plejera ni adrese za preuzimanje. Sve što se kupuje
  ili preuzima, radi se na Bandcampu, u tvom pregledaču.
- Pretraga po imenu nije Bandcampova, nego filtrira ono što je Discover već vratio,
  jer je Bandcampov pretraživač programima zabranjen.

Program nije povezan sa Bandcampom niti ga Bandcamp podržava. Muzika, omoti i
tekstovi pripadaju svojim izvođačima i izdavačima.

## Prevodi

Jezici su obične tekstualne datoteke u fascikli `jezici` (`Ime.lang`, UTF-8).
Svaki red ima oblik `engleski izvornik=prevod`. Šablon sa svim tekstovima programa
daje konzolna verzija:

```
bcf.exe --sprance Novi.lang
```

## Izgradnja iz izvornog koda

Potreban je C# prevodilac (`csc`) iz Visual Studija i referentni sklopovi za .NET
Framework 4.8. Putanje su na vrhu `gradi.sh`.

```
bash gradi.sh
```

Rezultat je u `bin\`: `Nemoviz BC Finder.exe` i `bcf.exe`. Drugi je isti program
sa konzolom, za merenje i probe, i ne ide u paket.

## Licence

- Program: **GNU General Public License, verzija 3** ([LICENSE](LICENSE)).
  Slobodan je za upotrebu, deljenje i menjanje, pod uslovom da i izmenjene verzije
  ostanu pod istom licencom, sa izvornim kodom.
- Microsoft Edge WebView2 SDK: Microsoftova licenca u BSD stilu
- Spisak država: [GeoNames](https://www.geonames.org/), CC BY 4.0
- Spisak žanrova: preuzet sa `bandcamp.com/discover`

Puni tekstovi su u fascikli `Licenses`.

## Autori

Copyleft 2026 Nemoguća vizija, [www.nemoviz.org](https://www.nemoviz.org)

Koncept: Gordan Radić. Kodiranje: Claude by Anthropic
