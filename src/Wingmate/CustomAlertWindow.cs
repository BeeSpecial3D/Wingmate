using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Add or edit one custom alert (same pattern as the color picker: a small themed dialog, Save / Cancel, nothing
    /// is saved until Save). Editing also offers Delete. The caller stores the result in App.Settings.CustomAlerts.</summary>
    public sealed class CustomAlertWindow : Window
    {
        private static AppSettings S => App.Settings;
        public enum Outcome { Cancelled, Saved, Deleted }

        private readonly CustomAlert _alert;
        private readonly TextBox _name, _time, _repeatHours;
        private readonly CheckBox _everyDay, _repeat, _on;
        private readonly List<(string Day, RadioButton Pill)> _days = new();
        private readonly TextBlock _problem;
        public Outcome Result { get; private set; } = Outcome.Cancelled;

        private CustomAlertWindow(Window? owner, CustomAlert? existing, string regionName)
        {
            bool editing = existing != null;
            _alert = existing?.Clone() ?? new CustomAlert { Name = "", Time = "20:00" };

            Title = "Wingmate - Custom alert"; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize; Width = 330; SizeToContent = SizeToContent.Height;
            FontScaler.Grow(this);
            FontFamily = new FontFamily("Segoe UI"); FontSize = 12; this.Ref(ForegroundProperty, "text");
            UseLayoutRounding = true; TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            if (owner != null && owner.IsVisible) Owner = owner;
            if (WindowSnap.TryRestore(this, S.CustomAlertLeft, S.CustomAlertTop)) { }
            else if (Owner != null) WindowStartupLocation = WindowStartupLocation.CenterOwner;
            else WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new StackPanel { Margin = new Thickness(16, 10, 16, 0) };
            root.Children.Add(new TextBlock { Text = editing ? "Edit custom alert" : "Add custom alert", FontSize = 16, Margin = new Thickness(0, 0, 0, 10) }
                .Ref(TextBlock.ForegroundProperty, "title"));

            root.Children.Add(Label("Name"));
            _name = Box(_alert.Name, CustomAlerts.MaxName);
            _name.HorizontalAlignment = HorizontalAlignment.Stretch;
            root.Children.Add(_name);

            root.Children.Add(Label("Time"));
            var timeRow = new StackPanel { Orientation = Orientation.Horizontal };
            _time = Box(CustomAlerts.NormalizeTime(_alert.Time) ?? _alert.Time, 5);
            _time.Width = 56; _time.TextAlignment = TextAlignment.Center;
            _time.ToolTip = "24-hour clock, for example 21:30";
            FontScaler.Grow(_time);
            timeRow.Children.Add(_time);
            timeRow.Children.Add(new TextBlock { Text = $"hh:mm, {regionName} server time (like the Abyss events)", FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 220 }.Ref(TextBlock.ForegroundProperty, "muted"));
            root.Children.Add(timeRow);

            root.Children.Add(Label("Days"));
            _everyDay = Check("Every day", CustomAlerts.EveryDay(_alert));
            root.Children.Add(_everyDay);
            var wrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            var picked = CustomAlerts.NormalizeDays(_alert.Days);
            foreach (var d in CustomAlerts.WeekDays)
            {
                var pill = new RadioButton { Content = d, GroupName = "day-" + d, Style = Res<Style>("Pill"), Margin = new Thickness(0, 0, 5, 4),
                    IsChecked = !CustomAlerts.EveryDay(_alert) && picked.Contains(d) };
                // a toggle: click flips it (a radio button on its own would stay on)
                pill.PreviewMouseLeftButtonDown += (s, e) => { e.Handled = true; if (pill.IsEnabled) pill.IsChecked = pill.IsChecked != true; };
                _days.Add((d, pill));
                wrap.Children.Add(pill);
            }
            root.Children.Add(wrap);
            void SyncDays() { foreach (var (_, p) in _days) p.IsEnabled = _everyDay.IsChecked != true; }
            _everyDay.Click += (s, e) => SyncDays();
            SyncDays();

            var repRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            _repeat = Check("Repeat every", _alert.RepeatHours > 0);
            _repeat.VerticalAlignment = VerticalAlignment.Center;
            repRow.Children.Add(_repeat);
            _repeatHours = Box(_alert.RepeatHours > 0 ? _alert.RepeatHours.ToString() : "3", 2);
            _repeatHours.Width = 30; _repeatHours.TextAlignment = TextAlignment.Center; _repeatHours.Margin = new Thickness(4, 0, 6, 0);
            FontScaler.Grow(_repeatHours);
            repRow.Children.Add(_repeatHours);
            repRow.Children.Add(new TextBlock { Text = "hours, until midnight", VerticalAlignment = VerticalAlignment.Center });
            void SyncRepeat() => _repeatHours.IsEnabled = _repeat.IsChecked == true;
            _repeat.Click += (s, e) => SyncRepeat();
            SyncRepeat();
            root.Children.Add(repRow);

            _on = Check("Alert is on", _alert.Enabled);
            _on.Margin = new Thickness(0, 8, 0, 0);
            _on.ToolTip = "Turn it off to keep the alert without being warned";
            root.Children.Add(_on);

            root.Children.Add(new TextBlock { Text = "Uses the same warning time, voice and volume as the Abyss events, and plays while the Abyss alerts window is open.",
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }.Ref(TextBlock.ForegroundProperty, "muted"));

            _problem = new TextBlock { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed }
                .Ref(TextBlock.ForegroundProperty, "error");
            root.Children.Add(_problem);

            var buttons = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = false };
            var save = new Button { Content = "Save", Style = Res<Style>("DialogButton"), Tag = "primary", Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            save.Click += (s, e) => TrySave();
            var cancel = new Button { Content = "Cancel", Style = Res<Style>("DialogButton"), IsCancel = true };
            cancel.Click += (s, e) => DialogResult = false;
            DockPanel.SetDock(cancel, Dock.Right); DockPanel.SetDock(save, Dock.Right);
            buttons.Children.Add(cancel); buttons.Children.Add(save);
            if (editing)
            {
                var del = new Button { Content = "Delete", Style = Res<Style>("DialogButton"), Tag = "danger" };
                del.Click += (s, e) =>
                {
                    if (!ConfirmDialog.Ask(this, "Delete alert?", $"Delete the custom alert \"{_alert.Name}\"?", "Delete", ConfirmKind.Danger)) return;
                    Result = Outcome.Deleted; DialogResult = true;
                };
                DockPanel.SetDock(del, Dock.Left);
                buttons.Children.Add(del);
            }
            root.Children.Add(buttons);

            Content = new Border { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Padding = new Thickness(0, 0, 0, 12), Child = root }
                .Ref(Border.BackgroundProperty, "menuBg").Ref(Border.BorderBrushProperty, "panelBorder");
            MouseLeftButtonDown += (s, e) => { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } WindowSnap.Clamp(this); } };
            WindowSnap.Attach(this);
            Closing += (s, e) => SavePosition();
            Loaded += (s, e) => { FontScaler.Apply(this); Activate(); _name.Focus(); _name.SelectAll(); };
        }

        private static T Res<T>(string key) => (T)Application.Current.FindResource(key);

        private static TextBlock Label(string text) => new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) }
            .Ref(TextBlock.ForegroundProperty, "title");

        private static TextBox Box(string text, int max) => new TextBox
        {
            Text = text, MaxLength = max, FontSize = 12, BorderThickness = new Thickness(1), Padding = new Thickness(4, 2, 4, 2), VerticalAlignment = VerticalAlignment.Center,
        }.Ref(ForegroundProperty, "text").Ref(BackgroundProperty, "inputBg").Ref(BorderBrushProperty, "panelBorder").Ref(TextBox.CaretBrushProperty, "caret");

        private static CheckBox Check(string text, bool on) => new CheckBox { Content = text, Style = Res<Style>("DarkCheck"), IsChecked = on };

        private void TrySave()
        {
            var picked = _days.Where(x => x.Pill.IsChecked == true).Select(x => x.Day).ToList();
            bool every = _everyDay.IsChecked == true, rep = _repeat.IsChecked == true;
            var problem = CustomAlerts.Problem(_name.Text, _time.Text, picked, every, rep, _repeatHours.Text);
            if (problem != null) { _problem.Text = problem; _problem.Visibility = Visibility.Visible; return; }
            _alert.Name = _name.Text.Trim();
            _alert.Time = CustomAlerts.NormalizeTime(_time.Text)!;
            _alert.Days = every ? new List<string> { "daily" } : CustomAlerts.NormalizeDays(picked);
            _alert.RepeatHours = rep ? int.Parse(_repeatHours.Text.Trim()) : 0;
            _alert.Enabled = _on.IsChecked == true;
            Result = Outcome.Saved;
            DialogResult = true;
        }

        private void SavePosition()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return;
            double l = Math.Round(Left), t = Math.Round(Top);
            if (S.CustomAlertLeft == l && S.CustomAlertTop == t) return;
            S.CustomAlertLeft = l; S.CustomAlertTop = t; // saved with the alert (or the next settings save)
        }

        /// <summary>Opens the editor. A new alert (existing null) or a copy of an existing one is edited; App.Settings is only
        /// changed here after Save or Delete, then saved (atomic write with a .bak).</summary>
        public static Outcome Edit(Window? owner, CustomAlert? existing)
        {
            var region = App.Schedule.Current.Regions.FirstOrDefault(r => r.Id == S.Region) ?? App.Schedule.Current.Regions[0];
            var d = new CustomAlertWindow(owner, existing, region.Name);
            _open = d;
            try { d.ShowDialog(); } finally { _open = null; }
            var list = S.CustomAlerts ??= new List<CustomAlert>();
            if (d.Result == Outcome.Saved)
            {
                int i = list.FindIndex(a => a.Id == d._alert.Id);
                if (i >= 0) list[i] = d._alert; else list.Add(d._alert);
            }
            else if (d.Result == Outcome.Deleted) list.RemoveAll(a => a.Id == d._alert.Id);
            S.Save(); // also keeps the window position
            return d.Result;
        }

        private static CustomAlertWindow? _open;

        /// <summary>Closes an open editor as Cancel (Settings closing with the launcher).</summary>
        public static void CancelOpen()
        {
            var d = _open;
            if (d == null) return;
            try { d.DialogResult = false; } catch (InvalidOperationException) { d.Close(); }
        }
    }
}
