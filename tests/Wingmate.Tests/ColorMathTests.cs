using Wingmate.Core;
using Xunit;
using static Wingmate.Core.ColorMath;

namespace Wingmate.Tests
{
    public class ColorMathTests
    {
        [Theory]
        [InlineData("#000000")] [InlineData("#FFFFFF")] [InlineData("#FC0303")] [InlineData("#1E140A")]
        [InlineData("#FFC53D")] [InlineData("#3D7BFF")] [InlineData("#808080")] [InlineData("#01FE7F")] [InlineData("#F0A030")]
        public void HexToHsvAndBack_IsExact(string hex)
        {
            var c = ParseHex(hex)!.Value;
            var (h, s, v) = ToHsv(c);
            Assert.Equal(hex, Hex(FromHsv(h, s, v)));
        }

        [Fact]
        public void ReferenceRed()
        {
            var (h, s, v) = ToHsv(ParseHex("#fc0303")!.Value);
            Assert.Equal(0, h, 6);
            Assert.Equal(249.0 / 252, s, 6);
            Assert.Equal(252.0 / 255, v, 6);
        }

        [Fact]
        public void KnownHsv()
        {
            Assert.Equal("#FF0000", Hex(FromHsv(0, 1, 1)));
            Assert.Equal("#00FF00", Hex(FromHsv(120, 1, 1)));
            Assert.Equal("#0000FF", Hex(FromHsv(240, 1, 1)));
            Assert.Equal("#FF0000", Hex(FromHsv(360, 1, 1))); // 360 wraps to 0
            Assert.Equal("#FFFFFF", Hex(FromHsv(200, 0, 1)));  // top-left of the square: white
            Assert.Equal("#000000", Hex(FromHsv(200, 1, 0)));  // bottom: black
            Assert.Equal("#FF0000", Hex(FromHsv(0, 2, 2)));    // clamped
            var (h, _, _) = ToHsv(ParseHex("#00FFFF")!.Value); Assert.Equal(180, h, 6);
            var (h2, s2, v2) = ToHsv(ParseHex("#000000")!.Value); Assert.Equal((0d, 0d, 0d), (h2, s2, v2));
        }

        [Fact]
        public void HexParsing()
        {
            Assert.Equal(new Rgb(0xFC, 3, 3), ParseHex("fc0303"));
            Assert.Equal(new Rgb(0xFC, 3, 3), ParseHex("#FC0303"));
            Assert.Null(ParseHex("#zz0303"));
            Assert.Null(ParseHex(""));
            Assert.Null(ParseHex(null));
        }
    }
}
