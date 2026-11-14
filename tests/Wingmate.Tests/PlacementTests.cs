using System.IO;
using Wingmate.Core;
using Xunit;

public class PlacementTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1040, 400, 300, 760, 370)]          // 1080p primary, taskbar at the bottom
    [InlineData(0, 0, 3840, 2080, 800, 600, 1520, 740)]         // 4K at 200%: physical pixels, same middle
    [InlineData(1920, 0, 4480, 1400, 480, 360, 2960, 520)]      // second monitor to the right
    [InlineData(-1920, 200, 0, 1240, 400, 300, -1160, 570)]     // monitor to the left, lower down
    [InlineData(0, 40, 1920, 1080, 400, 300, 760, 410)]         // taskbar at the top
    public void Centers_InTheWorkArea(int l, int t, int r, int b, int w, int h, int x, int y)
        => Assert.Equal((x, y), Placement.Center(l, t, r, b, w, h));

    [Fact] public void BiggerThanTheScreen_StartsAtTheTopLeft()
        => Assert.Equal((0, 0), Placement.Center(0, 0, 800, 600, 1000, 900));

    [Fact] public void CouponPopup_OpensCentered_NotOnItsOwner()
    {
        var src = File.ReadAllText(Path.Combine("..", "..", "..", "..", "..", "src", "Wingmate", "CouponPopup.cs"));
        Assert.Contains("WindowSnap.CenterOnScreen(this, _monitorOf)", src);
        Assert.DoesNotContain("CenterOwner", src);
    }
}
