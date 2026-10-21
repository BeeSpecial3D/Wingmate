using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wingmate.Core;

namespace Wingmate
{
    /// <summary>Locked checklist ("Lock window" / padlock = click-through): the mouse wheel still scrolls the list and the
    /// checkboxes still check and uncheck; everything else stays locked.
    /// How: Windows can't make only part of a click-through (WS_EX_TRANSPARENT) window take clicks, and HTTRANSPARENT from
    /// WM_NCHITTEST only passes clicks to windows of our own thread, never to the game. So while locked, a light timer reads
    /// the mouse position (GetCursorPos, no hook) and turns the click-through bit off only while the pointer is over the
    /// visible list, back on everywhere else (header, buttons, footer, edges), where clicks keep going to the game. Over the
    /// list the window takes the mouse so the wheel reaches it, but every click is swallowed except a left click on a
    /// checkbox or on the status line's "Undo" link (shown briefly after a check). The list itself is not hit-testable while locked, so rows never highlight and nothing looks different.</summary>
    public partial class ChecklistWindow
    {
        private readonly DispatcherTimer _lockTimer = new() { Interval = TimeSpan.FromMilliseconds(30) };
        private ChecklistItem? _lockedPress;
        private bool _lockedUndoPress;
        private bool? _lockTakesMouse;

        private sealed record Target(Rect Box, ChecklistItem Item, SectionView View);

        private void InitLockedInput()
        {
            _lockTimer.Tick += (s, e) => LockTick();
            IsVisibleChanged += (s, e) => UpdateLockTimer();
            PreviewMouseDown += LockedPreviewDown;
            PreviewMouseUp += LockedPreviewUp;
            ContextMenuOpening += (s, e) => { if (ClickThrough) e.Handled = true; };
        }

        /// <summary>Called whenever the lock turns on or off.</summary>
        private void ApplyLockedInput()
        {
            bool on = ClickThrough;
            SectionsHost.IsHitTestVisible = !on;                    // no hover, drag, edit or menus on rows and headers
            Scroller.Background = on ? Brushes.Transparent : null;  // ...but the scroller itself still catches the wheel
            ForceCursor = on;
            Cursor = on ? Cursors.Arrow : null;
            _lockedPress = null;
            _lockedUndoPress = false;
            _lockTakesMouse = null;
            UpdateLockTimer();
            if (on) LockTick();
        }

        private void UpdateLockTimer()
        {
            if (ClickThrough && IsVisible) _lockTimer.Start(); else _lockTimer.Stop();
        }

        private List<Target> Targets()
        {
            var list = new List<Target>();
            foreach (var v in SectionsHost.Children.OfType<SectionView>())
                foreach (var (box, item) in v.CheckTargets(this)) list.Add(new Target(box, item, v));
            return list;
        }

        private static Box ToBox(Rect r) => new(r.X, r.Y, r.Width, r.Height);

        /// <summary>Zone and checkbox under a point in this window's coordinates.</summary>
        private (LockZone Zone, Target? Hit) LockZoneAt(Point p)
        {
            if (!Scroller.IsVisible || Scroller.ActualHeight <= 0) return (LockZone.PassThrough, null);
            Rect listRect;
            try { listRect = Scroller.TransformToAncestor(this).TransformBounds(new Rect(0, 0, Scroller.ActualWidth, Scroller.ActualHeight)); }
            catch (InvalidOperationException) { return (LockZone.PassThrough, null); }
            Box? undo = null;
            if (UndoBtn.IsVisible && UndoBtn.ActualWidth > 0)
            {
                try { undo = ToBox(UndoBtn.TransformToAncestor(this).TransformBounds(new Rect(0, 0, UndoBtn.ActualWidth, UndoBtn.ActualHeight))); }
                catch (InvalidOperationException) { }
            }
            if (undo is Box ub && ub.Inflate(LockedInput.UndoSlop).Contains(p.X, p.Y)) return (LockZone.Undo, null);
            if (!ToBox(listRect).Contains(p.X, p.Y)) return (LockZone.PassThrough, null); // outside the list: no need to measure checkboxes
            var targets = Targets();
            var (zone, i) = LockedInput.Classify(p.X, p.Y, ToBox(listRect), targets.Select(t => ToBox(t.Box)).ToList(), undo);
            return (zone, i >= 0 ? targets[i] : null);
        }

        private void LockTick()
        {
            if (!ClickThrough || !IsVisible || Handle == IntPtr.Zero) return;
            LockZone zone = LockZone.PassThrough;
            try
            {
                if (Native.TryGetCursorPos(out int x, out int y) && PresentationSource.FromVisual(this) != null)
                    zone = LockZoneAt(PointFromScreen(new Point(x, y))).Zone;
            }
            catch (Exception ex) { Log.Write("checklist lock: " + ex.Message); }
            bool take = LockedInput.TakesMouse(zone);
            if (_lockTakesMouse != take)
            {
                _lockTakesMouse = take;
                Native.SetTransparentBit(Handle, !take);
            }
            var cur = zone == LockZone.Checkbox || zone == LockZone.Undo ? Cursors.Hand : Cursors.Arrow;
            if (Cursor != cur) Cursor = cur;
        }

        private void LockedPreviewDown(object sender, MouseButtonEventArgs e)
        {
            if (!ClickThrough) return;
            e.Handled = true; // locked: nothing in the window reacts to a press...
            _lockedPress = null;
            _lockedUndoPress = false;
            if (e.ChangedButton != MouseButton.Left) return;
            var (zone, hit) = LockZoneAt(e.GetPosition(this));
            if (LockedInput.ClickToggles(zone) && hit != null) _lockedPress = hit.Item; // ...except a checkbox (toggles on release)
            _lockedUndoPress = LockedInput.ClickUndoes(zone);                            // ...and the Undo link
        }

        private void LockedPreviewUp(object sender, MouseButtonEventArgs e)
        {
            if (!ClickThrough) return;
            e.Handled = true;
            var pressed = _lockedPress;
            bool undoPressed = _lockedUndoPress;
            _lockedPress = null;
            _lockedUndoPress = false;
            if (e.ChangedButton != MouseButton.Left) return;
            var (zone, hit) = LockZoneAt(e.GetPosition(this));
            if (undoPressed) { if (LockedInput.ClickUndoes(zone)) UndoLastCheck(); return; }
            if (pressed == null) return;
            if (LockedInput.ClickToggles(zone) && hit != null && ReferenceEquals(hit.Item, pressed)) hit.View.ToggleFromLocked(pressed);
        }
    }
}
