// Lokalizacija.cs -- the interface in other languages, from plain .lang files. TAKEN
// from NHR (Jezik.cs, 2026-09-17), where it is proven: `English text=translation`, one
// per line, UTF-8, in `<program>\jezici\<Name>.lang`. English is built in and is the
// key itself, so the code still reads what the user will see.
//
// Added here (Gordan, 2026-09-28: "brzinska lokalizacija na hrvatski + language
// detect + mogucnost drugih lokalizacija"): a .lang file says which system languages
// it is for, with a line `#culture=hr,bs,sr`; Automatic picks the one that matches
// Windows. Another language is one more file in the folder -- `--sprance` writes the
// template with every text the program uses.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

static class L
{
    public const string Engleski = "English";
    public const string Automatski = "";

    public static string Trenutni = Engleski;
    static readonly Dictionary<string, string> prijevod = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly SortedDictionary<string, string> vidjeno = new SortedDictionary<string, string>(StringComparer.Ordinal);

    public static string Mapa { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jezici"); } }

    /// <summary>English plus every .lang file.</summary>
    public static List<string> Popis()
    {
        var l = new List<string> { Engleski };
        try { if (Directory.Exists(Mapa)) foreach (string f in Directory.GetFiles(Mapa, "*.lang")) if (!f.EndsWith("Sprance.lang")) l.Add(Path.GetFileNameWithoutExtension(f)); }
        catch { }
        return l;
    }

    /// <summary>The .lang file that says it is for this system language, or English.</summary>
    public static string ZaSustav()
    {
        string kod = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        foreach (string ime in Popis())
        {
            if (ime == Engleski) continue;
            try
            {
                foreach (string r in File.ReadAllLines(Path.Combine(Mapa, ime + ".lang"), Encoding.UTF8))
                {
                    if (!r.StartsWith("#culture=")) continue;
                    foreach (string k in r.Substring(9).Split(','))
                        if (k.Trim().Equals(kod, StringComparison.OrdinalIgnoreCase)) return ime;
                }
            }
            catch { }
        }
        return Engleski;
    }

    /// <summary>"" = Automatic (the system language).</summary>
    public static void Postavi(string ime)
    {
        prijevod.Clear();
        Trenutni = string.IsNullOrEmpty(ime) ? ZaSustav() : ime;
        if (Trenutni == Engleski) return;
        try
        {
            string f = Path.Combine(Mapa, Trenutni + ".lang");
            if (!File.Exists(f)) { Trenutni = Engleski; return; }
            foreach (string redak in File.ReadAllLines(f, Encoding.UTF8))
            {
                string r = redak.TrimEnd();
                if (r.Length == 0 || r[0] == '#') continue;
                int j = r.IndexOf('=');
                if (j > 0) prijevod[r.Substring(0, j).Trim()] = r.Substring(j + 1).Trim();
            }
        }
        catch { Trenutni = Engleski; }
    }

    /// <summary>The translation; the key IS the English text.</summary>
    public static string T(string engleski)
    {
        if (engleski == null) return "";
        vidjeno[engleski] = engleski;
        string p;
        return prijevod.TryGetValue(engleski, out p) && p.Length > 0 ? p : engleski;
    }

    /// <summary>Translation with the numbers and names put in: F("{0} added", x).</summary>
    public static string F(string engleski, params object[] a) { return string.Format(T(engleski), a); }

    /// <summary>Writes the template with every text that went through T().</summary>
    public static int Sprance(string put)
    {
        var s = new StringBuilder();
        s.AppendLine("# Nemoviz BC Finder, translation template. Left: English, right: translation.");
        s.AppendLine("# Save as <Language>.lang in the `jezici` folder next to the program.");
        s.AppendLine("# #culture= lists the Windows languages it is for (two letters), for Automatic.");
        s.AppendLine("# {0}, {1}... are filled in by the program and must stay. Empty = keep English.");
        s.AppendLine("#culture=");
        s.AppendLine();
        int n = 0;
        foreach (var kv in vidjeno) { if (kv.Key.Trim().Length == 0) continue; s.AppendLine(kv.Key.Replace("\n", "\\n") + "="); n++; }
        File.WriteAllText(put, s.ToString(), new UTF8Encoding(false));
        return n;
    }
}
