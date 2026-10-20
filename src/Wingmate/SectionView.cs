using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>One checklist section (header + items), with editing, drag-to-reorder and the completion celebration.</summary>
    public sealed class SectionView : Grid
    {
        // palette (same as the overlay and the warp previews)
        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        // list colours follow the theme (read when the list is built; the window rebuilds on a theme change).
        // The gold celebration, diamonds and sparkles are the same in every theme.
        private static Color TXT => Th.C("text");
        private static Color DIM => Th.C("struck");
        private static Color ACC => Th.C("counter");
        private static Color DONEGREEN => Th.C("check");
        private static Color OUTLINE => Th.C("checkBoxOutline");
        private static readonly Color GOLD = C("#FFFFD58A"),
            AMBER = C("#FFC47F22"), CREAM = C("#FFFFF7E8"), CREAM2 = C("#FFFFEFD2"), CHKG = C("#FFFFF3DA"), DARKAMB = C("#FF5A3708"),
            FLASH = C("#FFFFFFA6"), PURPLE = C("#FF9E6BF2");
        private static SolidColorBrush B(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        private const string DragFormat = "WingmateItemId";
        private const double BOXOP = 0.52, FXD = 1.2, COLLAPSE = 0.6;

        public ChecklistSection Section { get; }
        public bool Busy { get; private set; }
        private readonly ChecklistWindow _win;
        private readonly Grid _body = new() { RenderTransformOrigin = new Point(0.5, 0.5) };
        private readonly Border _gold, _edge, _dark;
        private readonly Canvas _shimmerHost = new() { IsHitTestVisible = false, ClipToBounds = true };
        private readonly StackPanel _content = new() { Margin = new Thickness(6, 3, 6, 4) };
        private readonly Canvas _fx = new() { IsHitTestVisible = false };
        private readonly ScaleTransform _collapse = new(1, 1);
        private readonly TextBlock _name = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock _count = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
        private readonly List<Row> _rows = new();
        private sealed class Row { public ChecklistItem Item = null!; public Grid Grid = null!; public Border Box = null!; public Path Check = null!; public TextBlock Text = null!; public TextBlock Mark = null!;
            public Grid Diamond = null!; public ScaleTransform DiaScale = null!; public UIElement DiaShine = null!; public Image? DiaGlint; }

        public SectionView(ChecklistWindow win, ChecklistSection section)
        {
            _win = win; Section = section;
            Margin = new Thickness(0, 0, 0, 6);
            LayoutTransform = _collapse;

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            grad.GradientStops.Add(new GradientStop(GOLD, 0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(GOLD.R * 0.1 + AMBER.R * 0.9), (byte)(GOLD.G * 0.1 + AMBER.G * 0.9), (byte)(GOLD.B * 0.1 + AMBER.B * 0.9)), 1));
            _gold = new Border { CornerRadius = new CornerRadius(7), Background = grad, Opacity = 0, IsHitTestVisible = false,
                Effect = new DropShadowEffect { Color = GOLD, BlurRadius = 22, ShadowDepth = 0, Opacity = 1 } };
            _edge = new Border { CornerRadius = new CornerRadius(7), BorderBrush = B(GOLD), BorderThickness = new Thickness(1), Opacity = 0, IsHitTestVisible = false };
            _dark = new Border { CornerRadius = new CornerRadius(7), Background = Brushes.Black, Opacity = 0, IsHitTestVisible = false };
            _body.Children.Add(_gold); _body.Children.Add(_shimmerHost); _body.Children.Add(_edge); _body.Children.Add(_content); _body.Children.Add(_dark);
            _body.SizeChanged += (s, e) => _shimmerHost.Clip = new RectangleGeometry(new Rect(0, 0, _body.ActualWidth, _body.ActualHeight), 7, 7);
            Children.Add(_body); Children.Add(_fx);
            BuildContent();
        }

        // ------------------------------------------------------------ content
        private void BuildContent()
        {
            _content.Children.Clear(); _rows.Clear();
            var header = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, AllowDrop = true };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _name.Text = (Section.Collapsed ? "\u25B8 " : "\u25BE ") + Section.Name;
            _name.Foreground = B(TXT);
            _count.Text = $"{Section.Items.Count(i => i.Done)}/{Section.Items.Count}";
            _count.Foreground = B(ACC);
            UpdateCountTip();
            var plus = new Button { Content = "+", Style = (Style)_win.FindResource("LinkButton"), FontSize = 14, FontWeight = FontWeights.SemiBold,
                ToolTip = "Add an item", VerticalAlignment = VerticalAlignment.Center, Focusable = false };
            plus.Click += (s, e) => { if (!Busy) _win.AddItemTo(Section); };
            Grid.SetColumn(_count, 1); Grid.SetColumn(plus, 2);
            header.Children.Add(_name); header.Children.Add(_count); header.Children.Add(plus);
            bool headerPress = false;
            header.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (InTextBox(e.OriginalSource)) return;
                headerPress = true;
            };
            header.MouseLeftButtonUp += (s, e) =>
            {
                if (!headerPress || Busy || InTextBox(e.OriginalSource)) return;
                headerPress = false;
                _win.CommitActiveEdit();
                Section.Collapsed = !Section.Collapsed; _win.Save();
                _win.RequestRebuild();
            };
            header.ContextMenu = new ContextMenu();
            header.ContextMenuOpening += (s, e) => { _win.CommitActiveEdit(); BuildHeaderMenu(header.ContextMenu); };
            header.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            header.Drop += (s, e) => { if (e.Data.GetData(DragFormat) is string id) _win.MoveItem(id, Section, 0); e.Handled = true; };
            _content.Children.Add(header);
            if (Section.Collapsed) return;
            if (Section.Items.Count == 0)
                _content.Children.Add(new TextBlock { Text = "No items yet. Click + to add one.", FontSize = 11, Foreground = B(DIM), Margin = new Thickness(2, 3, 0, 1) });
            else if (!CheckedItems.Visible(Section, ShowChecked).Any())
                _content.Children.Add(new TextBlock { Text = "All checked.", FontSize = 11, Foreground = B(DIM), Margin = new Thickness(2, 3, 0, 1) });
            foreach (var item in CheckedItems.Visible(Section, ShowChecked)) _content.Children.Add(MakeRow(item).Grid);
        }

        private Row MakeRow(ChecklistItem item)
        {
            var r = new Row { Item = item };
            r.Grid = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 3, 0, 0), AllowDrop = true };
            r.Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            r.Grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            r.Check = new Path { Data = Geometry.Parse("M 3,7 L 6,10 L 11,4"), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            r.Box = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 1, 0, 0), Child = r.Check };
            r.Text = new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 0, 0), FontSize = 12 };
            // checked look like the original Aion Overlay: green check in place of the box, struck-through dimmed text
            r.Mark = new TextBlock { Text = "\u2714", Foreground = B(DONEGREEN), FontSize = 12, Width = 14, Margin = new Thickness(2, 0, 0, 0),
                TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
            MakeDiamond(r);
            Grid.SetColumn(r.Text, 1);
            r.Grid.Children.Add(r.Box); r.Grid.Children.Add(r.Mark); r.Grid.Children.Add(r.Diamond); r.Grid.Children.Add(r.Text);
            ApplyRowVisual(r);
            _rows.Add(r);

            Point? press = null;
            r.Grid.MouseEnter += (s, e) => { if (!Busy && !_win.ClickThrough) r.Grid.Background = Th.Br("rowHover"); };
            r.Grid.MouseLeave += (s, e) => r.Grid.Background = Brushes.Transparent;
            r.Grid.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (InTextBox(e.OriginalSource)) return;
                if (_win.CommitActiveEdit()) return; // first click elsewhere just finishes the edit
                press = e.GetPosition(r.Grid);
            };
            r.Grid.MouseMove += (s, e) =>
            {
                if (press == null || e.LeftButton != MouseButtonState.Pressed || Busy) return;
                var d = e.GetPosition(r.Grid) - press.Value;
                if (Math.Abs(d.X) < 5 && Math.Abs(d.Y) < 5) return;
                press = null;
                r.Grid.Opacity = 0.5;
                DragDrop.DoDragDrop(r.Grid, new DataObject(DragFormat, item.Id), DragDropEffects.Move);
                r.Grid.Opacity = 1;
            };
            r.Grid.MouseLeftButtonUp += (s, e) =>
            {
                if (press == null || Busy) return;
                press = null;
                Toggle(item);
            };
            r.Grid.DragOver += (s, e) => { e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
            r.Grid.Drop += (s, e) =>
            {
                if (e.Data.GetData(DragFormat) is not string id || id == item.Id) return;
                int idx = Section.Items.IndexOf(item);
                if (e.GetPosition(r.Grid).Y > r.Grid.ActualHeight / 2) idx++;
                _win.MoveItem(id, Section, idx);
                e.Handled = true;
            };
            r.Grid.ContextMenu = new ContextMenu();
            r.Grid.ContextMenuOpening += (s, e) => { _win.CommitActiveEdit(); BuildItemMenu(r.Grid.ContextMenu, r); };
            return r;
        }

        private static bool ShowChecked => App.Settings.ShowCheckedItems;
        private static readonly Random FxRng = new();

        private void UpdateCountTip()
        {
            int hidden = CheckedItems.HiddenCount(Section, ShowChecked);
            _count.ToolTip = hidden == 0 ? null
                : $"{hidden} checked item{(hidden == 1 ? " is" : "s are")} hidden. Right-click the checklist and pick Show checked items to see or uncheck {(hidden == 1 ? "it" : "them")}.";
        }

        /// <summary>Checks or unchecks an item. A checked item then warps out on its own and the rest of the list closes the gap,
        /// unless it was the last one (the list celebration plays, as before) or checked items are shown.</summary>
        private void Toggle(ChecklistItem item)
        {
            bool was = Section.IsComplete;
            item.Done = !item.Done;
            var row = _rows.FirstOrDefault(x => x.Item == item);
            bool warp = row != null && CheckedItems.WarpOutOnCheck(Section, item, ShowChecked);
            RefreshRows();
            _win.ItemToggled(this, was); // saves; celebrates if that completed the list
            if (warp && !Busy) WarpOutRow(row!, WarpPicker.Next(FxRng));
        }

        /// <summary>Locked window: the checkbox of every visible row (its 14 px box) in <paramref name="relativeTo"/>'s coordinates,
        /// with the item it toggles.</summary>
        public IEnumerable<(Rect Box, ChecklistItem Item)> CheckTargets(Visual relativeTo)
        {
            foreach (var r in _rows)
            {
                if (!r.Grid.IsVisible || !Section.Items.Contains(r.Item)) continue;
                Rect b;
                try { b = r.Box.TransformToAncestor(relativeTo).TransformBounds(new Rect(0, 0, Math.Max(14, r.Box.ActualWidth), Math.Max(14, r.Box.ActualHeight))); }
                catch (InvalidOperationException) { continue; } // not in this window's tree right now
                yield return (b, r.Item);
            }
        }

        /// <summary>Locked window: checks or unchecks an item from its checkbox (same as a normal click on the row).</summary>
        public bool ToggleFromLocked(ChecklistItem item)
        {
            if (Busy || !Section.Items.Contains(item)) return false;
            Toggle(item);
            return true;
        }

        /// <summary>Gem-like diamond check for completed lists: crown and pavilion facets with a gradient, a white shine and an outline.</summary>
        private static void MakeDiamond(Row r)
        {
            var body = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            body.GradientStops.Add(new GradientStop(C("#FFF2FDFF"), 0));
            body.GradientStops.Add(new GradientStop(C("#FF9BE7FF"), 0.35));
            body.GradientStops.Add(new GradientStop(C("#FF4FA6F2"), 0.7));
            body.GradientStops.Add(new GradientStop(C("#FFA48BFF"), 1));
            body.Freeze();
            var g = new Grid { Width = 14, Height = 14, Margin = new Thickness(2, 1, 0, 0), VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Hidden, IsHitTestVisible = false, RenderTransformOrigin = new Point(0.5, 0.5) };
            r.DiaScale = new ScaleTransform(1, 1);
            g.RenderTransform = r.DiaScale;
            g.Children.Add(new Path { Data = Geometry.Parse("M 1,5 L 4,1.5 L 10,1.5 L 13,5 L 7,13 Z"), Fill = body, Stroke = Th.Br("gemOutline"), StrokeThickness = 0.8, StrokeLineJoin = PenLineJoin.Round });
            g.Children.Add(new Path { Data = Geometry.Parse("M 1,5 L 13,5 M 4,1.5 L 5.5,5 L 7,1.5 L 8.5,5 L 10,1.5 M 5.5,5 L 7,13 L 8.5,5"), Stroke = B(C("#99FFFFFF")), StrokeThickness = 0.5 });
            var shine = new Path { Data = Geometry.Parse("M 4.3,2.3 L 6.4,2.3 L 5.2,4.6 L 2.4,4.6 Z"), Fill = B(Colors.White), Opacity = 0.85 };
            g.Children.Add(shine);
            r.DiaShine = shine;
            r.Diamond = g;
        }

        /// <summary>Repaints every row and the counter (a completed list shows diamonds, so every row may change).</summary>
        public void RefreshRows()
        {
            foreach (var row in _rows) ApplyRowVisual(row);
            _count.Text = $"{Section.Items.Count(i => i.Done)}/{Section.Items.Count}";
            UpdateCountTip();
        }

        private void ApplyRowVisual(Row r)
        {
            bool d = r.Item.Done;
            bool gem = d && Section.IsComplete; // completed list: diamonds instead of green checks
            r.Diamond.Visibility = gem ? Visibility.Visible : Visibility.Hidden;
            r.DiaScale.ScaleX = r.DiaScale.ScaleY = 1;
            r.Box.Background = Brushes.Transparent;
            r.Box.BorderBrush = B(OUTLINE);
            r.Box.Visibility = d ? Visibility.Hidden : Visibility.Visible;
            r.Check.Stroke = B(C("#FF141A24"));
            r.Check.Visibility = Visibility.Hidden;
            r.Mark.Visibility = d && !gem ? Visibility.Visible : Visibility.Collapsed;
            r.Text.Foreground = B(d ? DIM : TXT); // same dimmed colour as before, now struck through
            r.Text.TextDecorations = d ? TextDecorations.Strikethrough : null;
        }

        // ------------------------------------------------------------ menus / editing
        private MenuItem Mi(string header, Action a, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += (s, e) => { e.Handled = true; a(); };
            return mi;
        }

        private void BuildHeaderMenu(ContextMenu m)
        {
            m.Items.Clear();
            int idx = _win.State.Sections.IndexOf(Section);
            m.Items.Add(Mi("Add item", () => _win.AddItemTo(Section)));
            if (Section.IsCustom) m.Items.Add(Mi("Rename list", () => _win.RenameSection(Section)));
            m.Items.Add(Mi("Uncheck all", () => { foreach (var i in Section.Items) i.Done = false; _win.Save(); _win.RequestRebuild(); }, Section.Items.Any(i => i.Done)));
            var showChk = new MenuItem { Header = "Show checked items", IsChecked = ShowChecked };
            showChk.Click += (s2, e2) => { e2.Handled = true; _win.ToggleShowChecked(); };
            m.Items.Add(showChk);
            m.Items.Add(Mi("Move list up", () => _win.MoveSection(Section, -1), idx > 0));
            m.Items.Add(Mi("Move list down", () => _win.MoveSection(Section, 1), idx < _win.State.Sections.Count - 1));
            m.Items.Add(Mi("Hide list", () => { Section.Hidden = true; _win.Save(); _win.RequestRebuild(); }));
            var others = App.Book.Characters.Where(c => c != _win.State).ToList();
            if (others.Count > 0) // "Copy to" another character (only when there is one)
            {
                var copy = new MenuItem { Header = "Copy to", IsEnabled = Section.Items.Count > 0 };
                foreach (var c in others)
                {
                    var id = c.Id;
                    copy.Items.Add(Mi(c.Name, () => _win.CopyList(Section, new[] { id })));
                }
                copy.Items.Add(new Separator { Style = (Style)_win.FindResource("DarkSep") });
                var ids = others.Select(c => c.Id).ToList();
                copy.Items.Add(Mi("All characters", () => _win.CopyList(Section, ids)));
                m.Items.Add(copy);
            }
            m.Items.Add(new Separator { Style = (Style)_win.FindResource("DarkSep") });
            if (Section.IsCustom) m.Items.Add(Mi("Delete list...", () => _win.DeleteSection(Section)));
            else m.Items.Add(Mi("Reset to default...", () => _win.ResetToDefault(Section)));
            m.IsEnabled = !Busy;
        }

        private void BuildItemMenu(ContextMenu m, Row r)
        {
            m.Items.Clear();
            int i = Section.Items.IndexOf(r.Item), n = Section.Items.Count;
            m.Items.Add(Mi("Edit", () => _win.EditItem(Section, r.Item)));
            m.Items.Add(Mi("Move up", () => _win.MoveItem(r.Item.Id, Section, i - 1), i > 0));
            m.Items.Add(Mi("Move down", () => _win.MoveItem(r.Item.Id, Section, i + 2), i < n - 1));
            m.Items.Add(Mi("Move to top", () => _win.MoveItem(r.Item.Id, Section, 0), i > 0));
            m.Items.Add(Mi("Move to bottom", () => _win.MoveItem(r.Item.Id, Section, n), i < n - 1));
            m.Items.Add(new Separator { Style = (Style)_win.FindResource("DarkSep") });
            m.Items.Add(Mi("Remove", () => { Section.Items.Remove(r.Item); _win.Save(); _win.RequestRebuild(); }));
            m.IsEnabled = !Busy;
        }

        private static bool InTextBox(object src)
        {
            for (var d = src as DependencyObject; d != null; d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
                if (d is TextBox) return true;
            return false;
        }

        public void BeginItemEdit(ChecklistItem item, bool isNew)
        {
            var r = _rows.FirstOrDefault(x => x.Item == item);
            if (r != null) StartEdit(r, isNew);
        }

        private TextBox EditBox(string text) => new()
        {
            Text = text, FontSize = 12, Foreground = B(TXT), Background = Th.Br("inputBg"), BorderBrush = Th.Br("focus"),
            BorderThickness = new Thickness(1), CaretBrush = Th.Br("caret"), SelectionBrush = B(ACC), Padding = new Thickness(2, 0, 2, 0),
            TextWrapping = TextWrapping.Wrap, AcceptsReturn = false,
        };

        /// <summary>One edit box at a time. commit(typed, escaped) is called exactly once, whatever ends the edit.</summary>
        private void RunEdit(TextBox tb, Panel host, UIElement replaced, Action<string, bool> commit)
        {
            bool finished = false;
            void Finish(bool escaped)
            {
                if (finished) return;
                finished = true;
                commit(tb.Text, escaped);
                _win.EndEdit();
                _win.RequestRebuild();
            }
            _win.BeginEdit(() => Finish(false)); // also commits any other open edit first
            replaced.Visibility = Visibility.Collapsed;
            host.Children.Add(tb);
            FontScaler.Apply(tb);
            tb.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { Finish(false); e.Handled = true; }
                else if (e.Key == Key.Escape) { Finish(true); e.Handled = true; }
            };
            tb.LostKeyboardFocus += (s, e) => Finish(false);
            Dispatcher.BeginInvoke(new Action(() => { if (!finished) { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); } }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void StartEdit(Row r, bool isNew)
        {
            var tb = EditBox(r.Item.Text);
            Grid.SetColumn(tb, 1); tb.Margin = new Thickness(6, -1, 0, 0);
            var original = r.Item.Text;
            RunEdit(tb, r.Grid, r.Text, (typed, esc) => EditLogic.ApplyItem(Section, r.Item, EditLogic.ResolveItem(original, typed, isNew, esc)));
        }

        public void StartRename(bool isNew)
        {
            var header = (Grid)_content.Children[0];
            var tb = EditBox(Section.Name);
            tb.FontWeight = FontWeights.SemiBold;
            var original = Section.Name;
            RunEdit(tb, header, _name, (typed, esc) => Section.Name = EditLogic.ResolveName(original, typed, esc));
        }

        // ------------------------------------------------------------ celebration
        private sealed class Glint { public double X, Y, Vx, Vy, T0, Life, S, Tw, U; public Color Col; public Image? El; }
        private static readonly Dictionary<Color, ImageSource> Stars = new();

        private static ImageSource Star(Color c)
        {
            if (Stars.TryGetValue(c, out var img)) return img;
            RadialGradientBrush Rad(double a) { var b = new RadialGradientBrush(Color.FromArgb((byte)(255 * a), c.R, c.G, c.B), Color.FromArgb(0, c.R, c.G, c.B)); b.Freeze(); return b; }
            var g = new DrawingGroup();
            g.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 24, 24))));
            g.Children.Add(new GeometryDrawing(Rad(0.9), null, new EllipseGeometry(new Point(12, 12), 4.5, 4.5)));
            g.Children.Add(new GeometryDrawing(Rad(0.95), null, new EllipseGeometry(new Point(12, 12), 12, 1.1)));
            g.Children.Add(new GeometryDrawing(Rad(0.95), null, new EllipseGeometry(new Point(12, 12), 1.1, 12)));
            g.Children.Add(new GeometryDrawing(B(Colors.White), null, new EllipseGeometry(new Point(12, 12), 1.4, 1.4)));
            var di = new DrawingImage(g); di.Freeze();
            Stars[c] = di;
            return di;
        }

        private void SetGlint(ref Image? el, double x, double y, double s, double intensity, Color col)
        {
            if (intensity <= 0.01) { if (el != null) el.Visibility = Visibility.Collapsed; return; }
            if (el == null) { el = new Image { Source = Star(col), IsHitTestVisible = false }; _fx.Children.Add(el); }
            double size = s * 7;
            el.Width = el.Height = size;
            Canvas.SetLeft(el, x - size / 2); Canvas.SetTop(el, y - size / 2);
            el.Opacity = Math.Min(1, intensity);
            el.Visibility = Visibility.Visible;
        }

        private static double Smooth(double a, double b, double x) { var t = Math.Clamp((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }
        private static Color Lerp(Color a, Color b, double t) => Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        private Rect RectOf(FrameworkElement e)
        {
            try { return e.TransformToAncestor(_body).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight)); }
            catch { return Rect.Empty; }
        }

        /// <summary>Steady sparkling gold until 2 s after Bella finishes, then a warp-out (0 sparkle dissolve,
        /// 1 black hole, 2 ripple), then the section folds away. done() runs at the end.</summary>
        public void RunCelebration(double speechSec, int effect, Random rng, Action done)
        {
            Busy = true;
            _content.IsHitTestVisible = false;
            double W = Math.Max(20, _body.ActualWidth), H = Math.Max(16, _body.ActualHeight);
            double T_FX = Math.Max(0.5, speechSec) + 2.0, T_COL = T_FX + FXD, T_END = T_COL + COLLAPSE;

            // gold text mode
            var head = new SolidColorBrush(CREAM); var itemB = new SolidColorBrush(CREAM2);
            _name.Foreground = head; _count.Foreground = head;
            foreach (var r in _rows)
            {
                // completed items: shiny diamond checks that sparkle, text stays struck through
                r.Mark.Visibility = Visibility.Collapsed; r.Box.Visibility = Visibility.Hidden; r.Check.Visibility = Visibility.Hidden;
                r.Diamond.Visibility = Visibility.Visible;
                r.Text.TextDecorations = TextDecorations.Strikethrough;
                r.Text.Foreground = itemB;
            }
            _content.Effect = new DropShadowEffect { Color = Color.FromRgb(0x1A, 0x0D, 0x00), ShadowDepth = 1.2, BlurRadius = 1.5, Opacity = 0.75, Direction = 315 };

            var texts = new List<FrameworkElement> { _name, _count };
            foreach (var r in _rows) { texts.Add(r.Text); texts.Add(r.Diamond); }
            var gems = _rows.Select((r, i) => (Row: r, Rc: RectOf(r.Diamond), T0: 0.15 + 0.09 * i)).ToList();
            var avoid = texts.Select(RectOf).Where(x => !x.IsEmpty).Select(x => { x.Inflate(3, 3); return x; }).ToList();

            // steady-phase sparkles
            double area = W * H, rateBg = Math.Clamp(7 * area / 34560, 4, 16), rateBd = Math.Clamp(3.5 * (W + H) / 428, 2.5, 7);
            var bg = new List<Glint>(); var bd = new List<Glint>();
            for (double t0 = 0.15; t0 < T_FX; t0 += -Math.Log(1 - rng.NextDouble()) / rateBg)
            {
                double x = 0, y = 0;
                for (int k = 0; k < 60; k++) { x = 6 + rng.NextDouble() * (W - 12); y = 5 + rng.NextDouble() * (H - 10); if (!avoid.Any(a => a.Contains(x, y))) break; }
                bg.Add(new Glint { X = x, Y = y, T0 = t0, Life = 0.55 + rng.NextDouble() * 0.4, S = 1.6 + rng.NextDouble() * 1.4, Vy = -3 - rng.NextDouble() * 6,
                    Col = rng.NextDouble() < 0.5 ? C("#FFFFF7E0") : C("#FFFFE094") });
            }
            for (double t0 = 0.15; t0 < T_FX; t0 += -Math.Log(1 - rng.NextDouble()) / rateBd)
                bd.Add(new Glint { U = rng.NextDouble(), T0 = t0, Life = 0.4 + rng.NextDouble() * 0.35, S = 2.2 + rng.NextDouble() * 1.4, Col = C("#FFFFEDBF") });
            Image? trav1 = null, trav2 = null;
            var sweeps = Enumerable.Range(0, 20).Select(k => 0.6 + k * 1.5).Where(ts => ts + 0.8 < T_FX + 0.01).ToList();
            var bandBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            bandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 245, 210), 0));
            bandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 245, 210), 0.5));
            bandBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 245, 210), 1));
            var bandMove = new TranslateTransform();
            var band = new Rectangle { Width = 40, Height = H * 3, Fill = bandBrush, Opacity = 0,
                RenderTransform = new TransformGroup { Children = { new RotateTransform(25), bandMove } } };
            Canvas.SetTop(band, -H);
            _shimmerHost.Children.Add(band);

            Point Perim(double u)
            {
                double P = 2 * (W + H), d = ((u % 1) + 1) % 1 * P;
                if (d < W) return new Point(d, 0); d -= W;
                if (d < H) return new Point(W - 1, d); d -= H;
                if (d < W) return new Point(W - 1 - d, H - 1); d -= W;
                return new Point(0, H - 1 - d);
            }

            // effect state
            List<Glint>? dust = null;
            LinearGradientBrush? mask = null;
            ScaleTransform? bhScale = null; RotateTransform? bhRot = null;
            Ellipse? core = null, ring = null, halo = null; RotateTransform? ringSpin = null; Image? flash = null;
            List<(SolidColorBrush? Brush, UIElement? El, double Tc, Point P, Image? G)>? chars = null;

            var sw = Stopwatch.StartNew();
            bool finished = false;
            EventHandler? tick = null;
            tick = (s, e) =>
            {
                if (finished) return;
                double t = sw.Elapsed.TotalSeconds;
                try
                {
                    double gm = Smooth(0, 0.25, t);
                    double breathe = 1.0 + 0.06 * Math.Sin(2 * Math.PI * t / 1.6) * gm;
                    double p = Math.Clamp((t - T_FX) / FXD, 0, 1);
                    double k = gm * (1 - Smooth(T_FX, T_FX + 0.12, t));
                    double secop = gm * breathe;

                    // --- steady sparkles
                    foreach (var q in bg)
                    {
                        double age = t - q.T0, inten = 0;
                        if (age >= 0 && age <= q.Life) { double u = age / q.Life; inten = Math.Pow(Math.Sin(Math.PI * u), 1.5) * (0.75 + 0.25 * Math.Sin(age * 30 + q.X)) * 0.85 * k; }
                        SetGlint(ref q.El, q.X, q.Y + q.Vy * Math.Max(0, age), q.S, inten, q.Col);
                    }
                    foreach (var q in bd)
                    {
                        double age = t - q.T0, inten = 0; var pt = Perim(q.U + 0.02 * Math.Max(0, age));
                        if (age >= 0 && age <= q.Life) inten = Math.Sin(Math.PI * age / q.Life) * k;
                        SetGlint(ref q.El, pt.X, pt.Y, q.S, inten, q.Col);
                    }
                    var p1 = Perim(0.11 * t); var p2 = Perim(0.5 - 0.11 * t);
                    SetGlint(ref trav1, p1.X, p1.Y, 2.6, 0.55 * k, C("#FFFFEBB3"));
                    SetGlint(ref trav2, p2.X, p2.Y, 2.6, 0.55 * k, C("#FFFFEBB3"));
                    band.Opacity = 0;
                    foreach (var ts in sweeps)
                    {
                        double u = (t - ts) / 0.8;
                        if (u < 0 || u > 1) continue;
                        bandMove.X = -80 + u * (W + 160);
                        band.Opacity = 0.35 * Math.Sin(Math.PI * u) * k;
                    }

                    // --- diamond checks: pop in one after another, then a twinkle about every 1.3 s
                    foreach (var gm2 in gems)
                    {
                        double age = t - gm2.T0, pulse = 0;
                        if (age >= 0)
                        {
                            double u = (age % 1.3) / 0.45;
                            if (u <= 1) pulse = Math.Sin(Math.PI * u);
                        }
                        double pop = age < 0 ? 0.55 : 0.55 + 0.45 * Smooth(0, 0.22, age) + 0.12 * Math.Sin(Math.PI * Math.Clamp(age / 0.3, 0, 1));
                        gm2.Row.DiaScale.ScaleX = gm2.Row.DiaScale.ScaleY = pop * (1 + 0.12 * pulse * k);
                        gm2.Row.DiaShine.Opacity = 0.55 + 0.45 * pulse;
                        if (!gm2.Rc.IsEmpty)
                            SetGlint(ref gm2.Row.DiaGlint, gm2.Rc.X + gm2.Rc.Width * 0.78, gm2.Rc.Y + gm2.Rc.Height * 0.2, 1.5, pulse * k, Colors.White);
                    }

                    // --- warp-out
                    if (effect == 0) // sparkle dissolve
                    {
                        secop *= 1 - Smooth(0.15, 0.8, p);
                        if (p > 0)
                        {
                            double fl = Smooth(0, 0.12, p) * (1 - Smooth(0.12, 0.4, p)), gk = Smooth(0, 0.1, p) * 0.6;
                            head.Color = Lerp(Lerp(CREAM, GOLD, gk), FLASH, fl);
                            itemB.Color = Lerp(Lerp(CREAM2, GOLD, gk), FLASH, fl);
                            if (mask == null)
                            {
                                mask = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
                                mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
                                mask.GradientStops.Add(new GradientStop(Colors.Black, 0.05));
                                _content.OpacityMask = mask;
                                dust = new List<Glint>();
                                foreach (var fe in texts)
                                {
                                    var rc = RectOf(fe); if (rc.IsEmpty) continue;
                                    int n = (int)Math.Clamp(rc.Width * rc.Height / 18, 4, 45);
                                    for (int i = 0; i < n; i++)
                                    {
                                        double x = rc.X + rng.NextDouble() * rc.Width, xf = x / W;
                                        dust.Add(new Glint { X = x, Y = rc.Y + rng.NextDouble() * rc.Height, T0 = 0.07 + 0.6 * xf + rng.NextDouble() * 0.1,
                                            Life = 0.45 + rng.NextDouble() * 0.25, Vy = -16 - rng.NextDouble() * 22, Vx = -8 + rng.NextDouble() * 16,
                                            S = (1.0 + rng.NextDouble() * 1.2) * 1.2, Tw = rng.NextDouble() * 6.28, Col = rng.NextDouble() < 0.7 ? C("#FFFFD980") : Colors.White });
                                    }
                                }
                            }
                            double lp = -0.1 + 1.2 * Math.Clamp((p - 0.12) / 0.6, 0, 1);
                            mask.GradientStops[0].Offset = lp - 0.04; mask.GradientStops[1].Offset = lp + 0.06;
                            foreach (var q in dust!)
                            {
                                double age = t - T_FX - q.T0 * FXD, inten = 0;
                                if (age >= 0 && age <= q.Life) { double u = age / q.Life; inten = Math.Sin(Math.PI * u) * (0.6 + 0.4 * Math.Sin(q.Tw + age * 25)); }
                                SetGlint(ref q.El, q.X + q.Vx * Math.Max(0, age), q.Y + q.Vy * Math.Max(0, age), q.S, inten, q.Col);
                            }
                        }
                    }
                    else if (effect == 1) // black hole
                    {
                        if (p > 0)
                        {
                            if (bhScale == null)
                            {
                                bhScale = new ScaleTransform(); bhRot = new RotateTransform();
                                _body.RenderTransform = new TransformGroup { Children = { bhScale, bhRot } };
                                halo = new Ellipse { Fill = new RadialGradientBrush(Color.FromArgb(90, PURPLE.R, PURPLE.G, PURPLE.B), Color.FromArgb(0, PURPLE.R, PURPLE.G, PURPLE.B)) };
                                ringSpin = new RotateTransform();
                                var rb = new LinearGradientBrush(GOLD, PURPLE, 0) { RelativeTransform = new RotateTransform { CenterX = 0.5, CenterY = 0.5 } };
                                ringSpin = (RotateTransform)rb.RelativeTransform;
                                ring = new Ellipse { Stroke = rb };
                                core = new Ellipse { Fill = Brushes.Black };
                                _fx.Children.Add(halo); _fx.Children.Add(ring); _fx.Children.Add(core);
                            }
                            double q = Math.Pow(Smooth(0.05, 0.82, p), 1.5);
                            bhScale.ScaleX = bhScale.ScaleY = Math.Max(0.001, 1 - 0.985 * q);
                            bhRot!.Angle = (1.2 * q + 2.1 * q) * 180 / Math.PI;
                            _dark.Opacity = 0.8 * Smooth(0.03, 0.8, p);
                            _body.Opacity = 1 - Smooth(0.72, 0.88, p);
                            double grow = Smooth(0, 0.2, p), coll = 1 - Smooth(0.8, 0.9, p), R0 = 11 * grow * coll * (1 + 0.25 * q);
                            double cx = W / 2, cy = H / 2;
                            void Place(Ellipse el, double r) { el.Width = el.Height = Math.Max(0, 2 * r); Canvas.SetLeft(el, cx - r); Canvas.SetTop(el, cy - r); }
                            Place(core!, R0); Place(ring!, R0 * 1.35 + 1); Place(halo!, R0 * 2.4 + 2);
                            ring!.StrokeThickness = 1.4 + R0 * 0.18;
                            ring.Opacity = 0.85 * grow * coll; halo!.Opacity = grow * coll;
                            ringSpin!.Angle = t * 9 * 180 / Math.PI;
                            double fi = Smooth(0.88, 0.92, p) * (1 - Smooth(0.93, 1.0, p));
                            SetGlint(ref flash, cx, cy, 6, fi * 1.3, C("#FFFFEBCC"));
                        }
                    }
                    else // ripple: letters vanish in random order
                    {
                        secop *= 1 - Smooth(0.25, 1.0, p);
                        if (p > 0)
                        {
                            if (chars == null)
                            {
                                chars = new();
                                var runs = new List<(TextBlock Tb, Run R)>();
                                foreach (var tb in texts.OfType<TextBlock>())
                                {
                                    string s0 = tb.Text; var col = ((SolidColorBrush)tb.Foreground).Color;
                                    tb.Inlines.Clear();
                                    foreach (var ch in s0) { var run = new Run(ch.ToString()) { Foreground = new SolidColorBrush(col) }; tb.Inlines.Add(run); if (!char.IsWhiteSpace(ch)) runs.Add((tb, run)); }
                                }
                                _content.UpdateLayout();
                                var items = new List<(SolidColorBrush?, UIElement?, Point)>();
                                foreach (var (tb, run) in runs)
                                {
                                    var a = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                                    var b2 = run.ContentEnd.GetCharacterRect(LogicalDirection.Backward);
                                    var pt = tb.TranslatePoint(new Point((a.Left + b2.Left) / 2, a.Top + a.Height / 2), _body);
                                    items.Add(((SolidColorBrush)run.Foreground, null, pt));
                                }
                                foreach (var r in _rows) { var rc = RectOf(r.Diamond); items.Add((null, r.Diamond, new Point(rc.X + rc.Width / 2, rc.Y + rc.Height / 2))); }
                                var order = Enumerable.Range(0, items.Count).OrderBy(_ => rng.Next()).ToList();
                                for (int i = 0; i < order.Count; i++)
                                {
                                    var it = items[order[i]];
                                    double tc = 0.03 + 0.8 * i / Math.Max(1, order.Count - 1) + (rng.NextDouble() - 0.5) * 0.02;
                                    chars.Add((it.Item1, it.Item2, tc, it.Item3, null));
                                }
                            }
                            for (int i = 0; i < chars.Count; i++)
                            {
                                var c = chars[i];
                                double a = 1 - Smooth(c.Tc - 0.02, c.Tc + 0.05, p);
                                if (c.Brush != null) c.Brush.Opacity = a; else if (c.El != null) c.El.Opacity = a;
                                double gi = Math.Exp(-Math.Pow((p - c.Tc - 0.02) / 0.035, 2));
                                var g = c.G;
                                SetGlint(ref g, c.P.X, c.P.Y, 2.4, gi > 0.02 ? gi * 0.95 : 0, C("#FFFFF2CC"));
                                chars[i] = (c.Brush, c.El, c.Tc, c.P, g);
                            }
                        }
                    }

                    _gold.Opacity = BOXOP * secop;
                    _edge.Opacity = 0.6 * secop;

                    // --- fold the finished section away
                    if (t >= T_COL)
                    {
                        double c = Smooth(T_COL, T_END, t);
                        _collapse.ScaleY = Math.Max(0.001, 1 - c);
                        Opacity = 1 - c;
                    }
                    if (t >= T_END) Finish();
                }
                catch (Exception ex) { Log.Write("celebration: " + ex); Finish(); }
            };
            void Finish()
            {
                if (finished) return;
                finished = true;
                CompositionTarget.Rendering -= tick;
                Busy = false;
                done();
            }
            CompositionTarget.Rendering += tick;
        }
            // ------------------------------------------------------------ one item warps out
        private const double ROWFX = 0.65, ROWCOL = 0.28;

        /// <summary>One checked item leaves: the same three warp-outs as a finished list (0 sparkle dissolve, 1 black hole,
        /// 2 ripple), sized to the row, then the row folds away so the items below slide up. The item stays checked in the data.</summary>
        private void WarpOutRow(Row r, int effect)
        {
            _rows.Remove(r);                 // no longer a target for clicks, repaints or the list celebration
            r.Grid.IsHitTestVisible = false;
            r.Grid.Background = Brushes.Transparent;
            _win.BeginFx();                  // holds list rebuilds until it's gone
            var rc = RectOf(r.Grid);
            if (rc.IsEmpty) rc = new Rect(0, 0, Math.Max(20, _body.ActualWidth), 18);
            double cx = rc.X + rc.Width / 2, cy = rc.Y + rc.Height / 2;
            var made = new List<UIElement>();
            var glints = new List<Glint>();
            var textBrush = new SolidColorBrush(((r.Text.Foreground as SolidColorBrush)?.Color) ?? DIM);
            r.Text.Foreground = textBrush;
            var baseCol = textBrush.Color;
            var baseMargin = r.Grid.Margin;
            var fold = new ScaleTransform(1, 1);
            r.Grid.LayoutTransform = fold;

            if (effect == 0) // sparkle dissolve: the text flashes gold and breaks into drifting sparkles
            {
                int n = (int)Math.Clamp(rc.Width / 14, 8, 22);
                for (int i = 0; i < n; i++)
                    glints.Add(new Glint { X = rc.X + 4 + FxRng.NextDouble() * Math.Max(1, rc.Width - 8), Y = rc.Y + 2 + FxRng.NextDouble() * Math.Max(1, rc.Height - 4),
                        T0 = 0.05 + FxRng.NextDouble() * 0.4, Life = 0.25 + FxRng.NextDouble() * 0.25, S = 1.2 + FxRng.NextDouble() * 1.0,
                        Vx = (FxRng.NextDouble() - 0.5) * 20, Vy = -8 - FxRng.NextDouble() * 14, Tw = FxRng.NextDouble() * 6,
                        Col = FxRng.NextDouble() < 0.5 ? C("#FFFFF7E0") : C("#FFFFE094") });
            }
            ScaleTransform? bhScale = null; RotateTransform? bhRot = null; Ellipse? core = null, ring = null; Image? flash = null;
            if (effect == 1) // black hole: the row spins down into a small dark core, then a flash
            {
                bhScale = new ScaleTransform(1, 1); bhRot = new RotateTransform();
                r.Grid.RenderTransformOrigin = new Point(0.5, 0.5);
                r.Grid.RenderTransform = new TransformGroup { Children = { bhScale, bhRot } };
                ring = new Ellipse { Stroke = new LinearGradientBrush(GOLD, PURPLE, 0), IsHitTestVisible = false };
                core = new Ellipse { Fill = Brushes.Black, IsHitTestVisible = false };
                _fx.Children.Add(ring); _fx.Children.Add(core); made.Add(ring); made.Add(core);
            }
            List<(Run Run, double Tc, Point P, Image? G)>? chars = null;
            if (effect == 2) // ripple: the letters vanish in random order, each with a tiny sparkle
            {
                chars = new();
                string s0 = r.Text.Text; var col = textBrush.Color;
                r.Text.Inlines.Clear();
                var runs = new List<Run>();
                foreach (var ch in s0) { var run = new Run(ch.ToString()) { Foreground = new SolidColorBrush(col) }; r.Text.Inlines.Add(run); if (!char.IsWhiteSpace(ch)) runs.Add(run); }
                try { r.Grid.UpdateLayout(); } catch { }
                var order = Enumerable.Range(0, runs.Count).OrderBy(_ => FxRng.Next()).ToList();
                for (int i = 0; i < order.Count; i++)
                {
                    var run = runs[order[i]];
                    Point pt;
                    try
                    {
                        var a = run.ContentStart.GetCharacterRect(LogicalDirection.Forward);
                        pt = r.Text.TranslatePoint(new Point(a.Left + 3, a.Top + a.Height / 2), _body);
                    }
                    catch { pt = new Point(cx, cy); }
                    chars.Add((run, 0.05 + 0.8 * i / Math.Max(1, order.Count - 1), pt, null));
                }
            }

            var sw = Stopwatch.StartNew();
            bool finished = false;
            EventHandler? tick = null;
            void Finish()
            {
                if (finished) return;
                finished = true;
                CompositionTarget.Rendering -= tick;
                try
                {
                    _content.Children.Remove(r.Grid);
                    foreach (var g in glints) if (g.El != null) _fx.Children.Remove(g.El);
                    if (chars != null) foreach (var c in chars) if (c.G != null) _fx.Children.Remove(c.G);
                    if (flash != null) _fx.Children.Remove(flash);
                    foreach (var el in made) _fx.Children.Remove(el);
                    if (Section.Items.Count > 0 && !CheckedItems.Visible(Section, ShowChecked).Any() && !Busy && !Section.Collapsed)
                        _content.Children.Add(FontScaler.Scaled(new TextBlock { Text = "All checked.", FontSize = 11, Foreground = B(DIM), Margin = new Thickness(2, 3, 0, 1) }));
                }
                catch (Exception ex) { Log.Write("item warp: " + ex.Message); }
                _win.EndFx();
            }
            tick = (s, e) =>
            {
                if (finished) return;
                double t = sw.Elapsed.TotalSeconds, p = Math.Clamp(t / ROWFX, 0, 1);
                try
                {
                    if (effect == 0)
                    {
                        double fl = Smooth(0, 0.15, p) * (1 - Smooth(0.15, 0.45, p));
                        textBrush.Color = Lerp(Lerp(baseCol, GOLD, Smooth(0, 0.1, p) * 0.5), FLASH, fl);
                        r.Grid.Opacity = 1 - Smooth(0.15, 0.8, p);
                        foreach (var q in glints)
                        {
                            double age = t - q.T0, inten = 0;
                            if (age >= 0 && age <= q.Life) inten = Math.Sin(Math.PI * age / q.Life) * (0.65 + 0.35 * Math.Sin(q.Tw + age * 25));
                            SetGlint(ref q.El, q.X + q.Vx * Math.Max(0, age), q.Y + q.Vy * Math.Max(0, age), q.S, inten, q.Col);
                        }
                    }
                    else if (effect == 1)
                    {
                        double q = Math.Pow(Smooth(0.05, 0.85, p), 1.5);
                        bhScale!.ScaleX = bhScale.ScaleY = Math.Max(0.001, 1 - 0.985 * q);
                        bhRot!.Angle = q * 200;
                        r.Grid.Opacity = 1 - Smooth(0.7, 0.9, p);
                        double grow = Smooth(0, 0.25, p), coll = 1 - Smooth(0.8, 0.92, p), R0 = Math.Min(7, rc.Height * 0.4) * grow * coll;
                        void Place(Ellipse el, double rad) { el.Width = el.Height = Math.Max(0, 2 * rad); Canvas.SetLeft(el, cx - rad); Canvas.SetTop(el, cy - rad); }
                        Place(core!, R0); Place(ring!, R0 * 1.35 + 1);
                        ring!.StrokeThickness = 1 + R0 * 0.18; ring.Opacity = 0.85 * grow * coll;
                        double fi = Smooth(0.88, 0.93, p) * (1 - Smooth(0.94, 1.0, p));
                        SetGlint(ref flash, cx, cy, 4, fi * 1.3, C("#FFFFEBCC"));
                    }
                    else if (chars != null)
                    {
                        for (int i = 0; i < chars.Count; i++)
                        {
                            var c = chars[i];
                            ((SolidColorBrush)c.Run.Foreground).Opacity = 1 - Smooth(c.Tc - 0.03, c.Tc + 0.07, p);
                            double gi = Math.Exp(-Math.Pow((p - c.Tc - 0.02) / 0.05, 2));
                            var g = c.G;
                            SetGlint(ref g, c.P.X, c.P.Y, 2.0, gi > 0.02 ? gi * 0.95 : 0, C("#FFFFF2CC"));
                            chars[i] = (c.Run, c.Tc, c.P, g);
                        }
                        r.Box.Opacity = r.Mark.Opacity = 1 - Smooth(0.6, 1.0, p);
                    }
                    // the rest of the list closes the gap
                    if (t >= ROWFX)
                    {
                        double c = Smooth(ROWFX, ROWFX + ROWCOL, t);
                        fold.ScaleY = Math.Max(0.001, 1 - c);
                        r.Grid.Margin = new Thickness(baseMargin.Left, baseMargin.Top * (1 - c), baseMargin.Right, baseMargin.Bottom * (1 - c));
                        r.Grid.Opacity = Math.Min(r.Grid.Opacity, 1 - c);
                    }
                    if (t >= ROWFX + ROWCOL) Finish();
                }
                catch (Exception ex) { Log.Write("item warp: " + ex); Finish(); }
            };
            CompositionTarget.Rendering += tick;
            // safety: frames can stop while every window is hidden; never leave the list waiting
            var guard = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            guard.Tick += (s, e) => { guard.Stop(); Finish(); };
            guard.Start();
        }
    }
}
