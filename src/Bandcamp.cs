// Bandcamp.cs -- the only two things this program asks Bandcamp for.
//
// 1. DISCOVER (POST /api/discover/1/discover_web). This is what bandcamp.com/discover
//    itself uses, and it is the one /api/ path their robots.txt ALLOWS. It takes a tag
//    (genre or subgenre slug) and a GeoNames id (country) and gives releases page by
//    page with a cursor. MEASURED 2026-09-28:
//      * one request = one country; several tags in one request mean ALL of them
//        (punk 728, punk + hardcore 136 in Croatia) -- so "any of" is several requests;
//      * the server sorts only "new arrivals" (newest first); other orders are ours;
//      * size 60 works.
// (The language condition, which read release pages, was taken out on 2026-09-28:
// too slow, and of little use -- Gordan.)
//
// Nothing else: no release pages, no search API (/api/ is disallowed), no stream, no download. Albums are
// opened in the browser and listened to there. One request every 1.5 s, always.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

static class Bandcamp
{
    const string Predstavljanje = "NemovizBCFinder/1.0 (personal, non-commercial)";
    const int RazmakMs = 1500;
    static DateTime zadnji = DateTime.MinValue;
    static readonly object brava = new object();

    /// <summary>Genre ids to names (filled from podacizanrovi.txt).</summary>
    public static readonly Dictionary<int, string> ZanrImena = new Dictionary<int, string>();

    public class Izdanje
    {
        public long Id;
        public string Tip = "a";               // "a" album, "t" track
        public string Izvodac = "", Naslov = "", Mjesto = "", Url = "", BandUrl = "";
        public DateTime Datum;
        public int Pjesama;
        public int ZanrId;                     // the ARTIST's main genre, as Bandcamp gives it with the release
        // Download from nothing: "name your price" with a minimum of 0, or free outright
        // (MEASURED 2026-09-28: 38 of 60 Croatian punk releases, price 0 and not a set price).
        public bool Besplatno;
        public List<string> Tagovi = new List<string>();   // the searched genres this release came up under

        /// <summary>The line for the list (Gordan, 2026-09-28): what was set to exactly ONE
        /// value is left out, because it is the same on every line; what was "any" or two
        /// and more is written. `imeTaga` names a searched genre.</summary>
        public string Redak(bool zemlja, bool zanr, Func<string, string> imeTaga)
        {
            string z = "";
            if (zanr)
            {
                if (Tagovi.Count > 0) z = string.Join(" / ", Tagovi.ConvertAll(t => imeTaga(t)));   // which of the searched
                else ZanrImena.TryGetValue(ZanrId, out z);                                            // any: the artist's own
            }
            // One country: "Pula, Croatia" is said as "Pula", "Croatia" alone not at all.
            string m = Mjesto;
            if (!zemlja) { int c = m.LastIndexOf(", "); m = c > 0 ? m.Substring(0, c) : ""; }
            // Order (Gordan, 2026-09-28): artist, album, country, genre (those two only when
            // needed), release date -- the date alone -- and the number of tracks.
            return Izvodac + " - " + Naslov + (Tip == "t" ? " (" + L.T("track") + ")" : "")
                 + (m.Length > 0 ? ", " + m : "")
                 + (!string.IsNullOrEmpty(z) ? ", " + z : "")
                 + (Datum != DateTime.MinValue ? ", " + Datum.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : "")
                 + (Pjesama > 1 ? ", " + L.F("{0} tracks", Pjesama) : "")
                 + (Besplatno ? ", " + L.T("free download") : "");
        }
    }

    static string Dohvati(string url, string json)
    {
        lock (brava)
        {
            double proslo = (DateTime.UtcNow - zadnji).TotalMilliseconds;
            if (proslo < RazmakMs) System.Threading.Thread.Sleep((int)(RazmakMs - proslo));
            zadnji = DateTime.UtcNow;
        }
        var z = (HttpWebRequest)WebRequest.Create(url);
        z.UserAgent = Predstavljanje;
        z.Timeout = 30000;
        z.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        if (json != null)
        {
            z.Method = "POST";
            z.ContentType = "application/json";
            byte[] b = Encoding.UTF8.GetBytes(json);
            using (var s = z.GetRequestStream()) s.Write(b, 0, b.Length);
        }
        try
        {
            using (var o = (HttpWebResponse)z.GetResponse())
            using (var r = new StreamReader(o.GetResponseStream(), Encoding.UTF8))
                return r.ReadToEnd();
        }
        catch (WebException e)
        {
            if (Environment.GetEnvironmentVariable("BCF_TRAG") == "1" && e.Response != null)
                using (var r = new StreamReader(e.Response.GetResponseStream()))
                {
                    string t = r.ReadToEnd();
                    Console.Error.WriteLine("poslano: " + json + " | odgovor: " + t.Substring(0, Math.Min(400, t.Length)));
                }
            throw;
        }
    }

    static object Polje(object o, string k)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(k, out v) ? v : null;
    }
    static string Tekst(object o, string k) { var v = Polje(o, k); return v == null ? "" : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture); }
    static System.Collections.IEnumerable Niz(object o) { return o as System.Collections.IEnumerable; }

    /// <summary>One page of Discover for one country (0 = anywhere) and one tag ("" =
    /// any), newest first. `cursor` "*" starts; comes back null at the end.</summary>
    public static List<Izdanje> Stranica(long geo, string tag, ref string cursor, out int ukupno)
    {
        var js = new JavaScriptSerializer();
        string tijelo = js.Serialize(new Dictionary<string, object>
        {
            { "category_id", 0 },
            { "tag_norm_names", tag.Length > 0 ? new[] { tag } : new string[0] },
            { "geoname_id", geo },
            { "slice", "new" },
            { "cursor", cursor },
            { "size", 60 },
            { "include_result_types", new[] { "a" } }         // "a" = releases, singles too; "t" is refused, "s" is merch
        });
        string odg = Dohvati("https://bandcamp.com/api/discover/1/discover_web", tijelo);
        if (Environment.GetEnvironmentVariable("BCF_TRAG") == "1") Console.Error.WriteLine("odgovor " + odg.Length + ": " + odg.Substring(0, Math.Min(300, odg.Length)));
        var d = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 }.DeserializeObject(odg);
        if (Tekst(d, "__api_special__") == "exception") throw new InvalidOperationException("Bandcamp refused the query (" + Tekst(d, "error_type") + ")");
        int.TryParse(Tekst(d, "result_count"), out ukupno);
        cursor = Polje(d, "cursor") as string;
        var l = new List<Izdanje>();
        var rez = Niz(Polje(d, "results"));
        if (rez == null) { cursor = null; return l; }
        foreach (object r in rez)
        {
            var i = new Izdanje
            {
                Tip = Tekst(r, "item_type"),
                Izvodac = Tekst(r, "album_artist").Length > 0 ? Tekst(r, "album_artist") : Tekst(r, "band_name"),
                Naslov = Tekst(r, "title"),
                Mjesto = Tekst(r, "band_location"),
                Url = Regex.Replace(Tekst(r, "item_url"), @"\?from=discover_page$", ""),
                BandUrl = Regex.Replace(Tekst(r, "band_url"), @"\?from=discover_page$", "")
            };
            long.TryParse(Tekst(r, "item_id"), out i.Id);
            int.TryParse(Tekst(r, "track_count"), out i.Pjesama);
            int.TryParse(Tekst(r, "band_genre_id"), out i.ZanrId);
            int cijena; int.TryParse(Tekst(Polje(r, "price"), "amount"), out cijena);
            i.Besplatno = Tekst(r, "is_free_download") == "True" || (Tekst(r, "is_set_price") != "True" && Polje(r, "price") != null && cijena == 0);
            DateTime dt;
            string rd = Tekst(r, "release_date").Replace(" UTC", "");
            if (DateTime.TryParse(rd, System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out dt)) i.Datum = dt.Date;   // the UTC date: a release at 23:30 UTC stays on its day
            if (i.Tip != "a" && i.Tip != "t") continue;          // merch never
            l.Add(i);
        }
        if (l.Count == 0) cursor = null;
        return l;
    }

    /// <summary>PORTABLE (Gordan, 2026-09-28): everything the program keeps -- the
    /// interface language -- is in `data`
    /// next to it. Only if that folder cannot be written, the user's local AppData.</summary>
    public static string Mapa
    {
        get
        {
            if (mapa != null) return mapa;
            string m = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            try { Directory.CreateDirectory(m); File.WriteAllText(Path.Combine(m, ".write-test"), ""); File.Delete(Path.Combine(m, ".write-test")); }
            catch { m = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nemoviz BC Finder"); Directory.CreateDirectory(m); }
            return mapa = m;
        }
    }
    static string mapa;
}
