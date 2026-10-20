using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Wingmate
{
    /// <summary>Saving the character name "Test" (trimmed, any case) shows every label in every open window as runic gibberish,
    /// re-scrambled about once a second, for 10 seconds; then every text and font comes back exactly. Display only: it changes
    /// the text TextBlocks show (never TextBox contents, never the data behind them); nothing is saved. Fires only when the
    /// name is entered or saved, never on load. Windows opened or closed mid-effect are fine (each tick walks the open ones).</summary>
    public static class TestEgg
    {
        public static bool Enabled = true;
        private static readonly TimeSpan Length = TimeSpan.FromSeconds(10);
        private static DispatcherTimer? _timer;
        private static DateTime _start, _last;
        private static readonly Random Rnd = new();

        public static bool Matches(string? name) => string.Equals((name ?? "").Trim(), "Test", StringComparison.OrdinalIgnoreCase);

        /// <summary>Called right after a character name is entered/saved (name-first box, or renaming in the checklist).</summary>
        public static void MaybeRun(string? name)
        {
            if (!Enabled || !Matches(name) || _timer != null) return;
            try
            {
                _start = _last = DateTime.UtcNow;
                Gibberish.Scramble(Rnd);
                _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
                _timer.Tick += (s, e) =>
                {
                    try
                    {
                        var now = DateTime.UtcNow;
                        if (now - _start >= Length) { Stop(); return; }
                        if (now - _last >= TimeSpan.FromSeconds(1)) { _last = now; Gibberish.Scramble(Rnd); }
                    }
                    catch (Exception ex) { Core.Log.Write("text egg: " + ex.Message); Stop(); }
                };
                _timer.Start();
            }
            catch (Exception ex) { Core.Log.Write("text egg: " + ex.Message); Stop(); }
        }

        private static void Stop()
        {
            _timer?.Stop(); _timer = null;
            try { Gibberish.Restore(); } catch (Exception ex) { Core.Log.Write("text egg: " + ex.Message); }
        }

        internal static class Gibberish
        {
            private sealed class Saved { public string Original = ""; public string Shown = ""; public object LocalFont = DependencyProperty.UnsetValue; }
            private static readonly Dictionary<TextBlock, Saved> Blocks = new();
            private static readonly Dictionary<Run, Saved> Runs = new();
            private static readonly FontFamily Runic = new("Segoe UI Historic, Segoe UI Symbol, Segoe UI");

            public static void Scramble(Random rnd)
            {
                foreach (Window w in Application.Current.Windows.Cast<Window>().ToList())
                    if (w.IsVisible) Walk(w, rnd);
            }

            private static void Walk(DependencyObject d, Random rnd)
            {
                if (d is TextBox || d is PasswordBox) return; // what the user types is never touched
                if (d is TextBlock tb) Do(tb, rnd);
                if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                    for (int i = 0, n = VisualTreeHelper.GetChildrenCount(d); i < n; i++) Walk(VisualTreeHelper.GetChild(d, i), rnd);
            }

            private static bool IsSimple(TextBlock tb) => tb.Inlines.Count <= 1 && tb.Inlines.All(i => i is Run);

            private static void Do(TextBlock tb, Random rnd)
            {
                if (!Blocks.TryGetValue(tb, out var s))
                {
                    s = new Saved { LocalFont = tb.ReadLocalValue(TextBlock.FontFamilyProperty) };
                    Blocks[tb] = s;
                    tb.FontFamily = Runic;
                }
                if (IsSimple(tb))
                {
                    var cur = tb.Text ?? "";
                    if (cur != s.Shown) s.Original = cur; // first time, or the app changed it meanwhile
                    s.Shown = Garble(s.Original, rnd);
                    tb.SetCurrentValue(TextBlock.TextProperty, s.Shown);
                }
                else foreach (var r in AllRuns(tb.Inlines))
                {
                    if (!Runs.TryGetValue(r, out var rs)) Runs[r] = rs = new Saved();
                    if (r.Text != rs.Shown) rs.Original = r.Text;
                    rs.Shown = Garble(rs.Original, rnd);
                    r.SetCurrentValue(Run.TextProperty, rs.Shown);
                }
            }

            private static IEnumerable<Run> AllRuns(InlineCollection inlines)
            {
                foreach (var i in inlines.ToList())
                    if (i is Run r) yield return r;
                    else if (i is Span sp) foreach (var x in AllRuns(sp.Inlines)) yield return x;
            }

            public static void Restore()
            {
                foreach (var (tb, s) in Blocks)
                {
                    try
                    {
                        if (IsSimple(tb) && tb.Text == s.Shown)
                        {
                            var expr = BindingOperations.GetBindingExpressionBase(tb, TextBlock.TextProperty);
                            if (expr != null) expr.UpdateTarget(); else tb.SetCurrentValue(TextBlock.TextProperty, s.Original);
                        }
                        if (s.LocalFont == DependencyProperty.UnsetValue) tb.ClearValue(TextBlock.FontFamilyProperty);
                        else tb.SetValue(TextBlock.FontFamilyProperty, s.LocalFont);
                    }
                    catch (Exception ex) { Core.Log.Write("text egg: " + ex.Message); }
                }
                foreach (var (r, s) in Runs)
                    try { if (r.Text == s.Shown) r.SetCurrentValue(Run.TextProperty, s.Original); } catch { }
                Blocks.Clear(); Runs.Clear();
            }

            /// <summary>Same length, whitespace kept; everything else becomes a random rune (U+16A0..U+16EA).</summary>
            private static string Garble(string text, Random rnd)
            {
                var sb = new StringBuilder(text.Length);
                foreach (var ch in text) sb.Append(char.IsWhiteSpace(ch) || char.IsSurrogate(ch) ? ch : (char)rnd.Next(0x16A0, 0x16EB));
                return sb.ToString();
            }
        }

    }
}
