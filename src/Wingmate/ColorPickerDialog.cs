using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Small dark colour picker (hue / saturation / brightness sliders + hex), same look as the confirm dialogs.</summary>
    public sealed class ColorPickerDialog : Window
    {
        private static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        private readonly Slider _h, _s, _v;
        private readonly Border _swatch = new() { Height = 26, CornerRadius = new CornerRadius(5), BorderBrush = Th.Br("panelBorder"), BorderThickness = new Thickness(1) };
        private readonly TextBox _hex;
        private bool _sync;
        public string Result { get; private set; }

        private ColorPickerDialog(Window? owner, string start)
        {
            Result = WarnColors.Normalize(start);
            Title = "Wingmate"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.Height; Width = 280;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Foreground = Th.Br("text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (owner != null && owner.IsVisible) { Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = (Style)Application.Current.FindResource("CloseButton"), Focusable = false, ToolTip = "Cancel", Margin = new Thickness(8, -2, -6, 0) };
            close.Click += (s, e) => DialogResult = false;
            DockPanel.SetDock(close, Dock.Right); head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = "Warning color", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Th.Br("title") });
            root.Children.Add(head);
            root.Children.Add(new Border { Height = 1, Background = Th.Br("divider"), Margin = new Thickness(0, 0, 0, 8) });
            root.Children.Add(_swatch);

            _h = Row(root, "Hue", 360); _s = Row(root, "Saturation", 100); _v = Row(root, "Brightness", 100);
            var hexRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            hexRow.Children.Add(new TextBlock { Text = "Hex", Width = 74, VerticalAlignment = VerticalAlignment.Center });
            _hex = new TextBox { Width = 80, HorizontalAlignment = HorizontalAlignment.Left, Foreground = Th.Br("text"), Background = Th.Br("inputBg"),
                BorderBrush = Th.Br("focus"), BorderThickness = new Thickness(1), CaretBrush = Th.Br("caret"), Padding = new Thickness(3, 1, 3, 1), MaxLength = 7 };
            _hex.TextChanged += (s, e) => { if (!_sync && WarnColors.TryParse(_hex.Text) is { } c) SetColor(c, fromHex: true); };
            hexRow.Children.Add(_hex);
            root.Children.Add(hexRow);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "Use color", Style = (Style)Application.Current.FindResource("DialogButton"), Tag = "primary", Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            ok.Click += (s, e) => DialogResult = true;
            var cancel = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("DialogButton"), IsCancel = true };
            cancel.Click += (s, e) => DialogResult = false;
            buttons.Children.Add(ok); buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            Content = new Border { CornerRadius = new CornerRadius(10), Background = Th.Br("menuBg"), BorderBrush = Th.Br("panelBorder"),
                BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 14, 12), Child = root };
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            Loaded += (s, e) => Activate();
            SetColor(WarnColors.Parse(Result), fromHex: false);
        }

        private Slider Row(Panel root, string label, double max)
        {
            var g = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            g.Children.Add(new TextBlock { Text = label, Width = 74, VerticalAlignment = VerticalAlignment.Center });
            var sl = new Slider { Style = (Style)Application.Current.FindResource("DarkSlider"), Minimum = 0, Maximum = max, VerticalAlignment = VerticalAlignment.Center };
            sl.ValueChanged += (s, e) => { if (!_sync) FromSliders(); };
            g.Children.Add(sl);
            root.Children.Add(g);
            return sl;
        }

        private void FromSliders()
        {
            var c = Hsv(_h.Value, _s.Value / 100, _v.Value / 100);
            Show(c, updateHex: true);
        }

        private void SetColor((byte R, byte G, byte B) c, bool fromHex)
        {
            ToHsv(c, out var h, out var s, out var v);
            _sync = true;
            _h.Value = h; _s.Value = s * 100; _v.Value = v * 100;
            _sync = false;
            Show(c, updateHex: !fromHex);
        }

        private void Show((byte R, byte G, byte B) c, bool updateHex)
        {
            Result = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            _swatch.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            if (updateHex) { _sync = true; _hex.Text = Result; _sync = false; }
        }

        private static (byte, byte, byte) Hsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            (double r, double g, double b) = h < 60 ? (c, x, 0d) : h < 120 ? (x, c, 0d) : h < 180 ? (0d, c, x) : h < 240 ? (0d, x, c) : h < 300 ? (x, 0d, c) : (c, 0d, x);
            return ((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        private static void ToHsv((byte R, byte G, byte B) c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
            if (h < 0) h += 360;
            s = max == 0 ? 0 : d / max; v = max;
        }

        /// <summary>Returns the picked "#RRGGBB", or null if cancelled.</summary>
        public static string? Pick(Window? owner, string start)
        {
            var d = new ColorPickerDialog(owner, start);
            return d.ShowDialog() == true ? d.Result : null;
        }
    }
}
