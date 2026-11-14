using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Wingmate.Core
{
    /// <summary>Free codes = newest of the bundled list, the saved online copy and the online file (raw GitHub, same repo
    /// and folder as the schedule). Offline or a bad download just keeps what we have; it never throws.</summary>
    public sealed class CouponProvider
    {
        public const string OnlineUrl = "https://raw.githubusercontent.com/BeeSpecial3D/Wingmate/main/schedule/coupons.json";
        private static readonly HttpClient Http = CreateHttp();
        private static string CachePath => Path.Combine(AppSettings.Dir, "coupons-cache.json");

        public CouponFile Current { get; private set; } = new() { FormatVersion = 1 };
        public string SourceLabel { get; private set; } = "built-in";
        /// <summary>Raised after the online list was read (the caller decides what is new to the user).</summary>
        public event Action? Changed;

        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Wingmate/1.0 (+https://github.com/BeeSpecial3D/Wingmate)");
            return h;
        }

        public void LoadLocal()
        {
            try
            {
                using var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("coupons.json");
                if (st != null)
                {
                    using var rd = new StreamReader(st, Encoding.UTF8);
                    var bundled = Coupons.TryParse(rd.ReadToEnd(), out var err);
                    if (bundled != null) { Current = bundled; SourceLabel = "built-in"; }
                    else Log.Write("built-in coupons ignored: " + err);
                }
            }
            catch (Exception ex) { Log.Write("built-in coupons: " + ex.Message); }
            try
            {
                if (File.Exists(CachePath))
                {
                    var cache = Coupons.TryParse(File.ReadAllText(CachePath, Encoding.UTF8), out var cerr);
                    if (cache == null) Log.Write("coupons cache ignored: " + cerr);
                    else if (string.CompareOrdinal(cache.Version, Current.Version) >= 0) { Current = cache; SourceLabel = "saved online copy"; }
                }
            }
            catch (Exception ex) { Log.Write("coupons cache: " + ex.Message); }
        }

        public async Task RefreshOnlineAsync()
        {
            try
            {
                var json = await Http.GetStringAsync(OnlineUrl);
                var f = Coupons.TryParse(json, out var err);
                if (f == null) { Log.Write("online coupons ignored: " + err); return; }
                if (string.CompareOrdinal(f.Version, Current.Version) < 0) return; // older than what we have
                SafeFile.WriteAtomic(CachePath, json, keepBackup: false); // a cache: whole or old, no .bak needed
                Current = f; SourceLabel = "online";
                Changed?.Invoke();
            }
            catch (Exception ex) { Log.Write("online coupons not available (using " + SourceLabel + "): " + ex.Message); }
        }
    }
}
