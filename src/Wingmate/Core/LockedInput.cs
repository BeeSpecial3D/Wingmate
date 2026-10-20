using System.Collections.Generic;

namespace Wingmate.Core
{
    /// <summary>A rectangle in window coordinates (device-independent pixels).</summary>
    public readonly record struct Box(double X, double Y, double W, double H)
    {
        public bool Contains(double x, double y) => x >= X && x < X + W && y >= Y && y < Y + H;
        public Box Inflate(double d) => new(X - d, Y - d, W + 2 * d, H + 2 * d);
    }

    /// <summary>Where the mouse is over a locked checklist window.</summary>
    public enum LockZone
    {
        /// <summary>Header, buttons, footer, edges: clicks go straight through to the game (as before).</summary>
        PassThrough,
        /// <summary>Over the list: the window takes the mouse so the wheel scrolls it; clicks here do nothing.</summary>
        List,
        /// <summary>Over an item's checkbox: a click checks or unchecks it.</summary>
        Checkbox,
    }

    /// <summary>Locked checklist: the wheel still scrolls the list and the checkboxes still toggle; nothing else takes clicks.
    /// Pure geometry so it can be tested without a window.</summary>
    public static class LockedInput
    {
        /// <summary>Extra margin around each 14 px checkbox so it is easy to hit.</summary>
        public const double CheckboxSlop = 3;

        /// <summary>The zone under (x, y) and, for a checkbox, its index in <paramref name="checkboxes"/>. Only what is inside
        /// the visible list area counts (checkboxes scrolled out of view never take clicks).</summary>
        public static (LockZone Zone, int Index) Classify(double x, double y, Box list, IReadOnlyList<Box> checkboxes)
        {
            if (!list.Contains(x, y)) return (LockZone.PassThrough, -1);
            for (int i = 0; i < checkboxes.Count; i++)
                if (checkboxes[i].Inflate(CheckboxSlop).Contains(x, y)) return (LockZone.Checkbox, i);
            return (LockZone.List, -1);
        }

        /// <summary>True when the window must take the mouse there (not click-through).</summary>
        public static bool TakesMouse(LockZone zone) => zone != LockZone.PassThrough;

        /// <summary>True when a click there may change something (only a checkbox).</summary>
        public static bool ClickToggles(LockZone zone) => zone == LockZone.Checkbox;
    }
}
