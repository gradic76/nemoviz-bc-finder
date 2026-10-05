// Nemoviz BC Finder -- find Bandcamp releases by country and genre, filter them by
// name, and open them on Bandcamp in the browser. Gordan's (2026-09-28); a
// separate project from NHR, whose accessible WPF pattern it takes: real controls with
// names for the screen reader, lists that say "3 of 46", messages through a live region.
//
//   Nemoviz BC Finder.exe                                 the window
//   bcf.exe --proba HR,RS punk,post-punk [n] [more]       measuring, no window
//   bcf.exe --okvir                                       the names the reader gets
//   bcf.exe --sprance <file>                              the translation template
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Without this the runtime runs the program as older than 4.7.1: newer accessibility
// features are off and the network starts with old TLS (measured in NHR).
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

static class Program
{
    public const string Ime = "Nemoviz BC Finder";
    public const string Verzija = "1.0";
    public const string Izvor = "https://github.com/gradic76/nemoviz-bc-finder";

    class Drzava { public string Ime, Iso; public long Geo; }
    class Zanr { public string Slug, Roditelj, Ime; public int Id; }

    static List<Drzava> drzave = new List<Drzava>();
    static List<Zanr> zanrovi = new List<Zanr>();

    static string Podaci { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "podaci"); } }

    static void UcitajPodatke()
    {
        foreach (string r in File.ReadAllLines(Path.Combine(Podaci, "drzave.txt"), System.Text.Encoding.UTF8))
        {
            var d = r.Split('|'); long g;
            if (d.Length == 3 && long.TryParse(d[1], out g)) drzave.Add(new Drzava { Ime = d[0], Geo = g, Iso = d[2] });
        }
        foreach (string r in File.ReadAllLines(Path.Combine(Podaci, "zanrovi.txt"), System.Text.Encoding.UTF8))
        {
            var d = r.Split('|');
            if (d.Length < 3) continue;
            var z = new Zanr { Slug = d[0], Roditelj = d[1], Ime = d[2] };
            if (d.Length > 3) int.TryParse(d[3], out z.Id);
            zanrovi.Add(z);
            if (z.Roditelj.Length == 0 && z.Id > 0) Bandcamp.ZanrImena[z.Id] = z.Ime;
        }
    }

    static string ImeZanra(Zanr z) { return z.Roditelj.Length == 0 ? z.Ime : z.Ime + " (" + z.Roditelj.Replace('-', ' ') + ")"; }

    // The only thing kept between runs is the interface language (not a search condition:
    // those are never remembered -- Gordan, 2026-09-28).
    static string PutJezika { get { return Path.Combine(Bandcamp.Mapa, "language.txt"); } }
    static string ZapamceniJezik() { try { return File.Exists(PutJezika) ? File.ReadAllText(PutJezika).Trim() : L.Automatski; } catch { return L.Automatski; } }
    static void ZapamtiJezik(string j) { try { File.WriteAllText(PutJezika, j); } catch { } }

    static Application app;

    [STAThread]
    static int Main(string[] args)
    {
        ServicePointManager();
        UcitajPodatke();
        L.Postavi(ZapamceniJezik());
        if (args.Length > 0 && args[0] == "--proba") return Proba(args);
        if (args.Length > 1 && args[0] == "--player-proba") return ProbaPlayera(long.Parse(args[1]));
        if (args.Length > 0 && args[0] == "--okvir") return Okvir(args.Length > 1 ? args[1] : null);
        if (args.Length > 1 && args[0] == "--sprance")
        {
            Prozor(); Tekst(PomocRetci()); Tekst(OProgramu());
            Console.WriteLine(L.Sprance(args[1]) + " texts");
            return 0;
        }
        app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var w = Prozor();
        w.Closed += (s, e) => { if (!mijenjamJezik) app.Shutdown(); };
        w.Show();
        app.Run();
        return 0;
    }

    static void ServicePointManager()
    {
        // Bandcamp answers TLS 1.2 with 403 (MEASURED 2026-09-28: .NET forced to 1.2 got 403
        // even for a plain page, with 1.3 200, curl 1.3 200). So 1.3, and 1.2 only as a fallback.
        System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls13 | System.Net.SecurityProtocolType.Tls12;
        // No "Expect: 100-continue" on a POST either -- it only adds a round trip.
        System.Net.ServicePointManager.Expect100Continue = false;
    }

    // ---------------- THE SEARCH ITSELF (window and measuring share it) ----------------

    class Upit
    {
        public List<long> Geo = new List<long>();         // empty = anywhere
        public List<string> Tagovi = new List<string>();  // empty = any genre
        public int NajviseZaPar = 1000;                   // per country x genre, each time
    }

    /// <summary>One country x genre pair and where its paging stands.</summary>
    class Par { public long Geo; public string Tag = ""; public string Cursor = "*"; public int Ukupno = -1, Dohvaceno; }

    /// <summary>A search that can be continued: More loads the next 1000 of every pair.
    /// (Gordan, 2026-09-28: Croatia, any genre -- 8679 on Bandcamp, 1020 shown.)</summary>
    class Pretraga
    {
        public Upit U;
        public List<Par> Parovi = new List<Par>();
        public HashSet<long> Vidjeno = new HashSet<long>();
        public Dictionary<long, Bandcamp.Izdanje> PoId = new Dictionary<long, Bandcamp.Izdanje>();
        public List<Bandcamp.Izdanje> Sve = new List<Bandcamp.Izdanje>();

        public Pretraga(Upit u)
        {
            U = u;
            foreach (long g in u.Geo.Count > 0 ? u.Geo : new List<long> { 0 })
                foreach (string t in u.Tagovi.Count > 0 ? u.Tagovi : new List<string> { "" })
                    Parovi.Add(new Par { Geo = g, Tag = t });
        }
        public bool ImaJos { get { return Parovi.Exists(p => p.Cursor != null); } }
        public int Dohvaceno { get { return Parovi.Sum(p => p.Dohvaceno); } }
        /// <summary>What Bandcamp says it has. With several genres a release may count in
        /// more than one pair, so this is "up to".</summary>
        public int Ukupno { get { return Parovi.Sum(p => Math.Max(0, p.Ukupno)); } }
    }

    /// <summary>Runs the pairs on from where they stopped, up to NajviseZaPar more each,
    /// newest first, merging by release. `javi` gets progress, `dodaj` the releases as
    /// they come.</summary>
    static void Trazi(Pretraga p, Action<string> javi, Action<List<Bandcamp.Izdanje>> dodaj, CancellationToken stop,
                      Action<Bandcamp.Izdanje> promijeni = null)
    {
        var u = p.U;
        int br = 0;
        foreach (var par in p.Parovi)
        {
            br++;
            int doSada = par.Dohvaceno;
            while (par.Cursor != null && par.Dohvaceno - doSada < u.NajviseZaPar)
            {
                if (stop.IsCancellationRequested) return;
                List<Bandcamp.Izdanje> l;
                try { l = Bandcamp.Stranica(par.Geo, par.Tag, ref par.Cursor, out par.Ukupno); }
                catch (Exception e) { javi(L.F("Bandcamp did not answer: {0}", e.Message)); break; }
                par.Dohvaceno += l.Count;
                // A release found again under another searched genre gets that genre too.
                if (par.Tag.Length > 0)
                    foreach (var x in l)
                    {
                        Bandcamp.Izdanje bio;
                        if (p.PoId.TryGetValue(x.Id, out bio)) { if (!bio.Tagovi.Contains(par.Tag)) { bio.Tagovi.Add(par.Tag); if (promijeni != null) promijeni(bio); } }
                        else x.Tagovi.Add(par.Tag);
                    }
                var n = l.FindAll(x => p.Vidjeno.Add(x.Id));
                foreach (var x in n) p.PoId[x.Id] = x;
                p.Sve.AddRange(n);
                dodaj(n);
                javi((p.Parovi.Count > 1 ? L.F("searching ({0} of {1}),", br, p.Parovi.Count) : L.T("searching,"))
                     + " " + L.F("loaded {0} of {1}", p.Dohvaceno, p.Ukupno));
            }
        }
    }

    static string ImeTaga(string slug) { var z = zanrovi.Find(x => x.Slug == slug); return z != null ? z.Ime : slug; }

    static string Redak(Upit u, Bandcamp.Izdanje x)
    {
        return x.Redak(u.Geo.Count != 1, u.Tagovi.Count != 1, ImeTaga);
    }

    static readonly string[] Poretci = { "Newest first", "Oldest first", "Artist, A to Z", "Artist, Z to A", "Title, A to Z", "Title, Z to A" };

    static int Usporedi(int poredak, Bandcamp.Izdanje a, Bandcamp.Izdanje b)
    {
        var c = StringComparer.CurrentCultureIgnoreCase;
        switch (poredak)
        {
            case 1: return a.Datum.CompareTo(b.Datum);
            case 2: { int r = c.Compare(a.Izvodac, b.Izvodac); return r != 0 ? r : c.Compare(a.Naslov, b.Naslov); }
            case 3: { int r = c.Compare(b.Izvodac, a.Izvodac); return r != 0 ? r : c.Compare(b.Naslov, a.Naslov); }
            case 4: return c.Compare(a.Naslov, b.Naslov);
            case 5: return c.Compare(b.Naslov, a.Naslov);
            default: return b.Datum.CompareTo(a.Datum);
        }
    }

    /// <summary>For the name filter: no case, no diacritics ("cokolada" finds "Čokolada").</summary>
    static string Ogoli(string s)
    {
        var d = (s ?? "").ToLowerInvariant().Replace('đ', 'd').Normalize(System.Text.NormalizationForm.FormD);
        var b = new System.Text.StringBuilder();
        foreach (char c in d) if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) b.Append(c);
        return b.ToString();
    }

    // ---------------- MEASURING ----------------

    /// <summary>The player without disturbing anyone: a window off the screen, not
    /// activated, the sound muted. Load, seek, next, pause, stop -- each read back from
    /// the page itself; then what the small player has, and how big the profile is.</summary>
    static int ProbaPlayera(long album)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var a = new Application();
        var pl = new Player(s => Console.WriteLine("  javi: " + s)) { Tiho = true };
        var w = new Window { Content = pl.Pogled, Width = 500, Height = 120, Left = -3000, Top = -3000, ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        pl.Naslovna = s => w.Title = s + " - " + Ime;
        w.Loaded += async (s, e) =>
        {
            Func<string, Task> stanje = async naziv => Console.WriteLine(naziv + ": " + await pl.StanjeZaProbu());
            pl.Sviraj(new Bandcamp.Izdanje { Id = album, Tip = "a", Izvodac = "?", Naslov = "?" });
            await Task.Delay(6000); await stanje("poslije ucitavanja");
            Console.WriteLine("kontrole: " + await pl.KontroleZaProbu());
            // BCF_PUNI=1: walk every track to its end in 20-second jumps, so the cache
            // gets the whole album -- the measure of the cap.
            if (Environment.GetEnvironmentVariable("BCF_PUNI") == "1")
            {
                string cache = Path.Combine(Player.MapaProfila, "EBWebView", "Default", "Cache");
                for (int i = 0; i < 60; i++)
                {
                    // BCF_ALBUMI=id,id,...: a new album every 30 s, so the audio is all new.
                    var albumi = (Environment.GetEnvironmentVariable("BCF_ALBUMI") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (albumi.Length > 0 && i % 6 == 0) { pl.Sviraj(new Bandcamp.Izdanje { Id = long.Parse(albumi[(i / 6) % albumi.Length]), Tip = "a", Izvodac = "?", Naslov = "?" }); await Task.Delay(4000); }
                    string r = await pl.BrzoZaProbu();
                    await Task.Delay(5000);
                    long mb = Directory.Exists(cache) ? new DirectoryInfo(cache).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1048576 : 0;
                    if (i % 6 == 5) Console.WriteLine("  " + (i + 1) * 5 + " s: " + r + ", cache " + mb + " MB");
                }
                w.Close(); return;
            }
            Console.WriteLine("premotaj +30: " + await pl.PremotajZaProbu(30));
            pl.Sljedeca(); await Task.Delay(2500); await stanje("poslije next");
            pl.Prethodna(); await Task.Delay(2500); await stanje("poslije prev");
            pl.PlayPauza(); await Task.Delay(1500); await stanje("poslije pauze");
            pl.PlayPauza(); await Task.Delay(8000); await stanje("poslije nastavka");
            Console.WriteLine("stop: " + await pl.StopZaProbu());
            Console.WriteLine("naslovna traka: " + w.Title);
            Console.WriteLine("profil: " + Player.MapaProfila);
            w.Close();
        };
        a.Run(w);
        return 0;
    }

    static int Proba(string[] a)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var u = new Upit();
        if (a.Length > 1 && a[1] != "-")
            foreach (string iso in a[1].Split(','))
            {
                var d = drzave.Find(x => x.Iso.Equals(iso, StringComparison.OrdinalIgnoreCase));
                if (d == null) { Console.WriteLine("no country " + iso); return 1; }
                u.Geo.Add(d.Geo);
            }
        if (a.Length > 2 && a[2] != "-") u.Tagovi.AddRange(a[2].Split(','));
        int n; if (a.Length > 3 && int.TryParse(a[3], out n)) u.NajviseZaPar = n;
        var sve = new List<Bandcamp.Izdanje>();
        var sat = System.Diagnostics.Stopwatch.StartNew();
        string zadnje = "";
        var p = new Pretraga(u);
        Trazi(p, s => zadnje = s, l => sve.AddRange(l), CancellationToken.None);
        Console.WriteLine(zadnje + ", " + sat.ElapsedMilliseconds / 1000 + " s, shown " + sve.Count);
        int puta; if (a.Length > 4 && int.TryParse(a[4], out puta))
            for (int m = 0; m < puta && p.ImaJos; m++)
            {
                Trazi(p, s => zadnje = s, l => sve.AddRange(l), CancellationToken.None);
                Console.WriteLine("more: " + zadnje + ", shown " + sve.Count + ", " + sat.ElapsedMilliseconds / 1000 + " s");
            }
        sve.Sort((x, y) => Usporedi(0, x, y));
        foreach (var x in sve.Take(15)) Console.WriteLine("  " + Redak(u, x) + (Environment.GetEnvironmentVariable("BCF_IDS") == "1" ? "  id=" + x.Tip + x.Id : ""));
        return 0;
    }

    /// <summary>Measuring without showing: the names the reader gets, row by row, in a
    /// given interface language.</summary>
    static int Okvir(string jezik)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (jezik != null) L.Postavi(jezik);
        var w = Prozor();
        Console.WriteLine("title: " + w.Title + ", icon: " + (w.Icon != null));
        var dp = (DockPanel)w.Content;
        foreach (var o in dp.Children)
        {
            var m = o as Menu;
            if (m != null)
                foreach (MenuItem mi in m.Items)
                {
                    Console.WriteLine("  menu " + mi.Header);
                    foreach (var pod in mi.Items) Console.WriteLine("      " + (pod is MenuItem ? ((MenuItem)pod).Header + "  " + ((MenuItem)pod).InputGestureText : "---"));
                }
        }
        var stup = (StackPanel)((ScrollViewer)dp.Children[1]).Content;
        foreach (var o in stup.Children)
        {
            var r = o as StackPanel;
            if (r == null)
            {
                Console.WriteLine("  [" + o.GetType().Name + "] " + AutomationProperties.GetName((DependencyObject)o));
                var lb = o as ListBox;
                if (lb != null && lb.ContextMenu != null)
                    foreach (var mi in lb.ContextMenu.Items) Console.WriteLine("      menu: " + (mi is MenuItem ? ((MenuItem)mi).Header + "  " + ((MenuItem)mi).InputGestureText : "---"));
                continue;
            }
            var dio = new List<string>();
            foreach (var k in r.Children)
            {
                var b = k as ContentControl; var c = k as ComboBox;
                string ime = AutomationProperties.GetName((DependencyObject)k);
                if (ime.Length == 0 && b != null && b.Content is string) ime = (string)b.Content;
                if (k is TextBlock) ime = "\"" + ((TextBlock)k).Text + "\"";
                dio.Add(k.GetType().Name + " " + ime + (c != null ? " [" + ((ComboBoxItem)c.SelectedItem).Content + ", " + (c.SelectedIndex + 1) + " of " + c.Items.Count + "]" : ""));
            }
            Console.WriteLine("  " + string.Join(" | ", dio));
        }
        return 0;
    }

    // ---------------- HELP, ABOUT, LICENCES ----------------

    // Short and plain (Gordan, 2026-09-28: "koncizno i jednostavno bez previse objasnjavanja").
    static string[] PomocRetci()
    {
        return new[]
        {
            "Countries, Genres: pick one in the list and press Add. Several can be added; any of them will do.",
            "The first item, Any, clears the list.",
            "Sort changes the order at once.",
            "Search starts, Stop or Escape stops. More loads the next thousand.",
            "Name, below: shows only the results whose artist or title contains this.",
            "In the results: Enter plays the release here. Shift+Enter opens its page in the browser, Ctrl+Enter its download page, Ctrl+Shift+Enter the artist page.",
            "F5 previous track, F6 play or pause, F7 next track, F8 stop. The media keys work too.",
            "Left and right arrows rewind and fast forward 5 seconds, with Shift 30 seconds.",
            "The Applications key or a right click opens the menu of a release.",
            "Free downloads only, beside Name, shows only releases you can download for free.",
            "The title bar shows what is playing.",
        };
    }

    static string[] OProgramu()
    {
        return new[]
        {
            Ime + ", version " + Verzija,
            "Copyleft 2026 Nemoguća vizija",
            "www.nemoviz.org",
            "Concept: Gordan Radić. Coding: Claude by Anthropic",
            "",
            "Finds releases on Bandcamp by country and genre, filters them by name, plays them in Bandcamp's own embedded player and opens them on Bandcamp in your browser.",
            "It uses only what Bandcamp allows: its Discover service and the embedded player. Nothing is downloaded by the program.",
            "Not affiliated with or endorsed by Bandcamp. The music belongs to its artists.",
            "Country list: GeoNames, CC BY 4.0. Browser engine: Microsoft Edge WebView2.",
            "Free software under the GNU General Public License, version 3. Source code: " + Izvor,
        };
    }

    /// <summary>Joins the lines, each translated.</summary>
    static string Tekst(string[] retci) { return string.Join("\r\n", Array.ConvertAll(retci, r => r.Length == 0 ? "" : L.T(r))); }

    /// <summary>A read-only text in a small window: the reader walks it with the arrows.</summary>
    static void Citaj(Window vlasnik, string naslov, string tekst)
    {
        var t = new TextBox { Text = tekst, IsReadOnly = true, IsReadOnlyCaretVisible = true, TextWrapping = TextWrapping.Wrap,
                              VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10) };
        AutomationProperties.SetName(t, naslov);
        var zatvori = Tipka(L.T("Close"), null);
        zatvori.HorizontalAlignment = HorizontalAlignment.Right; zatvori.Margin = new Thickness(10);
        var dp = new DockPanel();
        DockPanel.SetDock(zatvori, Dock.Bottom);
        dp.Children.Add(zatvori); dp.Children.Add(t);
        var w = new Window { Title = naslov + " - " + Ime, Content = dp, Width = 640, Height = 480, Owner = vlasnik,
                             WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Icon = vlasnik.Icon };
        zatvori.Click += (s, e) => w.Close();
        w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { w.Close(); e.Handled = true; } };
        w.Loaded += (s, e) => t.Focus();
        w.ShowDialog();
    }

    /// <summary>Licences: one window, a list of the documents and the text of the chosen one.</summary>
    static void Licence(Window vlasnik)
    {
        string mapa = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Licenses");
        var datoteke = Directory.Exists(mapa) ? Directory.GetFiles(mapa, "*.txt") : new string[0];
        var popis = new ListBox { Width = 220, Margin = new Thickness(10) };
        AutomationProperties.SetName(popis, L.T("Documents"));
        foreach (string f in datoteke) popis.Items.Add(Path.GetFileNameWithoutExtension(f));
        var t = new TextBox { IsReadOnly = true, IsReadOnlyCaretVisible = true, TextWrapping = TextWrapping.Wrap,
                              VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10) };
        AutomationProperties.SetName(t, L.T("Licence text"));
        popis.SelectionChanged += (s, e) => { try { t.Text = File.ReadAllText(datoteke[popis.SelectedIndex]); } catch { t.Text = ""; } };
        var zatvori = Tipka(L.T("Close"), null);
        zatvori.HorizontalAlignment = HorizontalAlignment.Right; zatvori.Margin = new Thickness(10);
        var dp = new DockPanel();
        DockPanel.SetDock(zatvori, Dock.Bottom); DockPanel.SetDock(popis, Dock.Left);
        dp.Children.Add(zatvori); dp.Children.Add(popis); dp.Children.Add(t);
        var w = new Window { Title = L.T("Licences") + " - " + Ime, Content = dp, Width = 820, Height = 560, Owner = vlasnik,
                             WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Icon = vlasnik.Icon };
        zatvori.Click += (s, e) => w.Close();
        w.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { w.Close(); e.Handled = true; } };
        w.Loaded += (s, e) => { if (popis.Items.Count > 0) popis.SelectedIndex = 0; popis.Focus(); };
        w.ShowDialog();
    }

    static bool mijenjamJezik;
    // Said once the window is built again, so the change of language is heard.
    static string porukaPoslije;

    // ---------------- THE WINDOW ----------------

    static Window Prozor()
    {
        var stup = new StackPanel { Margin = new Thickness(14) };
        var stanje = new TextBlock { Margin = new Thickness(0, 6, 0, 4), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(stanje, AutomationLiveSetting.Polite);
        Action<string> objavi = s =>
        {
            if (stanje.Text == s) return;
            stanje.Text = s;
            var p = UIElementAutomationPeer.CreatePeerForElement(stanje) ?? UIElementAutomationPeer.FromElement(stanje);
            if (p != null) p.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        };

        Func<string, UIElement[], StackPanel> redak = (oznaka, kontrole) =>
        {
            var r = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            r.Children.Add(new TextBlock { Text = oznaka.Length > 0 ? L.T(oznaka) : "", Width = 120, VerticalAlignment = VerticalAlignment.Center });
            foreach (var k in kontrole)
            {
                var f = k as FrameworkElement;
                if (f != null) { f.Margin = new Thickness(0, 0, 8, 0); f.VerticalAlignment = VerticalAlignment.Center; }
                r.Children.Add(k);
            }
            stup.Children.Add(r);
            return r;
        };

        // The name filters what the conditions brought, so it stands below them, right above
        // the results (Gordan, 2026-09-28). Bandcamp's own search is off limits to programs
        // (robots.txt: /api/ and /search).
        var imePolje = new TextBox { Width = 440 };
        AutomationProperties.SetName(imePolje, L.T("Filter the results: artist or title contains"));
        // Free downloads only (Gordan, 2026-10-04): a filter on what was found, like the
        // name; Bandcamp's Discover already says which releases are free.
        var samoBesplatno = new CheckBox { Content = L.T("Free downloads only"), Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

        // Country, genre, language: pick, Add, Clear, and the added in a read-only field.
        // The first item is "Any ...": a neutral place to stand; Add on it clears.
        Func<string, string, string, string, IList<string>, Func<int, string>, Func<string, string>, List<string>, ComboBox> popis =
            (oznaka, bilo, doDodaj, tipkaDodaj, stavke, kodZa, imeZa, dodani) =>
        {
            var k = new ComboBox { Width = 260 };
            AutomationProperties.SetName(k, L.T(doDodaj));
            var sve = new List<string> { L.T(bilo) }; sve.AddRange(stavke);
            Napuni(k, sve);
            var polje = new TextBox { Width = 440, IsReadOnly = true, IsReadOnlyCaretVisible = true, TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetName(polje, L.F("Added: {0}, read only", L.T(oznaka)));
            Action ispisi = () => polje.Text = dodani.Count == 0 ? L.T(bilo) : string.Join(", ", dodani.ConvertAll(x => imeZa(x)));
            ispisi();
            var d = Tipka(L.T(tipkaDodaj), () =>
            {
                if (k.SelectedIndex <= 0)
                {
                    bool bio = dodani.Count > 0; dodani.Clear(); ispisi();
                    objavi(bio ? L.F("{0}, the list is cleared", L.T(bilo)) : L.F("{0}, nothing to add", L.T(bilo)));
                    return;
                }
                string kod = kodZa(k.SelectedIndex - 1);
                if (dodani.Contains(kod)) { objavi(L.F("{0}, already added", imeZa(kod))); return; }
                dodani.Add(kod); ispisi();
                objavi(L.F("{0} added, {1} in all", imeZa(kod), dodani.Count));
            });
            var o = Tipka(L.T("Clear"), () => { dodani.Clear(); ispisi(); objavi(L.F("{0} cleared", L.T(oznaka))); });
            AutomationProperties.SetName(o, L.F("Clear {0}", L.T(oznaka)));
            redak(oznaka, new UIElement[] { k, d, o });
            redak("", new UIElement[] { polje });
            return k;
        };

        // Nothing is remembered between runs (Gordan, 2026-09-28): every start is "any".
        var mojeDrzave = new List<string>();
        var mojiZanrovi = new List<string>();

        var prviUvjet = popis("Countries", "Any country", "Country to add", "Add country", drzave.ConvertAll(x => x.Ime), i => drzave[i].Geo.ToString(),
              g => { var d = drzave.Find(x => x.Geo.ToString() == g); return d != null ? d.Ime : g; }, mojeDrzave);
        popis("Genres", "Any genre", "Genre to add", "Add genre", zanrovi.ConvertAll(ImeZanra), i => zanrovi[i].Slug,
              s => { var z = zanrovi.Find(x => x.Slug == s); return z != null ? ImeZanra(z) : s; }, mojiZanrovi);

        var poredak = new ComboBox { Width = 200 };
        AutomationProperties.SetName(poredak, L.T("Sort"));
        Napuni(poredak, Array.ConvertAll(Poretci, L.T));
        redak("Sort", new UIElement[] { poredak });

        var lista = new ListBox { Height = 380 };
        AutomationProperties.SetName(lista, L.T("Releases"));
        VirtualizingPanel.SetIsVirtualizing(lista, true);
        var nadeno = new List<Bandcamp.Izdanje>();       // everything found, in order
        var prikazano = new List<Bandcamp.Izdanje>();    // what the name lets through, as in the list
        CancellationTokenSource stop = null;

        Func<Bandcamp.Izdanje> izabrano = () => { var it = lista.SelectedItem as ListBoxItem; return it != null ? (Bandcamp.Izdanje)it.Tag : null; };
        Pretraga pretraga = null;
        Func<Bandcamp.Izdanje, bool> pasuje = x =>
        {
            if (samoBesplatno.IsChecked == true && !x.Besplatno) return false;
            string q = Ogoli(imePolje.Text.Trim());
            return q.Length == 0 || Ogoli(x.Izvodac + " " + x.Naslov).Contains(q);
        };
        Func<Bandcamp.Izdanje, ListBoxItem> stavka = x => new ListBoxItem { Content = Redak(pretraga.U, x), Tag = x };
        // A release already listed that came up under one more searched genre: its line grows.
        Action<Bandcamp.Izdanje> osvjeziRedak = x =>
        {
            int i = prikazano.IndexOf(x);
            if (i >= 0 && i < lista.Items.Count) ((ListBoxItem)lista.Items[i]).Content = Redak(pretraga.U, x);
        };
        // A full rebuild only when the order or the name changes (the user asked for it).
        Action slozi = () =>
        {
            var bio = izabrano();
            int p = Math.Max(0, poredak.SelectedIndex);
            nadeno.Sort((a, b) => Usporedi(p, a, b));
            prikazano.Clear(); lista.Items.Clear();
            if (pretraga == null) return;
            foreach (var x in nadeno) if (pasuje(x)) { prikazano.Add(x); lista.Items.Add(stavka(x)); }
            if (lista.Items.Count > 0) lista.SelectedIndex = bio == null ? 0 : Math.Max(0, prikazano.IndexOf(bio));
        };
        // New releases go in where the order puts them: the list is never cleared while
        // searching, so a reader walking it keeps the place (and the focus).
        Action<List<Bandcamp.Izdanje>> umetni = l =>
        {
            int p = Math.Max(0, poredak.SelectedIndex);
            foreach (var x in l)
            {
                int lo = 0, hi = nadeno.Count;
                while (lo < hi) { int m = (lo + hi) / 2; if (Usporedi(p, nadeno[m], x) <= 0) lo = m + 1; else hi = m; }
                nadeno.Insert(lo, x);
                if (!pasuje(x)) continue;
                lo = 0; hi = prikazano.Count;
                while (lo < hi) { int m = (lo + hi) / 2; if (Usporedi(p, prikazano[m], x) <= 0) lo = m + 1; else hi = m; }
                prikazano.Insert(lo, x);
                lista.Items.Insert(lo, stavka(x));
            }
            if (lista.SelectedIndex < 0 && lista.Items.Count > 0) lista.SelectedIndex = 0;
        };
        poredak.SelectionChanged += (s, e) => slozi();
        // The name filters as it is typed, once typing pauses.
        var cekaj = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        cekaj.Tick += (s, e) =>
        {
            cekaj.Stop();
            if (pretraga == null) return;
            slozi();
            objavi(imePolje.Text.Trim().Length == 0 ? L.F("{0} releases shown", prikazano.Count)
                                                     : L.F("{0} of {1} releases match the name", prikazano.Count, nadeno.Count));
        };
        imePolje.TextChanged += (s, e) => { cekaj.Stop(); cekaj.Start(); };
        RoutedEventHandler besplatno = (s, e) =>
        {
            if (pretraga == null) return;
            slozi();
            objavi(samoBesplatno.IsChecked == true ? L.F("{0} of {1} releases are free downloads", prikazano.Count, nadeno.Count)
                                                   : L.F("{0} releases shown", prikazano.Count));
        };
        samoBesplatno.Checked += besplatno; samoBesplatno.Unchecked += besplatno;

        // No embedded player any more (Gordan, 2026-10-05: it took too much room); a
        // release opens in the browser, at its page, its download dialog or its artist.
        Action<string, Func<Bandcamp.Izdanje, string>> uPregledniku = (poruka, adresa) =>
        {
            var x = izabrano();
            if (x == null) { objavi(L.T("nothing selected")); return; }
            string url = adresa(x);
            if (url.Length == 0) { objavi(L.T("no artist page for this release")); return; }
            try { System.Diagnostics.Process.Start(url); objavi(L.F(poruka, x.Izvodac, x.Naslov)); }
            catch (Exception e) { objavi(L.F("cannot open the browser: {0}", e.Message)); }
        };
        Action otvori = () => uPregledniku("opening {0} - {1} in the browser", x => x.Url);
        // ?action=download is the address Bandcamp's own "buy" links use: the page opens
        // with the buy or download dialog already up (measured 2026-10-05).
        Action preuzmi = () => uPregledniku("opening the download page of {0} - {1}", x => x.Url + (x.Url.Contains("?") ? "&" : "?") + "action=download");
        Action umjetnik = () => uPregledniku("opening the page of {0} in the browser", x => x.BandUrl);

        // The small player is back (Gordan, 2026-10-05), with its cache in the Windows
        // temp folder and capped; see Player.cs.
        var player = new Player(objavi);
        // Title bar: what plays, then the program (Gordan, 2026-09-28). Set once the window exists.
        Action<string> naslovna = s => { };
        player.Naslovna = s => naslovna(s);
        Action sviraj = () =>
        {
            var x = izabrano();
            if (x == null) { objavi(L.T("nothing selected")); return; }
            player.Sviraj(x);
        };
        Action<bool> trazi = null;
        var tTrazi = Tipka(L.T("Search (Alt+S)"), () => trazi(false));
        var tJos = Tipka(L.T("More (Alt+M)"), () => trazi(true));
        var tStop = Tipka(L.T("Stop"), () => { if (stop != null) { stop.Cancel(); objavi(L.T("stopping")); } });
        redak("", new UIElement[] { tTrazi, tJos, tStop });
        redak("Name", new UIElement[] { imePolje, samoBesplatno });
        stup.Children.Add(stanje);
        stup.Children.Add(lista);
        var ptipke = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        ptipke.Children.Add(Tipka(L.T("Previous (F5)"), player.Prethodna));
        ptipke.Children.Add(Tipka(L.T("Play or pause (F6)"), player.PlayPauza));
        ptipke.Children.Add(Tipka(L.T("Next (F7)"), player.Sljedeca));
        ptipke.Children.Add(Tipka(L.T("Stop (F8)"), player.Stop));
        stup.Children.Add(ptipke);
        player.Pogled.Margin = new Thickness(0, 6, 0, 0);
        stup.Children.Add(player.Pogled);
        // A release has its own menu (Gordan, 2026-09-28): Applications key, Shift+F10 or a right click.
        Func<string, string, Action, MenuItem> stavkaIzbornika = (ime, tipka, sto) =>
        {
            var m = new MenuItem { Header = L.T(ime), InputGestureText = tipka };
            m.Click += (s, e) => sto();
            return m;
        };
        var izbornik = new ContextMenu();
        izbornik.Items.Add(stavkaIzbornika("Play here", "Enter", sviraj));
        izbornik.Items.Add(stavkaIzbornika("Open release page", "Shift+Enter", otvori));
        izbornik.Items.Add(stavkaIzbornika("Open download page", "Ctrl+Enter", preuzmi));
        izbornik.Items.Add(stavkaIzbornika("Open artist page", "Ctrl+Shift+Enter", umjetnik));
        lista.ContextMenu = izbornik;
        // A right click first selects the release under the mouse, so the menu is about it.
        lista.PreviewMouseRightButtonDown += (s, e) =>
        {
            var it = ItemsControl.ContainerFromElement(lista, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (it != null) { it.IsSelected = true; it.Focus(); }
        };

        // The menu bar: Language and Help.
        var traka = new Menu();
        string izabran = ZapamceniJezik();
        // The current language is in the menu's own name, and the chosen item says so
        // (Gordan, 2026-10-05: the reader only said "language, collapsed", never which).
        Func<string, string> nazivJezika = j => j == L.Automatski ? L.F("Automatic ({0})", L.ZaSustav()) : j;
        var mJezik = new MenuItem { Header = L.F("_Language: {0}", nazivJezika(izabran)) };
        var jezici = new List<string> { L.Automatski }; jezici.AddRange(L.Popis());
        Window w = null;
        foreach (string j in jezici)
        {
            string jj = j;
            var mi = new MenuItem { Header = nazivJezika(jj), IsCheckable = true, IsChecked = jj == izabran };
            AutomationProperties.SetName(mi, nazivJezika(jj) + (jj == izabran ? ", " + L.T("selected") : ""));
            mi.Click += (s, e) =>
            {
                ZapamtiJezik(jj);
                L.Postavi(jj);
                // The window is built again in the new language; a search running stops.
                if (stop != null) stop.Cancel();
                mijenjamJezik = true;
                porukaPoslije = L.F("language: {0}", nazivJezika(jj));
                var novi = Prozor();
                novi.Closed += (s2, e2) => { if (!mijenjamJezik) app.Shutdown(); };
                novi.Show();
                w.Close();
                mijenjamJezik = false;
            };
            mJezik.Items.Add(mi);
        }
        var mPomoc = new MenuItem { Header = L.T("_Help") };
        var mHelp = new MenuItem { Header = L.T("Help"), InputGestureText = "F1" };
        mHelp.Click += (s, e) => Citaj(w, L.T("Help"), Tekst(PomocRetci()));
        var mLic = new MenuItem { Header = L.T("Licences") };
        mLic.Click += (s, e) => Licence(w);
        var mAbout = new MenuItem { Header = L.T("About") };
        mAbout.Click += (s, e) => Citaj(w, L.T("About"), Tekst(OProgramu()));
        mPomoc.Items.Add(mHelp); mPomoc.Items.Add(mLic); mPomoc.Items.Add(mAbout);
        traka.Items.Add(mJezik); traka.Items.Add(mPomoc);

        var dp = new DockPanel();
        DockPanel.SetDock(traka, Dock.Top);
        dp.Children.Add(traka);
        dp.Children.Add(new ScrollViewer { Content = stup, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false });

        w = new Window
        {
            Title = Ime,
            Content = dp,
            Width = 840, Height = 880,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF0, 0xEA))
        };
        // The icon is inside the exe (a resource), not a file beside it (Gordan, 2026-10-05).
        try { w.Icon = BitmapFrame.Create(typeof(Program).Assembly.GetManifestResourceStream("nemoviz.ico")); } catch { }
        naslovna = s => w.Title = s.Length > 0 ? s + " - " + Ime : Ime;

        trazi = nastavi =>
        {
            if (stop != null) { objavi(L.T("still searching; press Stop first")); return; }
            if (nastavi && (pretraga == null || !pretraga.ImaJos)) { objavi(L.T(pretraga == null ? "search first" : "that is all there is")); return; }
            if (!nastavi)
            {
                var u = new Upit
                {
                    Geo = mojeDrzave.ConvertAll(long.Parse),
                    Tagovi = new List<string>(mojiZanrovi)
                };
                pretraga = new Pretraga(u);
                nadeno.Clear(); prikazano.Clear(); lista.Items.Clear();
            }
            var pr = pretraga;
            stop = new CancellationTokenSource();
            var tok = stop.Token;
            var disp = w.Dispatcher;
            objavi(L.T(nastavi ? "loading more" : "searching"));
            Task.Run(() =>
            {
                try
                {
                    Trazi(pr, s => disp.BeginInvoke(new Action(() => objavi(s))),
                          l => { if (l.Count > 0) disp.BeginInvoke(new Action(() => umetni(l))); }, tok,
                          x => disp.BeginInvoke(new Action(() => osvjeziRedak(x))));
                }
                catch (Exception e) { disp.BeginInvoke(new Action(() => objavi(L.F("error: {0}", e.Message)))); }
                disp.BeginInvoke(new Action(() =>
                {
                    bool prekinuto = tok.IsCancellationRequested;
                    stop = null;
                    objavi((prekinuto ? L.T("stopped,") + " " : "") + L.F("{0} releases shown; loaded {1} of {2}", prikazano.Count, pr.Dohvaceno, pr.Ukupno)
                           + (pr.ImaJos ? ". " + L.F("More (Alt+M) loads the next {0}", pr.U.NajviseZaPar) : ", " + L.T("that is all")));
                }));
            });
        };

        lista.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            var mod = Keyboard.Modifiers;
            if (mod == (ModifierKeys.Control | ModifierKeys.Shift)) umjetnik();
            else if (mod == ModifierKeys.Control) preuzmi();
            else if (mod == ModifierKeys.Shift) otvori();
            else sviraj();
            e.Handled = true;
        };
        lista.MouseDoubleClick += (s, e) => sviraj();
        imePolje.KeyDown += (s, e) =>
        {
            if (e.Key != Key.Enter) return;
            cekaj.Stop(); if (pretraga != null) slozi();
            e.Handled = true;
            if (lista.Items.Count > 0) { var it = lista.ItemContainerGenerator.ContainerFromIndex(Math.Max(0, lista.SelectedIndex)) as ListBoxItem; if (it != null) it.Focus(); else lista.Focus(); }
            objavi(L.F("{0} releases shown", prikazano.Count));
        };
        w.PreviewKeyDown += (s, e) =>
        {
            if (e.Key == Key.System && e.SystemKey == Key.S) { trazi(false); e.Handled = true; }
            else if (e.Key == Key.System && e.SystemKey == Key.M) { trazi(true); e.Handled = true; }
            else if (e.Key == Key.F1) { Citaj(w, L.T("Help"), Tekst(PomocRetci())); e.Handled = true; }
            else if (e.Key == Key.F5) { player.Prethodna(); e.Handled = true; }
            else if (e.Key == Key.F6) { player.PlayPauza(); e.Handled = true; }
            else if (e.Key == Key.F7) { player.Sljedeca(); e.Handled = true; }
            else if (e.Key == Key.F8) { player.Stop(); e.Handled = true; }
            // Left and right seek (5 s, Shift 30 s), except where they move a caret or a
            // choice: a text field, a combo box, a menu.
            else if ((e.Key == Key.Left || e.Key == Key.Right) && (Keyboard.Modifiers == ModifierKeys.None || Keyboard.Modifiers == ModifierKeys.Shift)
                     && !(Keyboard.FocusedElement is TextBox) && !(Keyboard.FocusedElement is ComboBox) && !(Keyboard.FocusedElement is ComboBoxItem)
                     && !(Keyboard.FocusedElement is MenuItem))
            {
                int k = Keyboard.Modifiers == ModifierKeys.Shift ? 30 : 5;
                player.Premotaj(e.Key == Key.Left ? -k : k);
                e.Handled = true;
            }
            // Escape stops a search; it never closes the window (and never while a list is open).
            else if (e.Key == Key.Escape && stop != null && !(Keyboard.FocusedElement is ComboBoxItem)) { stop.Cancel(); objavi(L.T("stopping")); e.Handled = true; }
        };
        w.Closing += (s, e) => { if (stop != null) stop.Cancel(); };
        w.Loaded += (s, e) =>
        {
            prviUvjet.Focus();
            if (porukaPoslije != null) { objavi(porukaPoslije); porukaPoslije = null; }
        };
        w.SourceInitialized += (s, e) => player.UzmiMedijskeTipke(w);
        return w;
    }

    /// <summary>Items that carry "3 of 46" for the reader, even with the list closed
    /// (the NHR finding of 2026-09-28).</summary>
    static void Napuni(ComboBox c, IList<string> stavke)
    {
        c.Items.Clear();
        for (int i = 0; i < stavke.Count; i++)
        {
            var it = new ComboBoxItem { Content = stavke[i] };
            AutomationProperties.SetPositionInSet(it, i + 1);
            AutomationProperties.SetSizeOfSet(it, stavke.Count);
            c.Items.Add(it);
        }
        if (stavke.Count > 0) c.SelectedIndex = 0;
    }

    static Button Tipka(string ime, Action sto)
    {
        var b = new Button { Content = ime, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 4, 12, 4) };
        if (sto != null) b.Click += (s, e) => sto();
        return b;
    }
}
