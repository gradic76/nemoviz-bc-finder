// Player.cs -- Bandcamp's own embedded player, inside the window (Gordan, 2026-09-28:
// "Play album, pauza, naprijed i natrag su mi sasvim dovoljni za pocetak, pa ako i
// nisu pristupacni da se ozice na kratice ili globalne mm tipke").
//
// The embedded player (bandcamp.com/EmbeddedPlayer/album=ID) is the official way to
// listen outside Bandcamp. It runs in WebView2, the Edge engine Windows already has.
// MEASURED 2026-09-28: its buttons are plain divs without names (#big_play_button,
// .nextbutton, .prevbutton), so a screen reader cannot use them -- the keys here press
// them through a script, and the title playing is read back from #currenttitle.
// Audio plays in the player; nothing is taken out of it.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

sealed class Player
{
    public readonly WebView2 Pogled = new WebView2 { Height = Visina };
    // The small player (Gordan, 2026-10-05: the large one took too much room): one row,
    // play button, title and progress.
    public const int Visina = 42;

    /// <summary>The browser profile and its cache live in the Windows temp folder, not
    /// beside the program (MEASURED 2026-10-05: 265 MB in `data`, 223 MB of it cache),
    /// and the cache is capped.</summary>
    public static string MapaProfila = Path.Combine(Path.GetTempPath(), "Nemoviz BC Finder", "webview");
    public const long NajviseCache = 20L * 1024 * 1024;

    readonly Action<string> javi;
    bool spreman, sviraSe;
    Bandcamp.Izdanje tekuce;

    /// <summary>For measuring: no sound out of the machine.</summary>
    public bool Tiho;

    /// <summary>For measuring: title, paused, time -- straight from the page.</summary>
    public async Task<string> StanjeZaProbu()
    {
        return await Skripta("(function(){var a=document.querySelector('audio'),t=document.querySelector('#currenttitle_title'),c=document.querySelector('#currenttime');"
                             + "return (t?t.innerText:'?')+' | paused='+(a?a.paused:'no audio')+' | t='+(a?a.currentTime.toFixed(1):'-')+' | '+(c?c.innerText:'');})()");
    }

    /// <summary>Gets "3. Title - Artist" whenever the track changes, "" when nothing is
    /// loaded -- for the title bar (Gordan, 2026-09-28).</summary>
    public Action<string> Naslovna = s => { };
    string zadnjaNaslovna = "";
    readonly System.Windows.Threading.DispatcherTimer straza = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };

    public Player(Action<string> javi)
    {
        this.javi = javi;
        // The player goes on to the next track by itself; the title bar follows it. It is
        // NOT announced: an announcement on every track is chatter (Gordan, NHR).
        straza.Tick += async (s, e) => await OsvjeziNaslovnu();
        // In the Tab order on purpose (Gordan, 2026-09-28): to hear what the reader makes of
        // the page. Out again if it does not help.
    }

    async Task Pripremi()
    {
        if (spreman) return;
        // Audio may start from our keys: the page never gets a real click, so the
        // autoplay rule of Chromium would keep it silent.
        // BCF_BEZ_GRANICE=1 is only the control for measuring the cap.
        string granica = Environment.GetEnvironmentVariable("BCF_BEZ_GRANICE") == "1" ? "" : " --disk-cache-size=" + NajviseCache + " --media-cache-size=" + NajviseCache;
        var opcije = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required" + granica);
        var env = await CoreWebView2Environment.CreateAsync(null, MapaProfila, opcije);
        await Pogled.EnsureCoreWebView2Async(env);
        if (Tiho) Pogled.CoreWebView2.IsMuted = true;
        Pogled.CoreWebView2.NavigationCompleted += async (s, e) =>
        {
            if (!e.IsSuccess || tekuce == null) { javi(L.T("the player did not load")); return; }
            await Task.Delay(700);
            await Skripta("var b=document.querySelector('#big_play_button'); if(b) b.click();");
            sviraSe = true;
            await Task.Delay(1200);
            await KaziNaslov(L.T("playing") + " ");
        };
        straza.Start();
        spreman = true;
    }

    public async void Sviraj(Bandcamp.Izdanje x)
    {
        try
        {
            await Pripremi();
            tekuce = x;
            sviraSe = false;
            javi(L.F("loading {0} - {1}", x.Izvodac, x.Naslov));
            Pogled.CoreWebView2.Navigate("https://bandcamp.com/EmbeddedPlayer/" + (x.Tip == "t" ? "track=" : "album=") + x.Id
                                         + "/size=small/bgcol=ffffff/linkcol=0687f5/transparent=true/");
        }
        catch (Exception e) { javi(L.F("the player cannot start: {0}", e.Message)); }
    }

    public async void PlayPauza()
    {
        if (!spreman || tekuce == null) { javi(L.T("nothing loaded; press Enter on a release")); return; }
        // On the page's audio element, not the play button. MEASURED 2026-10-04: on a
        // release with ONE track the button click started the track over instead of
        // pausing (paused=false, time back to 1.5 s). The state comes from the page, so
        // our idea of it can never drift from what is heard.
        string r = await Skripta("(function(){var a=document.querySelector('audio');if(!a)return '';"
                                 + "if(a.paused){a.play();return 'play';}a.pause();return 'pause';})()");
        if (r.Contains("play") && !r.Contains("pause")) { sviraSe = true; await KaziNaslov(L.T("playing") + " "); }
        else if (r.Contains("pause")) { sviraSe = false; javi(L.T("paused")); }
        else
        {
            // No audio element yet (the page still loading): the old way.
            await Skripta("var b=document.querySelector('#big_play_button'); if(b) b.click();");
            sviraSe = !sviraSe;
            if (sviraSe) await KaziNaslov(L.T("playing") + " "); else javi(L.T("paused"));
        }
    }

    /// <summary>F8, full stop (Gordan, 2026-10-04): pause and back to the start of the
    /// track, so play begins it again.</summary>
    public async void Stop()
    {
        if (!spreman || tekuce == null) { javi(L.T("nothing loaded; press Enter on a release")); return; }
        // Seek first, then pause, and pause again a moment later: MEASURED 2026-10-04, the
        // player resumes by itself after a seek, so pause-then-seek kept it playing.
        await Skripta("(function(){var a=document.querySelector('audio');if(a){a.currentTime=0;a.pause();}})()");
        await Task.Delay(350);
        await Skripta("(function(){var a=document.querySelector('audio');if(a&&!a.paused)a.pause();})()");
        sviraSe = false;
        javi(L.T("stopped"));
    }

    /// <summary>For measuring the cache: play at 16 times the speed, so whole tracks
    /// come down quickly.</summary>
    public async Task<string> BrzoZaProbu()
    {
        return await Skripta("(function(){var a=document.querySelector('audio');if(!a)return 'no audio';a.playbackRate=16;if(a.paused)a.play();"
                             + "var t=document.querySelector('#currenttitle_title');return (t?t.innerText:'?')+' t='+a.currentTime.toFixed(0);})()");
    }

    /// <summary>For measuring: which of the player's controls the page has.</summary>
    public async Task<string> KontroleZaProbu()
    {
        return await Skripta("(function(){return ['#big_play_button','.nextbutton','.prevbutton','#currenttitle_title','#currenttime','audio'].map(function(q){var e=document.querySelector(q);"
                             + "return q+'='+(e?(e.offsetParent!==null||q=='audio'?'vidljiv':'skriven'):'nema');}).join(', ')+' | visina='+document.body.scrollHeight;})()");
    }

    /// <summary>For measuring.</summary>
    public async Task<string> StopZaProbu() { Stop(); await Task.Delay(600); return await StanjeZaProbu(); }

    /// <summary>Seek by `sekundi` (Gordan, 2026-10-04: left and right arrows, as in
    /// NHR: 5 s, with Shift 30 s). Straight on the page's audio element; the new place
    /// is said as "1:35 of 4:10".</summary>
    public async void Premotaj(int sekundi)
    {
        if (!spreman || tekuce == null) { javi(L.T("nothing loaded; press Enter on a release")); return; }
        string r = await Skripta("(function(){var a=document.querySelector('audio');if(!a||!isFinite(a.duration))return '';"
                                 + "a.currentTime=Math.max(0,Math.min(a.duration-1,a.currentTime+(" + sekundi + ")));"
                                 + "return Math.floor(a.currentTime)+'/'+Math.floor(a.duration);})()");
        string s;
        try { s = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(r) ?? ""; } catch { s = ""; }
        var d = s.Split('/');
        int t, u;
        if (d.Length != 2 || !int.TryParse(d[0], out t) || !int.TryParse(d[1], out u)) { javi(L.T("nothing is playing")); return; }
        Func<int, string> mmss = x => (x / 60) + ":" + (x % 60).ToString("00");
        javi(L.F("{0} of {1}", mmss(t), mmss(u)));
    }

    /// <summary>For measuring: seek and return what the page says.</summary>
    public async Task<string> PremotajZaProbu(int sekundi)
    {
        await Skripta("(function(){var a=document.querySelector('audio');if(a&&isFinite(a.duration))a.currentTime=Math.max(0,Math.min(a.duration-1,a.currentTime+(" + sekundi + ")));})()");
        await Task.Delay(400);
        return await StanjeZaProbu();
    }

    public async void Sljedeca() { await Skok(".nextbutton"); }
    public async void Prethodna() { await Skok(".prevbutton"); }

    async Task Skok(string gumb)
    {
        if (!spreman || tekuce == null) { javi(L.T("nothing loaded; press Enter on a release")); return; }
        string prije = await Naslov();
        await Skripta("var b=document.querySelector('" + gumb + "'); if(b) b.click();");
        await Task.Delay(600);
        string poslije = await Naslov();
        if (poslije == prije) javi(L.T(gumb == ".nextbutton" ? "this is the last track" : "this is the first track"));
        else { sviraSe = true; javi(poslije); }
    }

    async Task OsvjeziNaslovnu()
    {
        if (!spreman || tekuce == null) return;
        string r = await Skripta("(function(){var n=document.querySelector('#currenttitle_tracknum'),t=document.querySelector('#currenttitle_title'),a=document.querySelector('#artist');"
                                 + "return [(n?n.innerText:'').trim(),(t?t.innerText:'').trim(),(a?a.innerText:'').trim()].join('\\u0001');})()");
        string s;
        try { s = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(r) ?? ""; } catch { return; }
        var d = s.Split('\u0001');
        if (d.Length < 3 || d[1].Length == 0) return;
        // The page writes "by Artist" in #artist; the release is the fallback.
        string izv = System.Text.RegularExpressions.Regex.Replace(d[2], @"^by\s+", "");
        if (izv.Length == 0) izv = tekuce.Izvodac;
        string nova = (d[0].Length > 0 ? d[0] + " " : "") + d[1] + " - " + izv;
        if (nova == zadnjaNaslovna) return;
        zadnjaNaslovna = nova;
        Naslovna(nova);
    }

    async Task KaziNaslov(string uvod)
    {
        string n = await Naslov();
        javi(n.Length > 0 ? uvod + n : uvod + (tekuce != null ? tekuce.Naslov : ""));
    }

    async Task<string> Naslov()
    {
        string r = await Skripta("(function(){var n=document.querySelector('#currenttitle_tracknum'),t=document.querySelector('#currenttitle_title');"
                                 + "return ((n?n.innerText:'')+' '+(t?t.innerText:'')).trim();})()");
        // ExecuteScriptAsync returns JSON: a string comes in quotes.
        try { return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(r) ?? ""; } catch { return ""; }
    }

    async Task<string> Skripta(string js)
    {
        try { return await Pogled.CoreWebView2.ExecuteScriptAsync(js); } catch { return "\"\""; }
    }

    // ---------------- MEDIA KEYS, anywhere in Windows ----------------

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    const int WmHotkey = 0x0312;
    static readonly uint[] Tipke = { 0xB3, 0xB0, 0xB1, 0xB2 };   // play/pause, next, previous, stop
    static readonly string[] Imena = { "play/pause", "next", "previous", "stop" };

    /// <summary>Takes the media keys for the whole of Windows. Another program (NHR!) may
    /// already hold them; that is said, not swallowed. Returns how many were taken.</summary>
    public int UzmiMedijskeTipke(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        var zauzete = new System.Collections.Generic.List<string>();
        int ok = 0;
        for (int i = 0; i < Tipke.Length; i++)
            if (RegisterHotKey(h, 0xB00 + i, 0, Tipke[i])) ok++; else zauzete.Add(Imena[i]);
        HwndSource.FromHwnd(h).AddHook((IntPtr hw, int msg, IntPtr wp, IntPtr lp, ref bool obradeno) =>
        {
            if (msg != WmHotkey) return IntPtr.Zero;
            int id = wp.ToInt32() - 0xB00;
            if (id == 0) PlayPauza(); else if (id == 3) Stop(); else if (id == 1) Sljedeca(); else if (id == 2) Prethodna();
            obradeno = true;
            return IntPtr.Zero;
        });
        w.Closed += (s, e) => { for (int i = 0; i < Tipke.Length; i++) UnregisterHotKey(h, 0xB00 + i); };
        if (zauzete.Count > 0) javi(L.F("media keys: another program holds {0}; F5 to F8 still work here", string.Join(", ", zauzete)));
        return ok;
    }
}
