using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Applies a theme as app-wide brush resources "Th.&lt;token&gt;". XAML uses {DynamicResource Th.x} and code uses
    /// <see cref="Ref"/> so everything updates live; code that paints once per build reads <see cref="Br"/>/<see cref="C"/>.</summary>
    public static class Th
    {
        private static ThemeSet? _set;
        public static ThemeSet Set => _set ??= Load();
        public static ThemeSet.Theme Current { get; private set; } = null!;
        public static bool IsLight => ThemeSet.IsLight(Current);
        public static event Action? Changed;

        private static ThemeSet Load()
        {
            try
            {
                using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("themes.json");
                if (s != null) return ThemeSet.Parse(new StreamReader(s).ReadToEnd());
            }
            catch (Exception ex) { Log.Write("themes: " + ex.Message); }
            // never fail to start over a theme file: the original look, inline
            return ThemeSet.Parse("{\"current\":{\"name\":\"Current (Default)\",\"panelBg\":\"#E6141A24\",\"panelBorder\":\"#55A0B4D2\",\"divider\":\"#40A0B4D2\",\"title\":\"#FFFFD58A\",\"text\":\"#FFE6EEF7\",\"muted\":\"#AAE6EEF7\",\"faint\":\"#88E6EEF7\",\"struck\":\"#FF9BA3AE\",\"buttonBg\":\"#14FFFFFF\",\"buttonBorder\":\"#55A0B4D2\",\"buttonHoverBg\":\"#33FFFFFF\",\"buttonHoverBorder\":\"#AAFFD58A\",\"iconHoverBg\":\"#33FFFFFF\",\"iconStroke\":\"#AAE6EEF7\",\"iconHover\":\"#FFFFFFFF\",\"pillOnBg\":\"#33FFD58A\",\"pillOnBorder\":\"#FFFFD58A\",\"pillOnText\":\"#FFFFD58A\",\"badgeBg\":\"#335A6270\",\"link\":\"#FFFFD58A\",\"check\":\"#FF7CD992\",\"checkBoxOutline\":\"#88A0B4D2\",\"counter\":\"#FF7FB8FF\",\"live\":\"#FF7FB8FF\",\"liveText\":\"#FF9FD3FF\",\"menuBg\":\"#F2141A24\",\"menuBorder\":\"#55A0B4D2\",\"menuHover\":\"#22FFFFFF\",\"soonInsetBg\":\"#CC141A24\",\"soonInsetText\":\"#AAE6EEF7\",\"onBg\":\"#3348A0FF\",\"thumbHover\":\"#FFFFE6B8\",\"caret\":\"#FFFFFFFF\",\"soonIcon\":\"#AAE6EEF7\"}}");
        }

        /// <summary>Sets every Th.* resource (new frozen brushes, so DynamicResource users repaint) and notifies code-built views.</summary>
        public static void Apply(string? id)
        {
            Current = string.Equals((id ?? "").Trim(), CustomTheme.Id, StringComparison.OrdinalIgnoreCase) ? BuildCustom() : Set.Get(id);
            var res = Application.Current.Resources;
            foreach (var (token, hex) in Current.Colors)
            {
                var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                b.Freeze();
                res["Th." + token] = b;
            }
            Changed?.Invoke();
        }

        /// <summary>The Custom theme from the user's settings; its default warning colour is its start theme's.</summary>
        public static ThemeSet.Theme BuildCustom()
        {
            var s = App.Settings;
            var start = Set.Get(s.CustomBase);
            if (start.Colors.TryGetValue("warn", out var w) && w.Length == 9) AppSettings.ThemeWarnDefaults[CustomTheme.Id] = "#" + w[3..];
            return CustomTheme.Build(start, s.CustomColors);
        }

        public static Brush Br(string token) => (Brush)Application.Current.Resources["Th." + token];
        public static Color C(string token) => ((SolidColorBrush)Br(token)).Color;

        /// <summary>Live link from a property to a theme token (follows theme changes).</summary>
        public static T Ref<T>(this T e, DependencyProperty p, string token) where T : FrameworkElement
        {
            e.SetResourceReference(p, "Th." + token);
            return e;
        }
    }
}
