using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Wingmate
{
    /// <summary>Applies the Font size setting by scaling every explicitly sized text element from its original size.</summary>
    public static class FontScaler
    {
        private sealed class Base { public double Size; }
        private static readonly ConditionalWeakTable<DependencyObject, Base> Bases = new();
        private static readonly ConditionalWeakTable<FrameworkElement, Base> Widths = new();

        /// <summary>Marks an element whose fixed Width should grow with the font size too (a window or a box sized for its text),
        /// so bigger text never gets clipped. Uses the Width it has now as the base.</summary>
        public static T Grow<T>(T el) where T : FrameworkElement
        {
            if (!Widths.TryGetValue(el, out _) && !double.IsNaN(el.Width)) Widths.Add(el, new Base { Size = el.Width });
            return el;
        }

        /// <summary>Applies the current size to one element (and what is inside it) and hands it back; for controls made on the fly.</summary>
        public static T Scaled<T>(T el) where T : DependencyObject { Apply(el); return el; }

        public static double Scale => App.Settings.FontScale is > 0.5 and < 2 ? App.Settings.FontScale : 1.0;

        public static void Apply(DependencyObject root)
        {
            double k = Scale;
            Walk(root, k);
        }

        private static void Walk(DependencyObject d, double k)
        {
            DependencyProperty? prop = d switch { Control => Control.FontSizeProperty, TextBlock => TextBlock.FontSizeProperty, TextElement => TextElement.FontSizeProperty, _ => null };
            if (prop != null)
            {
                if (!Bases.TryGetValue(d, out var b))
                {
                    var src = DependencyPropertyHelper.GetValueSource(d, prop).BaseValueSource;
                    if (src != BaseValueSource.Inherited && src != BaseValueSource.Default)
                    {
                        b = new Base { Size = (double)d.GetValue(prop) };
                        Bases.Add(d, b);
                    }
                }
                if (b != null) d.SetValue(prop, b.Size * k);
            }
            if (d is FrameworkElement fe && Widths.TryGetValue(fe, out var w)) fe.Width = System.Math.Round(w.Size * k);
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
            {
                int n = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(d, i), k);
            }
            if (d is TextBlock tb) foreach (var inl in tb.Inlines) Walk(inl, k);
            else if (d is Span sp) foreach (var inl in sp.Inlines) Walk(inl, k);
        }
    }
}
