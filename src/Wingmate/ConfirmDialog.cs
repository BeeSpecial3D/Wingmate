using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Wingmate
{
    public enum ConfirmKind { Primary, Danger }

    /// <summary>Dark borderless replacement for MessageBox. Cancel is the safe default (Enter/Escape/×).</summary>
    public sealed class ConfirmDialog : Window
    {
        private static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;

        private readonly CheckBox? _check;

        private ConfirmDialog(Window? owner, string title, string message, string? okText, string cancelText, ConfirmKind kind, string? checkText = null)
        {
            Title = "Wingmate"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = owner == null; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; MaxWidth = 380;
            // taskbar icon (ownerless) stays the tiled one; the exe's own file icon is the transparent cut-out
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new System.Uri("pack://application:,,,/Assets/wingmate.ico")); } catch { }
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; Foreground = Th.Br("text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (owner != null && owner.IsVisible) { Owner = owner; WindowStartupLocation = WindowStartupLocation.CenterOwner; }
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new DockPanel { MinWidth = 260 };
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var close = new Button { Style = (Style)Application.Current.FindResource("CloseButton"), Focusable = false, ToolTip = "Cancel",
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -2, -6, 0) };
            close.Click += (s, e) => { DialogResult = false; };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Th.Br("title"),
                TextTrimming = TextTrimming.CharacterEllipsis });
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);

            var sep = new Border { Height = 1, Background = Th.Br("divider"), Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(sep, Dock.Top); root.Children.Add(sep);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var btnStyle = (Style)Application.Current.FindResource("DialogButton");
            if (okText != null)
            {
                var ok = new Button { Content = okText, Style = btnStyle, Tag = kind == ConfirmKind.Danger ? "danger" : "primary", Margin = new Thickness(0, 0, 8, 0) };
                ok.Click += (s, e) => { DialogResult = true; };
                buttons.Children.Add(ok);
            }
            var cancel = new Button { Content = cancelText, Style = btnStyle, IsDefault = true, IsCancel = true };
            if (okText == null) cancel.Tag = "primary";
            cancel.Click += (s, e) => { DialogResult = false; };
            buttons.Children.Add(cancel);
            DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            if (checkText != null)
            {
                _check = new CheckBox { Content = checkText, Style = (Style)Application.Current.FindResource("DarkCheck"), Focusable = false, Margin = new Thickness(0, 10, 0, 0) };
                DockPanel.SetDock(_check, Dock.Bottom); root.Children.Add(_check);
            }

            root.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = Th.Br("text"), LineHeight = 17 });

            Content = new Border { CornerRadius = new CornerRadius(10), Background = Th.Br("menuBg"), BorderBrush = Th.Br("panelBorder"),
                BorderThickness = new Thickness(1), Padding = new Thickness(14, 8, 14, 12), Child = root };

            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            Loaded += (s, e) => { Activate(); cancel.Focus(); };
        }

        /// <summary>Returns true only if the user chose the action button.</summary>
        public static bool Ask(Window? owner, string title, string message, string okText, ConfirmKind kind)
            => new ConfirmDialog(owner, title, message, okText, "Cancel", kind).ShowDialog() == true;

        /// <summary>Like Ask, plus a checkbox under the message (e.g. "Don't ask again"); checkedOut is its final state.</summary>
        public static bool AskWithCheck(Window? owner, string title, string message, string okText, ConfirmKind kind, string checkText, out bool checkedOut)
        {
            var d = new ConfirmDialog(owner, title, message, okText, "Cancel", kind, checkText);
            bool ok = d.ShowDialog() == true;
            checkedOut = d._check?.IsChecked == true;
            return ok;
        }

        public static void Info(Window? owner, string title, string message)
            => new ConfirmDialog(owner, title, message, null, "OK", ConfirmKind.Primary).ShowDialog();
    }
}
