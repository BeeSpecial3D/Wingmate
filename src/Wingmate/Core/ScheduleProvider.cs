using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Wingmate.Core
{
    /// <summary>Schedule = newest of bundled copy, local cache, and the online file (raw GitHub).</summary>
    public sealed class ScheduleProvider
    {
        public const string OnlineUrl = "https://raw.githubusercontent.com/BeeSpecial3D/Wingmate/main/schedule/schedule.json";
        private static readonly HttpClient Http = CreateHttp();
        private static string CachePath => Path.Combine(AppSettings.Dir, "schedule-cache.json");

        public ScheduleFile Current { get; private set; } = new();
        public string SourceLabel { get; private set; } = "built-in";
        public event Action? Changed;

        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("Wingmate/0.1 (+https://github.com/BeeSpecial3D/Wingmate)");
            return h;
        }

        public void LoadLocal()
        {
            using var st = Assembly.GetExecutingAssembly().GetManifestResourceStream("schedule.json")!;
            using var rd = new StreamReader(st, Encoding.UTF8);
            var bundled = ScheduleParser.TryParse(rd.ReadToEnd(), out var err) ?? throw new InvalidOperationException("Built-in schedule is invalid: " + err);
            Current = bundled; SourceLabel = "built-in";
            try
            {
                if (File.Exists(CachePath))
                {
                    var cache = ScheduleParser.TryParse(File.ReadAllText(CachePath, Encoding.UTF8), out var cerr);
                    if (cache == null) Log.Write("schedule cache ignored: " + cerr);
                    else if (string.CompareOrdinal(cache.Version, bundled.Version) >= 0) { Current = cache; SourceLabel = "saved online copy"; }
                }
            }
            catch (Exception ex) { Log.Write("schedule cache: " + ex.Message); }
        }

        public async Task RefreshOnlineAsync()
        {
            try
            {
                var json = await Http.GetStringAsync(OnlineUrl);
                var s = ScheduleParser.TryParse(json, out var err);
                if (s == null) { Log.Write("online schedule ignored: " + err); return; }
                if (string.CompareOrdinal(s.Version, Current.Version) < 0) return;
                SafeFile.WriteAtomic(CachePath, json, keepBackup: false); // a cache: whole or old, no .bak needed
                Current = s; SourceLabel = "online";
                Changed?.Invoke();
            }
            catch (Exception ex) { Log.Write("online schedule not available (using " + SourceLabel + "): " + ex.Message); }
        }
    }
}
