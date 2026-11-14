using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Wingmate.Core
{
    /// <summary>coupons.json: the free Aion 2 coupon codes Wingmate tells people about (added in 1.0.9).</summary>
    public sealed class CouponFile
    {
        public int FormatVersion { get; set; }
        /// <summary>Newer wins (compared as text, like the schedule): "2026-11-14.1".</summary>
        public string Version { get; set; } = "";
        public string? Updated { get; set; }
        public List<CouponCode> Codes { get; set; } = new();
    }

    public sealed class CouponCode
    {
        /// <summary>Stable id; dismissed codes are remembered by it. Missing = the code itself.</summary>
        public string Id { get; set; } = "";
        /// <summary>What to type in game.</summary>
        public string Code { get; set; } = "";
        /// <summary>What it gives, one line each, e.g. "Customization Voucher x5".</summary>
        public List<string> Rewards { get; set; } = new();
        /// <summary>Short plain note, e.g. "Once per account. Works on all servers."</summary>
        public string? Note { get; set; }
        /// <summary>When it starts working (ISO 8601 with offset, e.g. "2026-11-14T00:00:00Z"); missing = already working.</summary>
        public string? Starts { get; set; }
        /// <summary>When it stops working (ISO 8601 with offset, e.g. "2026-11-25T06:30:00Z"); missing = no end date known.</summary>
        public string? Expires { get; set; }

        public DateTime? StartsUtc => Coupons.ParseUtc(Starts);
        public DateTime? ExpiresUtc => Coupons.ParseUtc(Expires);
    }

    public static class Coupons
    {
        public const string RedeemWhere = "Settings > Miscellaneous > Account > Enter Coupon";
        public const int MaxRemembered = 300;

        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>ISO 8601 time with a zone ("Z" or "+09:00") to UTC; a time without a zone counts as UTC. Null if invalid.</summary>
        public static DateTime? ParseUtc(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            return DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
                ? d.UtcDateTime : null;
        }

        /// <summary>Reads coupons.json. Drops codes without code text or with an unreadable date (a typo never shows a code
        /// forever), trims text, fills missing ids and drops duplicate ids. An empty list is valid (no codes right now).</summary>
        public static CouponFile? TryParse(string json, out string error)
        {
            error = "";
            try
            {
                var f = JsonSerializer.Deserialize<CouponFile>(json, Opts);
                if (f == null) { error = "empty"; return null; }
                if (f.FormatVersion != 1) { error = "unsupported formatVersion " + f.FormatVersion; return null; }
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var keep = new List<CouponCode>();
                foreach (var c in f.Codes ?? new())
                {
                    if (c == null) continue;
                    c.Code = (c.Code ?? "").Trim();
                    if (c.Code.Length == 0 || c.Code.Length > 40) continue;
                    if (!string.IsNullOrWhiteSpace(c.Starts) && c.StartsUtc == null) continue;
                    if (!string.IsNullOrWhiteSpace(c.Expires) && c.ExpiresUtc == null) continue;
                    c.Id = string.IsNullOrWhiteSpace(c.Id) ? c.Code : c.Id.Trim();
                    if (!ids.Add(c.Id)) continue;
                    c.Rewards = (c.Rewards ?? new()).Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
                    c.Note = string.IsNullOrWhiteSpace(c.Note) ? null : c.Note.Trim();
                    keep.Add(c);
                }
                f.Codes = keep;
                f.Version ??= "";
                return f;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        /// <summary>Codes that work right now: started, and not expired (a code is gone from the moment it expires).</summary>
        public static List<CouponCode> Active(CouponFile? f, DateTime nowUtc) =>
            (f?.Codes ?? new()).Where(c => (c.StartsUtc is not DateTime s || s <= nowUtc) && (c.ExpiresUtc is not DateTime e || nowUtc < e)).ToList();

        /// <summary>Working codes the user hasn't closed yet (what the pop-up shows on its own, once each).</summary>
        public static List<CouponCode> New(CouponFile? f, IEnumerable<string>? dismissed, DateTime nowUtc)
        {
            var seen = new HashSet<string>(dismissed ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return Active(f, nowUtc).Where(c => !seen.Contains(c.Id)).ToList();
        }

        /// <summary>Adds ids to the remembered list (never removes one the user closed, no duplicates). Very old entries
        /// fall off only past <see cref="MaxRemembered"/>. Returns true if anything was added.</summary>
        public static bool Remember(List<string> dismissed, IEnumerable<string> ids)
        {
            bool added = false;
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || dismissed.Contains(id, StringComparer.OrdinalIgnoreCase)) continue;
                dismissed.Add(id.Trim());
                added = true;
            }
            if (dismissed.Count > MaxRemembered) dismissed.RemoveRange(0, dismissed.Count - MaxRemembered);
            return added;
        }

        /// <summary>Repairs a saved list: no nulls, blanks or duplicates.</summary>
        public static List<string> Sanitize(List<string>? list)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var r = (list ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Where(seen.Add).ToList();
            if (r.Count > MaxRemembered) r.RemoveRange(0, r.Count - MaxRemembered);
            return r;
        }

        /// <summary>The newer of two files (by Version); a is kept on a tie or when b is missing.</summary>
        public static CouponFile? Newer(CouponFile? a, CouponFile? b) =>
            a == null ? b : b == null ? a : string.CompareOrdinal(b.Version, a.Version) > 0 ? b : a;

        /// <summary>"Expires Tue, Nov 25 at 1:30 AM EST" in the user's own time zone ("today" / "tomorrow" when close).</summary>
        public static string ExpiryText(CouponCode c, DateTime nowUtc, TimeZoneInfo? tz = null)
        {
            if (c.ExpiresUtc is not DateTime e) return "No end date announced";
            tz ??= TimeZoneInfo.Local;
            var local = TimeZoneInfo.ConvertTimeFromUtc(e, tz);
            var today = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz).Date;
            string day = local.Date == today ? "today" : local.Date == today.AddDays(1) ? "tomorrow"
                : local.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
            return $"Expires {day} at {local.ToString("h:mm tt", CultureInfo.InvariantCulture)} {Abbrev(tz, e)}";
        }

        /// <summary>Short zone label like "EST" (same rule as the alerts' "times in EST"), or "local".</summary>
        public static string Abbrev(TimeZoneInfo tz, DateTime utc)
        {
            var name = tz.IsDaylightSavingTime(utc) ? tz.DaylightName : tz.StandardName;
            var words = (name ?? "").Replace(".", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words.All(w => w.All(ch => ch < 128 && char.IsLetter(ch))))
                return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            return "local";
        }
    }
}
