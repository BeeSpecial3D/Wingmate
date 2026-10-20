using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Wingmate's colour picker: big preview swatch, a saturation/brightness square (white top-left, pure hue
    /// top-right, black along the bottom) and a rainbow hue slider, both with round handles, plus a HEX box (Enter applies)
    /// with a copy button. OK returns the colour, Cancel / Escape returns nothing (so the colour stays as it was).
    /// Only ordinary mouse events on its own window; remembers where it was.</summary>
    public sealed class ColorPickerDialog : Window
    {
        private const double PreviewW = 130, SquareW = 270, AreaH = 170, HueH = 10, Knob = 18;
        private static AppSettings S => App.Settings;

        private double _h, _s, _v; // the picker works in HSV so the hue survives greys and black
        private readonly Border _swatch = new() { Width = PreviewW, Height = AreaH };
        private readonly Border _hueLayer = new() { Width = SquareW, Height = AreaH };
        private readonly Canvas _squareCanvas = new() { Width = SquareW, Height = AreaH, ClipToBounds = false, Background = Brushes.Transparent, Cursor = Cursors.Cross };
        private readonly Ellipse _squareKnob = MakeKnob();
        private readonly Canvas _hueCanvas = new() { Height = Knob, ClipToBounds = false, Background = Brushes.Transparent, Cursor = Cursors.Hand };
        private readonly Ellipse _hueKnob = MakeKnob();
        private readonly TextBox _hex;
        public string Result { get; private set; }

        private readonly Action<string>? _preview;
        private readonly System.Windows.Threading.DispatcherTimer _previewTimer;
        private string _previewed;

        private ColorPickerDialog(Window? owner, string start, Action<string>? preview)
        {
            _preview = preview;
            _previewed = WarnColors.Normalize(start);
            // live preview across the app, coalesced to ~30 repaints a second while dragging
            _previewTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _previewTimer.Tick += (s, e) => { _previewTimer.Stop(); PushPreview(); };
            Result = WarnColors.Normalize(start);
            (_h, _s, _v) = ColorMath.ToHsv(ColorMath.ParseHex(Result)!.Value);

            Title = "Wingmate - Color picker"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Foreground = Th.Br("text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (owner != null && owner.IsVisible) Owner = owner;
            if (WindowSnap.TryRestore(this, S.ColorPickerLeft, S.ColorPickerTop)) { }
            else if (Owner != null) WindowStartupLocation = WindowStartupLocation.CenterOwner;
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new StackPanel();
            root.Children.Add(new TextBlock { Text = "Color picker", FontSize = 16, Foreground = Th.Br("title"), Margin = new Thickness(16, 10, 16, 10) });

            // preview | saturation/value square (full width, like the reference)
            var area = new StackPanel { Orientation = Orientation.Horizontal };
            area.Children.Add(_swatch);
            var square = new Grid { Width = SquareW, Height = AreaH };
            square.Children.Add(_hueLayer);
            square.Children.Add(new Border { Background = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) });
            square.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) });
            _squareCanvas.Children.Add(_squareKnob);
            square.Children.Add(_squareCanvas);
            area.Children.Add(square);
            root.Children.Add(area);
            Drag(_squareCanvas, p =>
            {
                _s = Math.Clamp(p.X / SquareW, 0, 1);
                _v = 1 - Math.Clamp(p.Y / AreaH, 0, 1);
                Update(true);
            });

            // hue slider
            var hueGrid = new Grid { Margin = new Thickness(24, 14, 24, 10), Width = PreviewW + SquareW - 48 };
            var rainbow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            foreach (var (hex, at) in new[] { ("#FF0000", 0.0), ("#FFFF00", 1 / 6.0), ("#00FF00", 2 / 6.0), ("#00FFFF", 3 / 6.0), ("#0000FF", 4 / 6.0), ("#FF00FF", 5 / 6.0), ("#FF0000", 1.0) })
                rainbow.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(hex), at));
            hueGrid.Children.Add(new Border { Height = HueH, CornerRadius = new CornerRadius(HueH / 2), Background = rainbow, VerticalAlignment = VerticalAlignment.Center });
            _hueCanvas.Children.Add(_hueKnob);
            hueGrid.Children.Add(_hueCanvas);
            root.Children.Add(hueGrid);
            Drag(_hueCanvas, p =>
            {
                _h = Math.Clamp(p.X / Math.Max(1, _hueCanvas.ActualWidth), 0, 1) * 360;
                if (_h >= 360) _h = 359.999;
                Update(true);
            });

            // HEX box with the label sitting on its top border, copy button on the right
            var hexHost = new Grid { Margin = new Thickness(16, 0, 16, 0) };
            var hexBorder = new Border { BorderBrush = Th.Br("panelBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 8, 0, 0), Padding = new Thickness(8, 8, 6, 6) };
            var hexRow = new Grid();
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hexRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            _hex = new TextBox { MaxLength = 7, FontSize = 13, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Th.Br("text"), CaretBrush = Th.Br("caret"),
                VerticalAlignment = VerticalAlignment.Center, ToolTip = "Type a hex color like #FC0303, then Enter" };
            _hex.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { ApplyHex(); e.Handled = true; }
                else if (e.Key == Key.Escape) { Update(false); Keyboard.ClearFocus(); e.Handled = true; }
            };
            _hex.LostKeyboardFocus += (s, e) => ApplyHex();
            _hex.TextChanged += (s, e) =>
            {
                // live while typing: a complete #RRGGBB shows right away (the box keeps what the user typed)
                var t = _hex.Text.Trim().TrimStart('#');
                if (_hex.IsKeyboardFocused && t.Length == 6 && ColorMath.ParseHex(t) is { } c && ColorMath.Hex(c) != Result)
                {
                    var (h, sat, v) = ColorMath.ToHsv(c);
                    if (sat > 0 && v > 0) _h = h;
                    _s = sat; _v = v; Result = ColorMath.Hex(c);
                    Update(false);
                }
            };
            Grid.SetColumn(_hex, 1); hexRow.Children.Add(_hex);
            var copy = CopyButton();
            Grid.SetColumn(copy, 2); hexRow.Children.Add(copy);
            hexBorder.Child = hexRow;
            hexHost.Children.Add(hexBorder);
            hexHost.Children.Add(new Border { Background = Th.Br("menuBg"), Padding = new Thickness(5, 0, 5, 0), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = "HEX", FontSize = 11, Foreground = Th.Br("text") } });
            root.Children.Add(hexHost);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 12, 16, 0) };
            var ok = new Button { Content = "OK", Style = (Style)Application.Current.FindResource("DialogButton"), Tag = "primary", Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            ok.Click += (s, e) => { ApplyHex(); DialogResult = true; };
            var cancel = new Button { Content = "Cancel", Style = (Style)Application.Current.FindResource("DialogButton"), IsCancel = true };
            cancel.Click += (s, e) => DialogResult = false;
            buttons.Children.Add(ok); buttons.Children.Add(cancel);
            root.Children.Add(buttons);

            Content = new Border { CornerRadius = new CornerRadius(10), Background = Th.Br("menuBg"), BorderBrush = Th.Br("panelBorder"),
                BorderThickness = new Thickness(1), Padding = new Thickness(0, 0, 0, 12), Child = root };
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            Closing += (s, e) => SavePosition();
            Loaded += (s, e) => { FontScaler.Apply(this); Activate(); Update(false); };
            _hueCanvas.SizeChanged += (s, e) => Update(false);
            Update(false);
        }

        private static Ellipse MakeKnob() => new()
        {
            Width = Knob, Height = Knob, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false,
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.6 },
        };

        /// <summary>Press, drag and release on one of our own canvases (mouse capture on that element only).</summary>
        private static void Drag(FrameworkElement el, Action<Point> at)
        {
            el.MouseLeftButtonDown += (s, e) => { el.CaptureMouse(); at(e.GetPosition(el)); e.Handled = true; };
            el.MouseMove += (s, e) => { if (el.IsMouseCaptured) at(e.GetPosition(el)); };
            el.MouseLeftButtonUp += (s, e) => { if (el.IsMouseCaptured) el.ReleaseMouseCapture(); e.Handled = true; };
        }

        private FrameworkElement CopyButton()
        {
            var icon = new Canvas { Width = 16, Height = 18 };
            var back = new Rectangle { Width = 11, Height = 13, RadiusX = 1.5, RadiusY = 1.5, StrokeThickness = 1.6 };
            var front = new Rectangle { Width = 11, Height = 13, RadiusX = 1.5, RadiusY = 1.5, StrokeThickness = 1.6 };
            Canvas.SetLeft(back, 0); Canvas.SetTop(back, 0);
            Canvas.SetLeft(front, 4); Canvas.SetTop(front, 4);
            icon.Children.Add(back); icon.Children.Add(front);
            var b = new Border { Child = icon, Background = Brushes.Transparent, Cursor = Cursors.Hand, Padding = new Thickness(4, 2, 4, 2),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Copy" };
            void Paint(bool hot)
            {
                var br = hot ? Th.Br("iconHover") : Th.Br("iconStroke");
                back.Stroke = br; front.Stroke = br; front.Fill = Th.Br("menuBg");
            }
            Paint(false);
            b.MouseEnter += (s, e) => Paint(true);
            b.MouseLeave += (s, e) => { Paint(false); b.ToolTip = "Copy"; };
            b.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                try { Clipboard.SetText(Result.ToLowerInvariant()); b.ToolTip = "Copied"; }
                catch { b.ToolTip = "Couldn't copy"; }
            };
            return b;
        }

        private void ApplyHex()
        {
            if (ColorMath.ParseHex(_hex.Text) is { } c && ColorMath.Hex(c) != Result)
            {
                var (h, s, v) = ColorMath.ToHsv(c);
                if (s > 0 && v > 0) _h = h; // greys/black keep the hue the user was on
                _s = s; _v = v;
                Result = ColorMath.Hex(c);
            }
            Update(false);
        }

        /// <summary>Repaints from _h/_s/_v. fromHandles: the colour came from dragging, so recompute Result.</summary>
        private void Update(bool fromHandles)
        {
            if (fromHandles) Result = ColorMath.Hex(ColorMath.FromHsv(_h, _s, _v));
            var c = ColorMath.ParseHex(Result)!.Value;
            _swatch.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            var pure = ColorMath.FromHsv(_h, 1, 1);
            _hueLayer.Background = new SolidColorBrush(Color.FromRgb(pure.R, pure.G, pure.B));
            _squareKnob.Fill = _swatch.Background;
            Canvas.SetLeft(_squareKnob, _s * SquareW - Knob / 2);
            Canvas.SetTop(_squareKnob, (1 - _v) * AreaH - Knob / 2);
            _hueKnob.Fill = _hueLayer.Background;
            Canvas.SetLeft(_hueKnob, _h / 360 * _hueCanvas.ActualWidth - Knob / 2);
            Canvas.SetTop(_hueKnob, 0);
            if (!_hex.IsKeyboardFocused || fromHandles) _hex.Text = Result.ToLowerInvariant();
            if (_preview != null && Result != _previewed && !_previewTimer.IsEnabled) _previewTimer.Start();
        }

        private void PushPreview()
        {
            if (_preview == null || Result == _previewed || !IsVisible) return;
            _previewed = Result;
            try { _preview(Result); } catch (Exception ex) { Log.Write("picker preview: " + ex.Message); }
        }

        private void SavePosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return;
            double l = Math.Round(Left), t = Math.Round(Top);
            if (S.ColorPickerLeft == l && S.ColorPickerTop == t) return;
            S.ColorPickerLeft = l; S.ColorPickerTop = t;
            S.Save();
        }

        /// <summary>Returns the picked "#RRGGBB", or null if cancelled. <paramref name="preview"/> (optional) gets every colour
        /// while the user drags or types; the caller undoes it afterwards (the picker never saves anything itself).</summary>
        public static string? Pick(Window? owner, string start, Action<string>? preview = null)
        {
            var d = new ColorPickerDialog(owner, start, preview);
            d.Closed += (s, e) => { d._previewTimer.Stop(); if (_open == d) _open = null; };
            _open = d;
            return d.ShowDialog() == true ? d.Result : null;
        }

        private static ColorPickerDialog? _open;

        /// <summary>Closes an open picker as Cancel (the caller then undoes the live preview, as for the Cancel button).</summary>
        public static void CancelOpen()
        {
            var d = _open;
            if (d == null) return;
            try { d.DialogResult = false; } catch (InvalidOperationException) { d.Close(); }
        }
    }
}
