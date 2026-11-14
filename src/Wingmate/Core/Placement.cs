using System;

namespace Wingmate.Core
{
    /// <summary>Window placement math (pure, so it is unit tested). Works in physical pixels.</summary>
    public static class Placement
    {
        /// <summary>Top-left that centres a window of the given size in a work area; a window bigger than the area starts at
        /// its top-left corner so the title and close button stay on screen.</summary>
        public static (int X, int Y) Center(int left, int top, int right, int bottom, int width, int height)
        {
            int x = left + ((right - left) - width) / 2;
            int y = top + ((bottom - top) - height) / 2;
            return (Math.Max(left, x), Math.Max(top, y));
        }
    }
}
