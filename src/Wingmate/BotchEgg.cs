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
    /// <summary>TEMPORARY easter egg for the 1.0.3 test build (remove in 1.0.4: delete this file and the two BotchEgg.MaybeRun
    /// calls in App.xaml.cs). Saving a character name "Botch" makes every open Wingmate window fade smoothly through rainbow colours (about one colour step a second)
    /// and shows every label as runic gibberish (like the other faction's chat in Aion) for 7 seconds, then puts back the
    /// user's theme and every text exactly. "Ezio" instead mirrors every window left to right with sparkles flashing over it
    /// for 7 seconds, then puts every window back. Display only: it swaps in-memory Th.* brushes and the text shown by TextBlocks
    /// (never TextBox contents, never the data behind them); nothing is saved and the theme setting never changes.
    /// Windows opened or closed mid-effect are fine: every tick looks at the windows open right then.</summary>
    public static class BotchEgg
    {
        public static bool Enabled = true; // the flag: false turns the egg off
        private static readonly TimeSpan Length = TimeSpan.FromSeconds(7);
        private static DispatcherTimer? _timer;

        private static bool Is(string? name, string egg) => string.Equals((name ?? "").Trim(), egg, StringComparison.OrdinalIgnoreCase);
        public static bool Matches(string? name) => Is(name, "Botch") || Is(name, "Ezio");

        /// <summary>Called after a character name is saved (name-first box or rename).</summary>
        public static void MaybeRun(string? name)
        {
            if (!Enabled) return;
            if (Is(name, "Botch")) RunBotch();
            else if (Is(name, "Ezio")) Mirror.Run();
        }

        /// <summary>Botch: rainbow colours + runic gibberish for 7 s.</summary>
        private static void RunBotch()
        {
            if (_timer != null) return;
            try
            {
                var res = Application.Current.Resources;
                var tokens = res.Keys.OfType<string>().Where(k => k.StartsWith("Th.") && res[k] is SolidColorBrush).ToList();
                var alpha = tokens.ToDictionary(k => k, k => ((SolidColorBrush)res[k]).Color.A);
                var rnd = new Random();
                var offset = tokens.ToDictionary(k => k, k => rnd.NextDouble() * 360);
                var start = DateTime.UtcNow;
                int tick = 0;
                _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(80) };
                _timer.Tick += (s, e) =>
                {
                    try
                    {
                        double t = (DateTime.UtcNow - start).TotalSeconds;
                        if (t >= Length.TotalSeconds) { Stop(); return; }
                        foreach (var k in tokens)
                        {
                            var b = new SolidColorBrush(Hsv((offset[k] + t * 60) % 360, Math.Max(alpha[k], (byte)0x88)));
                            b.Freeze();
                            res[k] = b; // DynamicResource users in every open (or newly opened) window repaint
                        }
                        if (++tick % 12 == 0) Gibberish.Scramble(rnd); // about once a second (new windows join then)
                    }
                    catch (Exception ex) { Core.Log.Write("botch: " + ex.Message); Stop(); }
                };
                _timer.Start();
                Gibberish.Scramble(rnd);
            }
            catch (Exception ex) { Core.Log.Write("botch: " + ex.Message); Stop(); }
        }

        /// <summary>Every text back first, then exactly the user's theme (re-applied from the saved theme id; nothing is written).</summary>
        private static void Stop()
        {
            _timer?.Stop();
            _timer = null;
            try { Gibberish.Restore(); } catch (Exception ex) { Core.Log.Write("botch text restore: " + ex.Message); }
            try { Th.Apply(App.Settings.Theme); App.Current.ApplyLook(); } catch (Exception ex) { Core.Log.Write("botch restore: " + ex.Message); }
        }

        private static Color Hsv(double h, byte a)
        {
            double x = 1 - Math.Abs(h / 60 % 2 - 1);
            (double r, double g, double b) = h < 60 ? (1d, x, 0d) : h < 120 ? (x, 1d, 0d) : h < 180 ? (0d, 1d, x) : h < 240 ? (0d, x, 1d) : h < 300 ? (x, 0d, 1d) : (1d, 0d, x);
            return Color.FromArgb(a, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        /// <summary>Runic stand-ins for every TextBlock's text (same length, spaces kept), remembering the real text. If the app
        /// sets a new text meanwhile (countdowns, refreshes), that becomes the text to restore. Bound texts are changed with
        /// SetCurrentValue, so their bindings stay; restore re-reads the binding.</summary>
        private static class Gibberish
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
                    catch (Exception ex) { Core.Log.Write("botch text: " + ex.Message); }
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

        /// <summary>Ezio: every open window's content flipped left to right (ScaleTransform -1) with gold sparkles flashing over
        /// it (drawn by an adorner, like the checklist celebration's glints) for 7 s; then the original transforms come back.
        /// Clicks keep working (WPF hit-tests through the transform). Windows opened mid-effect join in; closed ones are skipped.</summary>
        private static class Mirror
        {
            private sealed class Saved { public Transform? Transform; public Point Origin; public Sparkles? Fx; }
            private static readonly Dictionary<UIElement, Saved> Roots = new();
            private static DispatcherTimer? _t;
            private static DateTime _start;

            public static void Run()
            {
                if (_t != null) return;
                _start = DateTime.UtcNow;
                _t = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(50) };
                _t.Tick += (s, e) =>
                {
                    try
                    {
                        if ((DateTime.UtcNow - _start) >= Length) { Stop(); return; }
                        Apply();
                    }
                    catch (Exception ex) { Core.Log.Write("ezio: " + ex.Message); Stop(); }
                };
                _t.Start();
                try { Apply(); } catch (Exception ex) { Core.Log.Write("ezio: " + ex.Message); Stop(); }
            }

            private static void Apply()
            {
                foreach (Window w in Application.Current.Windows.Cast<Window>().ToList())
                {
                    if (!w.IsVisible || w.Content is not UIElement root || w.ActualWidth < 60) continue; // skip the tiny padlock windows
                    if (!Roots.TryGetValue(root, out var s))
                    {
                        s = new Saved { Transform = root.RenderTransform, Origin = root.RenderTransformOrigin };
                        Roots[root] = s;
                        root.RenderTransformOrigin = new Point(0.5, 0.5);
                        root.RenderTransform = new ScaleTransform(-1, 1);
                        var layer = AdornerLayer.GetAdornerLayer(root);
                        if (layer != null) { s.Fx = new Sparkles(root); layer.Add(s.Fx); }
                    }
                    s.Fx?.InvalidateVisual();
                }
            }

            private static void Stop()
            {
                _t?.Stop();
                _t = null;
                foreach (var (root, s) in Roots)
                {
                    try
                    {
                        if (s.Fx != null) AdornerLayer.GetAdornerLayer(root)?.Remove(s.Fx);
                        root.RenderTransform = s.Transform ?? Transform.Identity;
                        root.RenderTransformOrigin = s.Origin;
                    }
                    catch (Exception ex) { Core.Log.Write("ezio restore: " + ex.Message); }
                }
                Roots.Clear();
            }

            /// <summary>Twinkling four-point stars at random spots, redrawn every tick (no hit-testing, so clicks pass).</summary>
            private sealed class Sparkles : Adorner
            {
                private readonly Random _rnd = new();
                private static readonly Color[] Cols = { Color.FromRgb(0xFF, 0xD5, 0x8A), Color.FromRgb(0xFF, 0xF7, 0xE8), Color.FromRgb(0xFF, 0xFF, 0xA6), Color.FromRgb(0x9E, 0x6B, 0xF2) };
                public Sparkles(UIElement adorned) : base(adorned) { IsHitTestVisible = false; }

                protected override void OnRender(DrawingContext dc)
                {
                    var size = AdornedElement.RenderSize;
                    if (size.Width <= 0 || size.Height <= 0) return;
                    int n = (int)Math.Clamp(size.Width * size.Height / 4000, 8, 60);
                    for (int i = 0; i < n; i++)
                    {
                        var c = Cols[_rnd.Next(Cols.Length)];
                        double x = _rnd.NextDouble() * size.Width, y = _rnd.NextDouble() * size.Height, r = 3 + _rnd.NextDouble() * 7, a = 0.5 + _rnd.NextDouble() * 0.5;
                        var glow = new RadialGradientBrush(Color.FromArgb((byte)(200 * a), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B));
                        dc.DrawEllipse(glow, null, new Point(x, y), r * 0.45, r * 0.45);
                        dc.DrawEllipse(glow, null, new Point(x, y), r, r * 0.1);   // horizontal ray
                        dc.DrawEllipse(glow, null, new Point(x, y), r * 0.1, r);   // vertical ray
                        dc.DrawEllipse(Brushes.White, null, new Point(x, y), 1.1, 1.1);
                    }
                }
            }
        }
    }
}
